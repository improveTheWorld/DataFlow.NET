using iCode.Framework;
using iCode.Extensions;

namespace iCode.Data.StringMapper
{

    public static class StringMapper
    {
        public static object parse(this string objectValue)
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
        public static T csv<T>(this string line, string separator = ";") where T : struct
                => NEW.Get<T>(line
                .Split(separator, StringSplitOptions.TrimEntries)
                .Where(param => !param.IsNullOrEmpty())
                .Select(x => x.parse())
                .ToArray());
        }
}
