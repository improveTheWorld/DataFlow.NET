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
/// var people = Read.AsCsvSync&lt;Person&gt;("data.csv").ToList()
/// var valid = people.Where(p => p.Age > 0 && !string.IsNullOrEmpty(p.Name));
/// </code>
/// </remarks>
public sealed class CtorMaterializationSession<T>
{
    private readonly Func<object?[], object> _ctorFactory;
    private readonly (int valueIndex, Type paramType)[] _paramMap;

    public CtorMaterializationSession(string[] schema, bool resolveSchema = true)
    {
        // Optional resolution step
        var effectiveSchema = resolveSchema
            ? SchemaMemberResolver.ResolveSchemaToMembers<T>(schema)
            : schema;

        var type = typeof(T);

        // Reuse schema dict cache from MemberMaterializationPlanner
        var schemaDict = MemberMaterializationPlanner.Get<T>().GetSchemaDict(schema);

        // Select primary ctor (longest)
        var ctor = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                       .OrderByDescending(c => c.GetParameters().Length)
                       .FirstOrDefault()
                   ?? throw new InvalidOperationException($"Type {type.FullName} has no accessible constructors.");

        var parms = ctor.GetParameters();
        _paramMap = new (int, Type)[parms.Length];

        for (int i = 0; i < parms.Length; i++)
        {
            var p = parms[i];
            var name = p.Name ?? string.Empty;
            int idx = schemaDict.TryGetValue(name, out var colIndex) ? colIndex : -1;
            _paramMap[i] = (idx, p.ParameterType);
        }

        // Build a stable signature key for this ctor so we reuse a compiled delegate
        var key = BuildCtorKey(type, ctor);

        if (!ObjectMaterializer._ctorCache.TryGetValue(key, out var factory))
        {
            factory = ObjectMaterializer.CompileFactoryDelegate(ctor);
            ObjectMaterializer._ctorCache[key] = factory;
        }

        _ctorFactory = factory;
    }


    public T Create(object?[] values)
    {
        var args = new object?[_paramMap.Length];
        for (int i = 0; i < _paramMap.Length; i++)
        {
            var (idx, _) = _paramMap[i];
            args[i] = (uint)idx < (uint)values.Length ? values[idx] : null;
        }
        return (T)_ctorFactory(args);
    }

    private static ObjectMaterializer.CtorSignatureKey BuildCtorKey(Type type, ConstructorInfo ctor)
    {
        // Build signature from parameter types
        var parms = ctor.GetParameters();
        var hash = new HashCode();
        hash.Add(type);
        hash.Add(parms.Length);
        for (int i = 0; i < parms.Length; i++)
            hash.Add(parms[i].ParameterType);
        return new ObjectMaterializer.CtorSignatureKey(type, hash.ToHashCode().ToString());
    }
}

public sealed class GeneralMaterializationSession<T>
{
    private enum StrategyKind { PrimaryCtor, MemberApply }

    private readonly StrategyKind _strategy;
    private readonly Func<T>? _factory;
    private readonly Action<T, object?[]>? _apply;
    private readonly CtorMaterializationSession<T>? _ctorSession;

    public GeneralMaterializationSession(string[] schema, bool resolveSchema = true)
    {
        // Optional resolution step
        var effectiveSchema = resolveSchema
            ? SchemaMemberResolver.ResolveSchemaToMembers<T>(schema)
            : schema;

        // Prefer ctor-based materialization if possible
        if (TryInitCtorSession(schema, out var ctorSession))
        {
            _ctorSession = ctorSession;
            _strategy = StrategyKind.PrimaryCtor;
            return;
        }

        // Fallback: parameterless + member apply
        var plan = MemberMaterializationPlanner.Get<T>();
        if (!ObjectMaterializer.TryGetParameterlessFactory<T>(out var f))
        {
            throw new InvalidOperationException(
                $"Type {typeof(T).FullName} has no accessible constructor and no parameterless constructor.");
        }

        _factory = f;
        _apply = plan.GetSchemaAction(schema); // cached action
        _strategy = StrategyKind.MemberApply;
    }

    public T Create(object?[] values)
    {
        if (_strategy == StrategyKind.PrimaryCtor)
            return _ctorSession!.Create(values);

        var obj = _factory!();
        _apply!(obj, values);
        return obj;
    }

    public void Feed(ref T obj, object?[] values)
    {
        if (_strategy != StrategyKind.MemberApply)
            throw new InvalidOperationException($"Type {typeof(T).FullName} does not support feeding without a parameterless constructor.");
        _apply!(obj, values);
    }

    public bool UsesMemberApply => _strategy == StrategyKind.MemberApply;

    private static bool TryInitCtorSession(string[] schema, out CtorMaterializationSession<T>? session)
    {
        try
        {
            // This will throw only if no accessible ctors exist; otherwise it builds the map and reuses caches
            session = new CtorMaterializationSession<T>(schema);
            return true;
        }
        catch (InvalidOperationException)
        {
            session = null;
            return false;
        }
    }
}


public static class ObjectMaterializer
{
    // ---------------------------
    // Caches
    // ---------------------------

    internal static readonly ConcurrentDictionary<CtorSignatureKey, Func<object?[], object>> _ctorCache = new();


    internal static MaterializationSession<T> CreateFeedSession<T>(string[] schema)
       => new MaterializationSession<T>(schema);

    internal static CtorMaterializationSession<T> CreateCtorSession<T>(string[] schema)
        => new CtorMaterializationSession<T>(schema);
    public static GeneralMaterializationSession<T> CreateGeneralSession<T>(string[] schema)
       => new GeneralMaterializationSession<T>(schema);

    public static string[] ResolveSchema<T>(string[] schema) => 
        SchemaMemberResolver.ResolveSchemaToMembers<T>(schema);


    // Key that represents a constructor signature for caching compiled delegates
    internal readonly record struct CtorSignatureKey(Type type, string Signature)
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
        Exception? exCtor = null;

        // 1) Fast path: constructor with schema mapping
        try
        {
            return CreateViaPrimaryConstructorWithSchema<T>(schema, parameters);
        }
        catch (InvalidOperationException ex)
        {
            // Save and try the fallback to keep old behavior if possible
            exCtor = ex;
        }
        catch (Exception ex) when (ex is InvalidCastException or FormatException or ArgumentException)
        {
            exCtor = new InvalidOperationException(
                $"Failed to materialize type {typeof(T).FullName} using primary constructor with schema. " +
                $"Schema columns: [{string.Join(", ", schema)}]. " +
                $"Parameter count: {parameters.Length}. " +
                $"Error: {ex.Message}",
                ex);
        }

        // 2) Fallback: parameterless + member feeding (existing behavior)
        try
        {
            if (!TryGetParameterlessFactory<T>(out var factory))
            {
                // No parameterless ctor; rethrow original ctor-based error
                throw exCtor!;
            }

            T instance = factory();
            var plan = MemberMaterializationPlanner.Get<T>();

            // Reuse cached action per (T, culture, thousands, formats, schema)
            var apply = plan.GetSchemaAction(schema);
            apply(instance, parameters);
            return instance;
        }
        catch (Exception ex)
        {
            // 3) Both paths failed: rethrow the first (ctor-based) error for clearer diagnostics
            throw new InvalidOperationException(ex!.Message + exCtor!.Message, ex);
        }
    }

    // ---------------------------
    // CORE CREATION METHODS
    // ---------------------------

    private static bool TryCreateViaBestConstructor<T>(object?[] parameters, out T? instance)
    {
        instance = default;

        if (parameters.Length == 0)
        {
            // Try parameterless constructor
            if (TryGetParameterlessFactory<T>(out var factory))
            {
                instance = factory();
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
        if (TryResolveConstructor<T>(parameters, out var ctor))
        {
            ctorFactory = CompileFactoryDelegate(ctor);
            _ctorCache[key] = ctorFactory;
            instance = (T?)ctorFactory(parameters);
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

        T instance = factory();
        MemberMaterializer.FeedUsingInternalOrder(ref instance, parameters);
        return instance;
    }


    /// <summary>
    /// Creates instance using primary constructor by mapping schema names to constructor parameters.
    /// Used for records and classes without parameterless constructors.
    /// </summary>
    private static T CreateViaPrimaryConstructorWithSchema<T>(string[] schema, object?[] values)
    {
        Type type = typeof(T);

        //  USE CACHED SCHEMA DICTIONARY
        var schemaDict = MemberMaterializationPlanner.Get<T>().GetSchemaDict(schema);

        // Find primary constructor (longest parameter list, typically)
        var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                        .OrderByDescending(c => c.GetParameters().Length)
                        .ToArray();

        if (ctors.Length == 0)
            throw new InvalidOperationException($"Type {type.FullName} has no accessible constructors.");

        var attemptedCtors = new List<string>();

        foreach (var ctor in ctors)
        {
            var ctorParams = ctor.GetParameters();
            if (ctorParams.Length == 0) continue;

            var args = new object?[ctorParams.Length];
            bool allMatched = true;
            var missingParams = new List<string>();

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
                    allMatched = false;
                    missingParams.Add($"{param.ParameterType.Name} {paramName}");
                    break;
                }
            }

            if (allMatched)
            {
                try
                {
                    var key = BuildSignatureKey<T>(args);
                    var factory = CompileFactoryDelegate(ctor);
                    _ctorCache[key] = factory;
                    return (T)factory(args);
                }
                catch (Exception ex) when (ex is InvalidCastException or FormatException)
                {
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
    internal static Func<object?[], object> CompileFactoryDelegate(ConstructorInfo ctor)
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
    private static readonly ConcurrentDictionary<Type, Func<object>?> _parameterlessFactoryCache = new();

    public static bool TryGetParameterlessFactory<T>(out Func<T> factory)
    {
        var type = typeof(T);

        var cachedFactory = _parameterlessFactoryCache.GetOrAdd(type, t =>
        {
            // Value types always work
            if (t.IsValueType)
                return () => Activator.CreateInstance(t);

            // For reference types, try parameterless constructor
            var ctor = t.GetConstructor(Type.EmptyTypes);
            if (ctor != null)
                return () => ctor.Invoke(null);

            return null;
        });

        if (cachedFactory != null)
        {
            factory = () => (T)cachedFactory();
            return true;
        }

        factory = null!;
        return false;
    }

    // ---------------------------
    // Signature Key Construction
    // ---------------------------
    private static CtorSignatureKey BuildSignatureKey<T>(object?[] args)
    {
        // Use HashCode struct instead of string concatenation
        var hash = new HashCode();
        hash.Add(typeof(T));
        hash.Add(args.Length);

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == null)
            {
                hash.Add(typeof(void)); // Marker for null
            }
            else
            {
                hash.Add(args[i].GetType());
            }
        }

        return new CtorSignatureKey(typeof(T), hash.ToHashCode().ToString());
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