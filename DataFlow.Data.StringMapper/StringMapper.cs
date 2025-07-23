using DataFlow.Framework;

namespace DataFlow.Data.StringMapper;


public static class StringMapper
{
    public static object GetObject(string objectValue)
    {
        bool boolValue;
        Int32 intValue;
        Int64 bigintValue;
        double doubleValue;
        DateTime dateValue;

        if (bool.TryParse(objectValue, out boolValue))
            return boolValue;
        else if (Int32.TryParse(objectValue, out intValue))
            return intValue;
        else if (Int64.TryParse(objectValue, out bigintValue))
            return bigintValue;
        else if (double.TryParse(objectValue, out doubleValue))
            return doubleValue;
        else if (DateTime.TryParse(objectValue, out dateValue))
            return dateValue;
        else return objectValue;

    }
    public static T? GetCSV<T>(this string line, string[] schema, string separator = ";")// where T : struct
            => NEW.GetNew<T>(schema,
                             line.Split(separator, StringSplitOptions.TrimEntries)
                            .Select(x => GetObject(x))
                            .ToArray());
                            
    }
