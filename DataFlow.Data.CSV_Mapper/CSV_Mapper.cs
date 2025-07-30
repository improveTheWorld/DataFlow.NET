using DataFlow.Extensions;

namespace DataFlow.Data;

public static class CSV_Mapper
{

    public static string csv<T>(this T csvRecord, string separator = ";")// where T : struct
                         => typeof(T)
                           .GetFields()?
                           .Select(f => f?.GetValue(csvRecord)?.ToString() ?? String.Empty)
                           .Aggregate((a, b) => a + separator + b) ?? String.Empty;

    public static string csv<T>(string separator = ";") where T : struct
                         => typeof(T)
                           .GetFields()?
                           .Select(f => f?.Name ?? String.Empty)
                           .Aggregate((a, b) => a + separator + b) ?? String.Empty;
}