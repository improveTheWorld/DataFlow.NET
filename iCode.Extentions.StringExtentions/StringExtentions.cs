namespace iCode.Extentions.StringExtentions
{
    public static class StringExtentions
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

    }
}


