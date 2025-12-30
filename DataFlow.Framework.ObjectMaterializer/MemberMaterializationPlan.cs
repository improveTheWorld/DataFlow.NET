
// FeedPlan: builds once per T and caches compiled setters and mapping metadata
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
namespace DataFlow.Framework;


public sealed class MaterializationSession<T>
{

    private readonly Func<T> _factory;
    private readonly Action<T, object?[]> _apply;

    public MaterializationSession(string[] schema)
    {
        // Reuse your parameterless factory cache
        if (!ObjectMaterializer.TryGetParameterlessFactory<T>(out var f))
            throw new InvalidOperationException($"{typeof(T).FullName} requires a parameterless constructor for feed sessions.");

        _factory = f;

        // Bind plan and cache the mapping/action once
        var plan = MemberMaterializationPlanner.Get<T>();
        _apply = plan.GetSchemaAction(schema); // cached in plan._schemaMappingCache
    }

    public T Create(object?[] values)
    {
        var obj = _factory();
        _apply(obj, values);
        return obj;
    }

    public void Feed(ref T obj, object?[] values) => _apply(obj, values);
}

public interface IHasSchema
{
    Dictionary<string, int> GetDictSchema();
}
internal static class MemberMaterializationPlanner
{
    private static readonly ConcurrentDictionary<PlanCacheKey, object> Cache = new();
    private readonly record struct PlanCacheKey(
    Type TargetType,
    string CultureName,
    bool AllowThousands,
    string DateTimeFormatsHash)
    {
        public static PlanCacheKey Create<T>(
            CultureInfo culture,
            bool allowThousands,
            string[] dateTimeFormats)
        {
            var formatsHash = dateTimeFormats.Length == 0
                ? string.Empty
                : string.Join("|", dateTimeFormats);

            return new PlanCacheKey(
                typeof(T),
                culture.Name,
                allowThousands,
                formatsHash);
        }
    }




    public static MemberMaterializationPlan<T> Get<T>(
        CultureInfo? culture = null,
        bool allowThousandsSeparators = true,
        string[]? dateTimeFormats = null)
    {
        var actualCulture = culture ?? CultureInfo.InvariantCulture;
        var actualFormats = dateTimeFormats ?? Array.Empty<string>();
        var key = PlanCacheKey.Create<T>(
            actualCulture,
            allowThousandsSeparators,
            actualFormats);
        return (MemberMaterializationPlan<T>)Cache.GetOrAdd(
            key,
            _ => MemberMaterializationPlan<T>.Build(
                actualCulture,
                allowThousandsSeparators,
                actualFormats));
    }
}

internal sealed class MemberMaterializationPlan<T>
{
    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;
    public bool AllowThousandsSeparators { get; init; } = true;
    public string[] DateTimeFormats { get; init; } = Array.Empty<string>();
    public readonly struct MemberSetter
    {
        public readonly string Name;
        public readonly int OrderIndex; // -1 if not ordered
        public readonly Action<T, object?> Set; // compiled setter
        public MemberSetter(string name, int orderIndex, Action<T, object?> set)
        { Name = name; OrderIndex = orderIndex; Set = set; }
    }

    public readonly MemberSetter[] Members;

    private readonly ConcurrentDictionary<SchemaKey, Action<T, object?[]>> _schemaMappingCache = new();

    internal readonly struct SchemaKey : IEquatable<SchemaKey>
    {

        private readonly int _hashCode;
        private readonly string[] _schema;

        public SchemaKey(string[] schema)
        {
            _schema = schema;
            var hash = new HashCode();
            hash.Add(schema.Length);
            foreach (var s in schema)
                hash.Add(s);
            _hashCode = hash.ToHashCode();
        }

        public override int GetHashCode() => _hashCode;

        public bool Equals(SchemaKey other)
        {
            if (_schema.Length != other._schema.Length) return false;

            for (int i = 0; i < _schema.Length; i++)
            {
                if (_schema[i] != other._schema[i])
                    return false;
            }
            return true;
        }
        override public bool Equals(object? obj) => obj is SchemaKey o && Equals(o);

    }

    private readonly ConcurrentDictionary<SchemaKey, Dictionary<string, int>> _schemaDictCache = new();
    internal Dictionary<string, int> computeSchemaDict(string[] schema)
    {
        // Case-insensitive to match PascalCase schema with camelCase ctor params
        var dict = new Dictionary<string, int>(schema.Length, StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < schema.Length; i++)
            dict[schema[i]] = i;
        return dict;
    }
    public Dictionary<string, int> GetSchemaDict(string[] schema)  // Control it here
    {

        var key = new SchemaKey(schema);

        return _schemaDictCache.GetOrAdd(key, _ =>
            computeSchemaDict(schema));
    }
    private MemberMaterializationPlan(MemberSetter[] members)
    {
        Members = members;
    }


    public Action<T, object?[]> GetSchemaAction(string[] schema)
    {
        var key = new SchemaKey(schema);

        return _schemaMappingCache.GetOrAdd(key, _ => BuildSchemaAction(schema));
    }

    private Action<T, object?[]> BuildSchemaAction(string[] schema)
    {
        var schemaDict = GetSchemaDict(schema);

        // Build a compact mapping: for each matched member, store (valueIndex, setter)
        // Allocate once per schema key (acceptable since _schemaMappingCache caches actions).
        var tmp = new List<(int valueIndex, Action<T, object?> setter)>(Members.Length);
        foreach (var member in Members)
        {
            if (schemaDict.TryGetValue(member.Name, out var idx))
                tmp.Add((idx, member.Set));
        }

        var mapping = tmp.ToArray(); // compact, contiguous

        // Returned action does no allocations and no dictionary lookups
        return (obj, values) =>
        {
            // Use a simple for loop for best JIT inlining
            for (int i = 0; i < mapping.Length; i++)
            {
                var pair = mapping[i];
                var idx = pair.valueIndex;
                if ((uint)idx < (uint)values.Length)
                    pair.setter(obj, values[idx]);
            }
        };
    }
    public static MemberMaterializationPlan<T> Build(
        CultureInfo? culture = null,
        bool allowThousandsSeparators = true,
        string[]? dateTimeFormats = null)
    {
        var actualCulture = culture ?? CultureInfo.InvariantCulture;
        var actualFormats = dateTimeFormats ?? Array.Empty<string>();

        var type = typeof(T);
        var props = type.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                        .Where(p => p.CanWrite || p.SetMethod != null);
        var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        var members = new List<MemberSetter>(32);

        foreach (var p in props)
        {
            // Skip compiler-generated backing fields (they'll be handled via properties)
            if (p.Name.Contains("BackingField")) continue;

            var ord = GetOrder(p);
            members.Add(new MemberSetter(p.Name, ord, CompileSetterForProperty(p, actualCulture, allowThousandsSeparators, actualFormats)));
        }
        foreach (var f in fields)
        {
            // Skip backing fields for properties (avoid duplicates)
            if (f.Name.Contains("BackingField")) continue;
            var ord = GetOrder(f);
            members.Add(new MemberSetter(f.Name, ord, CompileSetterForField(f, actualCulture, allowThousandsSeparators, actualFormats)));
        }

        // Sort: members with [Order] attribute come first (sorted by OrderIndex),
        // then members without Order (-1) preserve original declaration order
        members.Sort((a, b) =>
        {
            // Both have Order: sort by OrderIndex
            if (a.OrderIndex >= 0 && b.OrderIndex >= 0)
                return a.OrderIndex.CompareTo(b.OrderIndex);
            // Only 'a' has Order: 'a' comes first
            if (a.OrderIndex >= 0)
                return -1;
            // Only 'b' has Order: 'b' comes first
            if (b.OrderIndex >= 0)
                return 1;
            // Neither has Order: preserve original order (stable sort)
            return 0;
        });

        return new MemberMaterializationPlan<T>(members.ToArray())
        {
            Culture = culture ?? CultureInfo.InvariantCulture,
            AllowThousandsSeparators = allowThousandsSeparators,
            DateTimeFormats = dateTimeFormats ?? Array.Empty<string>()
        };
    }

    private static int GetOrder(MemberInfo m)
    {
        var attr = (OrderAttribute?)Attribute.GetCustomAttribute(m, typeof(OrderAttribute), inherit: false);
        return attr?.Order ?? -1;
    }

    private static Action<T, object?> CompileSetterForProperty(PropertyInfo p,
        CultureInfo culture,
        bool allowThousands,
        string[] dateTimeFormats)
    {
        var obj = Expression.Parameter(typeof(T), "obj");
        var val = Expression.Parameter(typeof(object), "val");

        var targetType = p.PropertyType;
        var assignValue = BuildConvertExpression(
        val,
        targetType,
        culture,
        allowThousands,
        dateTimeFormats);

        var body = Expression.Assign(Expression.Property(obj, p), assignValue);
        return Expression.Lambda<Action<T, object?>>(body, obj, val).Compile();
    }

    private static Action<T, object?> CompileSetterForField(
        FieldInfo f,
        CultureInfo culture,
        bool allowThousands,
        string[] dateTimeFormats)
    {
        var obj = Expression.Parameter(typeof(T), "obj");
        var val = Expression.Parameter(typeof(object), "val");

        var targetType = f.FieldType;
        var assignValue = BuildConvertExpression(
            val,
            targetType,
            culture,
            allowThousands,
            dateTimeFormats);

        var body = Expression.Assign(Expression.Field(obj, f), assignValue);
        return Expression.Lambda<Action<T, object?>>(body, obj, val).Compile();
    }

    // Minimal conversion bridge: handles null/defaults and direct cast/unbox
    // Extend ConvertObject if you need string->primitive parsing.
    private static Expression BuildConvertExpression(ParameterExpression input,
    Type targetType,
    CultureInfo culture,
    bool allowThousands,
    string[] dateTimeFormats)
    {
        // If reference or nullable<T>, allow null straight through
        var underlyingNullable = Nullable.GetUnderlyingType(targetType);
        if (!targetType.IsValueType || underlyingNullable != null)
        {
            var tgt = underlyingNullable ?? targetType;
            return Expression.Convert(
                Expression.Call(
                    typeof(MemberMaterializationPlan<T>).GetMethod(nameof(ConvertObject), BindingFlags.NonPublic | BindingFlags.Static)!,
                    input,
                    Expression.Constant(targetType, typeof(Type)),
                    Expression.Constant(culture, typeof(CultureInfo)),
                    Expression.Constant(allowThousands, typeof(bool)),
                    Expression.Constant(dateTimeFormats, typeof(string[]))
                ),
                targetType);
        }

        // Non-nullable value type: null -> default(T), else cast/unbox
        var isNull = Expression.Equal(input, Expression.Constant(null));
        var onNull = Expression.Default(targetType);
        var onVal = Expression.Convert(
            Expression.Call(
                typeof(MemberMaterializationPlan<T>).GetMethod(nameof(ConvertObject), BindingFlags.NonPublic | BindingFlags.Static)!,
                input,
                Expression.Constant(targetType, typeof(Type)),
                Expression.Constant(culture, typeof(CultureInfo)),
                Expression.Constant(allowThousands, typeof(bool)),
                Expression.Constant(dateTimeFormats, typeof(string[]))
                ),
                targetType);
        return Expression.Condition(isNull, onNull, onVal);
    }

    // Central conversion hook (fast path: already-typed -> return as-is)
    private static object? ConvertObject(
        object? value,
        Type targetType,
        CultureInfo culture,
        bool allowThousandsSeparators = true,
        string[]? dateTimeFormats = null)
    {
        if (value is null) return null;

        var vType = value.GetType();
        if (targetType.IsAssignableFrom(vType))
            return value;

        // Handle Nullable<T>
        var nullable = Nullable.GetUnderlyingType(targetType);
        if (nullable != null)
            targetType = nullable;

        // String -> primitive conversions
        if (value is string s)
        {
            // Trim whitespace (common in CSV files)
            s = s.Trim();

            if (string.IsNullOrEmpty(s))
            {
                // If original targetType was Nullable<T>, return null
                if (nullable != null)
                    return null;

                // Otherwise return default for value types, null for reference types
                return targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
            }

            // Integer types
            var intStyles = NumberStyles.Integer | (allowThousandsSeparators ? NumberStyles.AllowThousands : 0);

            if (targetType == typeof(int) && int.TryParse(s, intStyles, culture, out var i))
                return i;
            if (targetType == typeof(long) && long.TryParse(s, intStyles, culture, out var l))
                return l;
            if (targetType == typeof(short) && short.TryParse(s, intStyles, culture, out var sh))
                return sh;
            if (targetType == typeof(byte) && byte.TryParse(s, intStyles, culture, out var by))
                return by;

            // Floating-point types
            var floatStyles = NumberStyles.Float | NumberStyles.AllowThousands;

            if (targetType == typeof(decimal) && decimal.TryParse(s, floatStyles, culture, out var m))
                return m;
            if (targetType == typeof(double) && double.TryParse(s, floatStyles, culture, out var d))
                return d;
            if (targetType == typeof(float) && float.TryParse(s, floatStyles, culture, out var f))
                return f;

            // DateTime (with explicit formats if provided)
            if (targetType == typeof(DateTime))
            {
                if (dateTimeFormats != null && dateTimeFormats.Length > 0)
                {
                    if (DateTime.TryParseExact(s, dateTimeFormats, culture, DateTimeStyles.None, out var dtExact))
                        return dtExact;
                }

                // Fallback to lenient parsing
                if (DateTime.TryParse(s, culture, DateTimeStyles.None, out var dt))
                    return dt;
            }

            if (targetType == typeof(DateTimeOffset))
            {
                if (dateTimeFormats != null && dateTimeFormats.Length > 0)
                {
                    if (DateTimeOffset.TryParseExact(s, dateTimeFormats, culture, DateTimeStyles.None, out var dtoExact))
                        return dtoExact;
                }

                if (DateTimeOffset.TryParse(s, culture, DateTimeStyles.None, out var dto))
                    return dto;
            }

            if (targetType == typeof(TimeSpan) && TimeSpan.TryParse(s, culture, out var ts))
                return ts;

            // Culture-insensitive types
            // Boolean (support true/false AND 1/0 - common CSV convention)
            if (targetType == typeof(bool))
            {
                if (bool.TryParse(s, out var b))
                    return b;
                // Common CSV convention: "1" = true, "0" = false
                if (s == "1") return true;
                if (s == "0") return false;
            }
            if (targetType == typeof(Guid) && Guid.TryParse(s, out var g))
                return g;
            if (targetType == typeof(char) && char.TryParse(s, out var c))
                return c;

            // Enum support
            if (targetType.IsEnum)
            {
                if (Enum.TryParse(targetType, s, ignoreCase: true, out var enumVal))
                    return enumVal;
            }
        }

        // Last resort: Convert.ChangeType for compatible conversions (boxed numerics, etc.)
        try
        {
            return Convert.ChangeType(value, targetType, culture);
        }
        catch (Exception ex)
        {
            // Conversion failed;  throw 
            throw new FormatException($"Cannot convert value '{value}' (type: {value.GetType().Name}) to {targetType.Name}", ex);
        }
    }

}


