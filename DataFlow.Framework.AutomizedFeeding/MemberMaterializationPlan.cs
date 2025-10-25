
// FeedPlan: builds once per T and caches compiled setters and mapping metadata
using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
namespace DataFlow.Framework;

public interface IHasSchema
{
    Dictionary<string, int> GetSchema();
}
internal static class MemberMaterializationPlanner
{
    private readonly record struct PlanCacheKey(
    Type TargetType,
    bool CaseInsensitive,
    string CultureName,
    bool AllowThousands,
    string DateTimeFormatsHash)
    {
        public static PlanCacheKey Create<T>(
            bool caseInsensitive,
            CultureInfo culture,
            bool allowThousands,
            string[] dateTimeFormats)
        {
            var formatsHash = dateTimeFormats.Length == 0
                ? string.Empty
                : string.Join("|", dateTimeFormats);

            return new PlanCacheKey(
                typeof(T),
                caseInsensitive,
                culture.Name,
                allowThousands,
                formatsHash);
        }
    }

    private static readonly ConcurrentDictionary<PlanCacheKey, object> Cache = new();

    public static MemberMaterializationPlan<T> Get<T>(
        bool caseInsensitiveHeaders = true,
        CultureInfo? culture = null,
        bool allowThousandsSeparators = true,
        string[]? dateTimeFormats = null)
    {
        var key = PlanCacheKey.Create<T>(
            caseInsensitiveHeaders,
            culture ?? CultureInfo.InvariantCulture,
            allowThousandsSeparators,
            dateTimeFormats ?? Array.Empty<string>());

        return (MemberMaterializationPlan<T>)Cache.GetOrAdd(
            key,
            _ => MemberMaterializationPlan<T>.Build(
                caseInsensitiveHeaders,
                culture,
                allowThousandsSeparators,
                dateTimeFormats));
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
    public readonly StringComparer NameComparer;

    private MemberMaterializationPlan(MemberSetter[] members, StringComparer comparer)
    {
        Members = members;
        NameComparer = comparer;
    }

    public static MemberMaterializationPlan<T> Build(
        bool caseInsensitiveHeaders,
        CultureInfo? culture = null,
        bool allowThousandsSeparators = true,
        string[]? dateTimeFormats = null)
    {
        var actualCulture = culture ?? CultureInfo.InvariantCulture;
        var actualFormats = dateTimeFormats ?? Array.Empty<string>();
        var comparer = caseInsensitiveHeaders ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

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

        return new MemberMaterializationPlan<T>(members.ToArray(), comparer)
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
            if (targetType == typeof(bool) && bool.TryParse(s, out var b))
                return b;
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
            throw new FormatException($"Cannot convert value '{value}' (type: {value.GetType().Name}) to {targetType.Name}",ex);
        }
    }

}


