

namespace DataFlow;

public static class CsvWriter
{
    // Added: cache for property arrays per type for performance
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Type, System.Reflection.PropertyInfo[]> _propCache
        = new();

    private static System.Reflection.PropertyInfo[] GetProps<T>() =>
        _propCache.GetOrAdd(typeof(T),
            t => t.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance));

    /// <summary>
    /// RFC 4180 style quoting: quote if field contains separator, quote, CR, LF, or leading/trailing space
    /// </summary>
    private static string QuoteIfNeeded(string value, char separator)
    {
        if (value == null) return "";
        bool mustQuote =
            value.Contains(separator)
            || value.Contains('"')
            || value.Contains('\r')
            || value.Contains('\n')
            || (value.Length > 0 && (value[0] == ' ' || value[^1] == ' '));

        if (!mustQuote) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    /// <summary>
    /// Gets the CSV representation of a single record instance by reflecting its public properties..
    /// </summary>
    public static string ToCsvLine<T>(this T csvRecord, string separator = ",")
    {
        if (csvRecord == null) return string.Empty;
        var props = GetProps<T>();
        var sepChar = separator.Length == 1 ? separator[0] : ',';
        return string.Join(separator, props
            .Select(p => QuoteIfNeeded(p.GetValue(csvRecord)?.ToString() ?? string.Empty, sepChar)));
    }

    /// <summary>
    /// Gets the CSV header line from the type's public properties.
    /// </summary>
    public static string CsvHeader<T>(string separator = ",")
    {
        var props = GetProps<T>();
        var sepChar = separator.Length == 1 ? separator[0] : ',';
        return string.Join(separator, props
            .Select(p => QuoteIfNeeded(p.Name ?? string.Empty, sepChar)));
    }
}
