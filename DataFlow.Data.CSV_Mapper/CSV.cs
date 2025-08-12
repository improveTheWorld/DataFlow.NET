using System.Reflection;

namespace DataFlow.Data;

public static class CSV
{
    /// <summary>
    /// Gets the CSV representation of a single record instance by reflecting its public properties.
    /// </summary>
    public static string csv<T>(this T csvRecord, string separator = ";")
    {
        if (csvRecord == null) return string.Empty;

        return string.Join(separator, typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p?.GetValue(csvRecord)?.ToString() ?? string.Empty));
    }

    /// <summary>
    /// Gets the CSV header line from the type's public properties.
    /// </summary>
    public static string csv<T>(string separator = ";")
    {
        return string.Join(separator, typeof(T)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p?.Name ?? string.Empty));
    }
}
