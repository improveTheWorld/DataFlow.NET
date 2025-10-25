using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace DataFlow.Framework;

/// <summary>
/// ObjectMaterializer Conversion Rules
/// </summary>
/// 
/// <remarks>
/// <para><b>Lenient Behavior (No Exceptions):</b></para>
/// <list type="bullet">
///   <item>Empty/null/whitespace → Value types: default(T) (0, false, etc.)</item>
///   <item>Empty/null/whitespace → Nullable types: null</item>
/// </list>
/// 
/// <para><b>In CSV Pipeline Context:</b></para>
/// Invalid string values (e.g., "abc" for int) are handled by <see cref="CsvReadOptions.ConvertFieldValue"/>
/// BEFORE reaching ObjectMaterializer. Failed conversions result in default values, not exceptions.
/// 
/// <para><b>Direct Usage (Outside CSV Pipeline):</b></para>
/// Type conversion failures throw standard .NET exceptions (FormatException, InvalidCastException, OverflowException).
/// 
/// <para><b>Recommendation:</b></para>
/// Load data leniently, then apply business validation rules to filter/flag invalid records.
/// 
/// <code>
/// var people = Read.AsCsvSync&lt;Person&gt;("data.csv").ToList();
/// var valid = people.Where(p => p.Age > 0 && !string.IsNullOrEmpty(p.Name));
/// </code>
/// </remarks>
public static class ObjectMaterializer
{
    // ---------------------------
    // Caches
    // ---------------------------
    private static readonly ConcurrentDictionary<Type, Func<object>> _parameterlessCache = new();
    private static readonly ConcurrentDictionary<CtorSignatureKey, Func<object?[], object>> _ctorCache = new();

    // Key that represents a constructor signature for caching compiled delegates
    private readonly record struct CtorSignatureKey(Type type, string Signature)
    {
        public override int GetHashCode() => HashCode.Combine(type, Signature);
    }


    public static T? Create<T>(params object[] parameters)
    {
        // Original semantics: attempt constructor with string[] parameters (treated as object[]),
        // else fallback to internal order feeder.
        if (TryCreateViaBestConstructor<T>(parameters, out var instance))
            return instance;

        // Fallback with error warning if feeding fails
        try
        {
            return NewUsingInternalOrder<T>(parameters);
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException)
        {
            throw new InvalidOperationException(
                $"No matching constructor found for type {typeof(T).FullName} with provided parameters, " +
                $"and member feeding failed: {ex.Message}",
                ex);
        }
    }

    public static T? Create<T>(string[] schema, params object[] parameters)
    {
        try
        {
            T instance = NewWithSchema<T>(schema, parameters);

            // Optional: warn about unmapped members
            var plan = MemberMaterializationPlanner.Get<T>();
            var unmapped = plan.Members
                .Where(m => !schema.Contains(m.Name, plan.NameComparer))
                .Select(m => m.Name);

            if (unmapped.Any())
                Trace.WriteLine($"[ObjectMaterializer] Unmapped members in {typeof(T).Name}: {string.Join(", ", unmapped)}");

            return instance;
        }
        catch (InvalidOperationException)
        {
            // Already has meaningful message from CreateViaPrimaryConstructorWithSchema
            throw;
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or ArgumentException)
        {
            throw new InvalidOperationException(
                $"Failed to materialize type {typeof(T).FullName} using schema. " +
                $"Schema columns: [{string.Join(", ", schema)}]. " +
                $"Parameter count: {parameters.Length}. " +
                $"Error: {ex.Message}",
                ex);
        }
    }

    // ---------------------------
    // CORE CREATION METHODS
    // ---------------------------

    /// <summary>
    /// Creates an instance using schema-based member feeding (for records/classes without matching constructors).
    /// </summary>
    public static T? CreateWithSchema<T>(string[] schema, object?[] parameters)
    {
        return (T?)NewWithSchema<T>( schema, parameters);
    }

    private static bool TryCreateViaBestConstructor<T>( object?[] parameters, out T? instance)
    {
        instance = default;

        if (parameters.Length == 0)
        {
            // Try parameterless constructor
            if (TryGetParameterlessFactory<T>(out var factory))
            {
                instance =  (T?)factory();
                return true;
            }
            return false;
        }

        var key = BuildSignatureKey<T>(parameters);
        if (_ctorCache.TryGetValue(key, out var ctorFactory))
        {
            try
            {
                instance = (T?)ctorFactory(parameters);
                return true;
            }
            catch
            {
                // Very rare: cached delegate mismatch (e.g. dynamic type change or passed incompatible null)
                // Purge and retry resolution once.
                _ctorCache.TryRemove(key, out _);
            }
        }

        // Resolve & compile
        if (TryResolveConstructor<T>( parameters, out var ctor))
        {
            ctorFactory = CompileFactoryDelegate(ctor);
            _ctorCache[key] = ctorFactory;
            instance = instance = (T?)ctorFactory(parameters);
            return true;
        }

        return false;
    }

    private static T NewUsingInternalOrder<T>(params object?[] parameters)
    {
        // Try parameterless constructor first
        if (!TryGetParameterlessFactory<T>(out var factory))
        {
            throw new InvalidOperationException(
                $"Type {typeof(T).FullName} has no public parameterless constructor and no matching constructor for provided parameters.");
        }

        T instance = (T)factory();
        return MemberMaterializer.FeedUsingInternalOrder(instance, parameters);
    }

    private static T NewWithSchema<T>( string[] schema, params object?[] parameters)
    {
        Type newObjectType = typeof(T);
        if (schema == null) throw new ArgumentNullException(nameof(schema));

        // Try parameterless constructor first
        if (!TryGetParameterlessFactory<T>(out var factory))
        {
            // For records/types without parameterless ctor, try primary constructor with schema mapping
            return CreateViaPrimaryConstructorWithSchema<T>( schema, parameters);
        }

        T instance = (T) factory();
        var dict = MemberMaterializer.GetSchemaDictionary(schema, StringComparer.OrdinalIgnoreCase);
        return MemberMaterializer.FeedUsingSchema(instance, dict, parameters, caseInsensitiveHeaders: true);
    }

    /// <summary>
    /// Creates instance using primary constructor by mapping schema names to constructor parameters.
    /// Used for records and classes without parameterless constructors.
    /// </summary>
    private static T CreateViaPrimaryConstructorWithSchema<T>(string[] schema, object?[] values)
    {
        Type type = typeof(T);
        var schemaDict = MemberMaterializer.GetSchemaDictionary(schema, StringComparer.OrdinalIgnoreCase);

        // Find primary constructor (longest parameter list, typically)
        var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .OrderByDescending(c => c.GetParameters().Length)
                        .ToArray();

        if (ctors.Length == 0)
            throw new InvalidOperationException($"Type {type.FullName} has no accessible constructors.");

        var attemptedCtors = new List<string>();

        // Try each constructor (starting with longest - likely primary constructor for records)
        foreach (var ctor in ctors)
        {
            var ctorParams = ctor.GetParameters();
            if (ctorParams.Length == 0) continue;

            var args = new object?[ctorParams.Length];
            bool allMatched = true;
            var missingParams = new List<string>();

            // Try to find matching schema column (case-insensitive)
            for (int i = 0; i < ctorParams.Length; i++)
            {
                var param = ctorParams[i];
                var paramName = param.Name ?? string.Empty;

                if (schemaDict.TryGetValue(paramName, out var colIndex) &&
                    (uint)colIndex < (uint)values.Length)
                {
                    args[i] = values[colIndex];
                }
                else if (param.HasDefaultValue)
                {
                    args[i] = param.DefaultValue;
                }
                else
                {
                    // Required parameter not found in schema
                    allMatched = false;
                    missingParams.Add($"{param.ParameterType.Name} {paramName}");
                    break;
                }
            }

            if (allMatched)
            {
                try
                {
                    // Cache and invoke
                    var key = BuildSignatureKey<T>(args);
                    var factory = CompileFactoryDelegate(ctor);
                    _ctorCache[key] = factory;
                    return (T)factory(args);
                }
                catch (Exception ex) when (ex is InvalidCastException or FormatException)
                {
                    // Constructor matched but conversion failed
                    throw new InvalidOperationException(
                        $"Constructor matched for {type.FullName} but parameter conversion failed. " +
                        $"Constructor: ({string.Join(", ", ctorParams.Select(p => $"{p.ParameterType.Name} {p.Name}"))}). " +
                        $"Error: {ex.Message}",
                        ex);
                }
            }
            else
            {
                attemptedCtors.Add(
                    $"({string.Join(", ", ctorParams.Select(p => $"{p.ParameterType.Name} {p.Name}"))}) " +
                    $"- missing: {string.Join(", ", missingParams)}");
            }
        }

        throw new InvalidOperationException(
            $"Cannot materialize {type.FullName}:\n" +
            $"  Schema columns: [{string.Join(", ", schema)}]\n" +
            $"  Attempted constructors:\n    " +
            string.Join("\n    ", attemptedCtors));
    }


    // ---------------------------
    // Constructor Resolution
    // ---------------------------
    private static bool TryResolveConstructor<T>(object?[] args, out ConstructorInfo ctor)
    {
        ctor = null!;
        var ctors = typeof(T).GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

        if (ctors.Length == 0)
            return false;

        // In TryResolveConstructor:
        var scoredCtors = ctors
            .Select(c => (ctor: c, score: ScoreConstructor(c, args)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .ToList();

        if (scoredCtors.Any())
        {
            ctor = scoredCtors.First().ctor;
            return true;
        }

        return false;
}
    private static int ScoreConstructor(ConstructorInfo ctor, object?[] args)
    {
        int score = 0;
        var parms = ctor.GetParameters();

        if (parms.Length != args.Length)
            return 0; // Wrong parameter count

        for (int i = 0; i < parms.Length; i++)
        {
            if (args[i] == null)
            {
                if (!parms[i].ParameterType.IsValueType ||
                    Nullable.GetUnderlyingType(parms[i].ParameterType) != null)
                    score += 2;
                else
                    return 0; // Can't pass null to non-nullable value type
                continue;
            }

            var argType = args[i].GetType();
            var paramType = parms[i].ParameterType;

            if (paramType == argType)
                score += 10; // Exact match
            else if (paramType.IsAssignableFrom(argType))
                score += 5; // Widening (e.g., object from string)
            else if (IsConvertible(args[i], paramType))
                score += 1; // Actually convertible
            else
                return 0; // Incompatible - reject this constructor
        }
        return score;
    }

    private static bool IsConvertible(object value, Type targetType)
    {
        try
        {
            // Use your existing ConvertObject logic or a simplified check
            if (value is string s)
            {
                var nullable = Nullable.GetUnderlyingType(targetType);
                var actualType = nullable ?? targetType;

                // Quick checks for common types
                if (actualType == typeof(int)) return int.TryParse(s, out _);
                if (actualType == typeof(long)) return long.TryParse(s, out _);
                if (actualType == typeof(decimal)) return decimal.TryParse(s, out _);
                if (actualType == typeof(double)) return double.TryParse(s, out _);
                if (actualType == typeof(bool)) return bool.TryParse(s, out _);
                if (actualType == typeof(DateTime)) return DateTime.TryParse(s, out _);
                if (actualType.IsEnum) return Enum.TryParse(actualType, s, true, out _);
            }

            // Fallback to Convert.ChangeType check
            Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ---------------------------
    // Delegate Compilation
    // ---------------------------
    private static Func<object?[], object> CompileFactoryDelegate(ConstructorInfo ctor)
    {
        var argsParam = Expression.Parameter(typeof(object?[]), "args");
        var ctorParams = ctor.GetParameters();

        var argExprs = new Expression[ctorParams.Length];
        for (int i = 0; i < ctorParams.Length; i++)
        {
            var pInfo = ctorParams[i];
            var indexExpr = Expression.ArrayIndex(argsParam, Expression.Constant(i));

            // For reference/nullable types we can simplify:
            if (!pInfo.ParameterType.IsValueType ||
                Nullable.GetUnderlyingType(pInfo.ParameterType) != null)
            {
                argExprs[i] = Expression.Convert(indexExpr, pInfo.ParameterType);
            }
            else
            {
                // Convert with graceful handling for null (Convert(null) to value type would throw).
                argExprs[i] = Expression.Convert(
                    Expression.Condition(
                        test: Expression.Equal(indexExpr, Expression.Constant(null)),
                        ifTrue: GetDefaultExpression(pInfo.ParameterType),
                        ifFalse: Expression.Convert(indexExpr, pInfo.ParameterType)
                    ),
                    pInfo.ParameterType);
            }
        }

        var newExpr = Expression.New(ctor, argExprs);
        var body = Expression.Convert(newExpr, typeof(object));
        return Expression.Lambda<Func<object?[], object>>(body, argsParam).Compile();
    }

    private static Expression GetDefaultExpression(Type t)
    {
        if (t.IsValueType)
            return Expression.Default(t);
        return Expression.Constant(null, t);
    }

    // Parameterless factory with try pattern
    private static bool TryGetParameterlessFactory<T>( out Func<object> factory)
    {
        Type type = typeof(T);
        if (_parameterlessCache.TryGetValue(type, out factory!))
            return true;

        var ctor = type.GetConstructor(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
            null,
            Type.EmptyTypes,
            null);

        if (ctor == null)
        {
            factory = null!;
            return false;
        }

        var newExpr = Expression.New(ctor);
        var body = Expression.Convert(newExpr, typeof(object));
        factory = Expression.Lambda<Func<object>>(body).Compile();
        _parameterlessCache[type] = factory;
        return true;
    }

    // ---------------------------
    // Signature Key Construction
    // ---------------------------
    private static CtorSignatureKey BuildSignatureKey<T>(object?[] args)
    {
        // Signature encodes runtime types; null gets its own marker with position to reduce collisions.
        // Example: "System.String|#NULL#1|System.Int32"
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < args.Length; i++)
        {
            if (i > 0) sb.Append('|');
            var a = args[i];
            if (a == null)
            {
                sb.Append("#NULL#").Append(i);
            }
            else
            {
                sb.Append(a.GetType().FullName);
            }
        }

        return new CtorSignatureKey(typeof(T), sb.ToString());
    }

    // ---------------------------
    // Public Advanced API
    // ---------------------------
    public static T CreateOrFeed<T>(object?[] args, bool allowFeedFallback = true)
    {
        if (TryCreateViaBestConstructor<T>(args, out var inst))
            return inst!;

        if (!allowFeedFallback)
            throw new InvalidOperationException($"No matching constructor for type {typeof(T).FullName} and fallback disabled.");

        return NewUsingInternalOrder<T>(args);
    }
}