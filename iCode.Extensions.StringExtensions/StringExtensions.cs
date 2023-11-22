using System.Globalization;

namespace iCode.Extensions.StringExtentions
{
    public static class StringExtensions
    {
        /// <summary>
        /// Try to Convert a string to a typed value ( in order bool,  int, Int64, double, dataTime ) 
        /// </summary>
        /// <param name="value"></param>
        /// <returns> Return the first succefull convertion result. If none , return the input string as it</returns>
        public static object Convert(this string value)
        {
            bool boolValue;
            Int32 intValue;
            Int64 bigintValue;
            double doubleValue;
            DateTime dateValue;

            if (bool.TryParse(value, out boolValue))
                return boolValue;
            else if (Int32.TryParse(value, out intValue))
                return intValue;
            else if (Int64.TryParse(value, out bigintValue))
                return bigintValue;
            else if (double.TryParse(value, out doubleValue))
                return doubleValue;
            else if (DateTime.TryParse(value, out dateValue))
                return dateValue;
            else return value;

        }

        public static bool EndsWithAny(this string value, IEnumerable<string> acceptedEnds)
        {
            return acceptedEnds?.Select(possibleEnd => value.EndsWith(possibleEnd))?.FirstOrDefault(x => x) ?? false;
        }

        public static bool StartsWithAny(this string value, IEnumerable<string> acceptedStarts)
        {
            return acceptedStarts?.Select(possibleStart => value.StartsWith(possibleStart))?.FirstOrDefault(x => x) ?? false;
        }

        public static bool StartsWithAny(this string value, params string[] acceptedStarts)
        {
            return StartsWithAny(value,acceptedStarts.AsEnumerable());
        }

        public static bool EndsWithAny(this string value, params string[] acceptedEnds)
        {
            return EndsWithAny(value, acceptedEnds.AsEnumerable());
        }
        public static bool ContainsAny(this string value, IEnumerable<string> any)
        {
            return any?.Select(possible => value.Contains(possible))?.FirstOrDefault(x => x) ?? false;
        }

        public static string  ReplaceAt(this string value,int index, int length, string toInsert)
        {
            return value.Substring(0, index + 1) + toInsert + value.Substring(index + length);
        }
    }
}


