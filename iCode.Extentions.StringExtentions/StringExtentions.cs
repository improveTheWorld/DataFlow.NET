namespace iCode.Extentions.StringExtentions
{
    public static class StringExtentions
    {
        public static object ParseValue(this string value)
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

            foreach (string acceptedEnd in acceptedEnds)
            {
                if (value.EndsWith(acceptedEnd))
                    return true;
            }
            return false;

        }

        public static bool StartsWithAny(this string value, IEnumerable<string> acceptedStarts)
        {

            foreach (string acceptedStart in acceptedStarts)
            {
                if (value.StartsWith(acceptedStart))
                    return true;
            }
            return false;

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


