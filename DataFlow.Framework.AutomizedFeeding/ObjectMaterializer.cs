using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using DataFlow.Framework.AutomizedFeeding;

namespace DataFlow.Framework;

public static class ObjectMaterializer
{
    // ---------------------------
    // Caches
    // ---------------------------
    private static readonly ConcurrentDictionary<Type, Func<object>> _parameterlessCache = new();
    private static readonly ConcurrentDictionary<CtorSignatureKey, Func<object?[], object>> _ctorCache = new();

    // Key that represents a constructor signature for caching compiled delegates
    private readonly record struct CtorSignatureKey(Type TargetType, string Signature)
    {
        public override int GetHashCode() => HashCode.Combine(TargetType, Signature);
    }

    // Public API (backward compatible)
    public static T? Create<T>(params string[] parameters)
    {
        // Original semantics: attempt constructor with string[] parameters (treated as object[]),
        // else fallback to internal order feeder.
        if (TryCreateViaBestConstructor(typeof(T), parameters, out var instance))
            return (T?)instance;

        // Fallback
        return (T?)NewUsingInternalOrder(typeof(T), parameters);
    }

    public static T? Create<T>(string[] schema, params object[] parameters)
    {
        return (T?)NewWithSchema(typeof(T), schema, parameters);
    }

    // ---------------------------
    // CORE CREATION METHODS
    // ---------------------------
    private static bool TryCreateViaBestConstructor(Type type, object?[] parameters, out object? instance)
    {
        instance = null;

        if (parameters.Length == 0)
        {
            instance = GetOrCreateParameterless(type)();
            return true;
        }

        var key = BuildSignatureKey(type, parameters);
        if (_ctorCache.TryGetValue(key, out var factory))
        {
            try
            {
                instance = factory(parameters);
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
        if (TryResolveConstructor(type, parameters, out var ctor))
        {
            factory = CompileFactoryDelegate(ctor);
            _ctorCache[key] = factory;
            instance = factory(parameters);
            return true;
        }

        return false;
    }

    private static object NewUsingInternalOrder(Type objectType, params object?[] parameters)
    {
        var instance = GetOrCreateParameterless(objectType)();
        return Feeder.FeedUsingInternalOrder(instance, parameters);
    }

    private static object NewWithSchema(Type newObjectType, string[] schema, params object?[] parameters)
    {
        if (schema == null) throw new ArgumentNullException(nameof(schema));

        var instance = GetOrCreateParameterless(newObjectType)();
        return Feeder.FeedUsingSchema(instance, Feeder.GetSchemaDictionary(schema), parameters);
    }

    // ---------------------------
    // Constructor Resolution
    // ---------------------------
    private static bool TryResolveConstructor(Type type, object?[] args, out ConstructorInfo ctor)
    {
        ctor = null!;
        var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        if (ctors.Length == 0)
            return false;

        // Simple best-fit strategy: first viable; can be refined (score-based).
        foreach (var candidate in ctors)
        {
            var parms = candidate.GetParameters();
            if (parms.Length != args.Length) continue;

            bool viable = true;
            for (int i = 0; i < parms.Length; i++)
            {
                var pType = parms[i].ParameterType;
                var arg = args[i];

                if (arg is null)
                {
                    if (pType.IsValueType && Nullable.GetUnderlyingType(pType) == null)
                    {
                        viable = false;
                        break;
                    }
                }
                else
                {
                    var aType = arg.GetType();
                    if (!pType.IsAssignableFrom(aType))
                    {
                        viable = false;
                        break;
                    }
                }
            }

            if (viable)
            {
                ctor = candidate;
                return true;
            }
        }

        return false;
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
            // Convert with graceful handling for null (Convert(null) to value type would throw).
            Expression converted = Expression.Convert(
                Expression.Condition(
                    test: Expression.Equal(indexExpr, Expression.Constant(null)),
                    ifTrue: GetDefaultExpression(pInfo.ParameterType),
                    ifFalse: Expression.Convert(indexExpr, pInfo.ParameterType)
                ),
                pInfo.ParameterType);

            // For reference/nullable types we can simplify:
            if (!pInfo.ParameterType.IsValueType ||
                Nullable.GetUnderlyingType(pInfo.ParameterType) != null)
            {
                converted = Expression.Convert(indexExpr, pInfo.ParameterType);
            }

            argExprs[i] = converted;
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

    // Parameterless factory
    private static Func<object> GetOrCreateParameterless(Type type)
    {
        return _parameterlessCache.GetOrAdd(type, static t =>
        {
            var ctor = t.GetConstructor(Type.EmptyTypes);
            if (ctor == null)
            {
                // Support for types without public parameterless constructor could
                // be extended here (FormatterServices, etc.)
                throw new InvalidOperationException($"Type {t.FullName} has no public parameterless constructor.");
            }
            var newExpr = Expression.New(ctor);
            var body = Expression.Convert(newExpr, typeof(object));
            return Expression.Lambda<Func<object>>(body).Compile();
        });
    }

    // ---------------------------
    // Signature Key Construction
    // ---------------------------
    private static CtorSignatureKey BuildSignatureKey(Type targetType, object?[] args)
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

        return new CtorSignatureKey(targetType, sb.ToString());
    }

    // ---------------------------
    // Public Advanced API
    // ---------------------------
    public static T CreateOrFeed<T>(object?[] args, bool allowFeedFallback = true)
    {
        if (TryCreateViaBestConstructor(typeof(T), args, out var inst))
            return (T)inst!;

        if (!allowFeedFallback)
            throw new InvalidOperationException($"No matching constructor for type {typeof(T).FullName} and fallback disabled.");

        return (T)NewUsingInternalOrder(typeof(T), args);
    }
}