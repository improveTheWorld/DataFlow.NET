using iCode.Extensions;


namespace iCode.Data
{
    public static class CSV_Mapper
    {

        public static string csv<T>(T csvRecord, string separator = ";") where T : struct
                             => typeof(T)
                               .GetFields()?
                               .Select(f => f?.GetValue(csvRecord)?.ToString() ?? String.Empty)
                               .Cumul((a, b) => a + separator + b) ?? String.Empty;


        public static string csv<T>(string separator = ";") where T : struct
                             => typeof(T)
                               .GetFields()?
                               .Select(f => f?.Name ?? String.Empty)
                               .Cumul((a, b) => a + separator + b) ?? String.Empty;
    }       
}