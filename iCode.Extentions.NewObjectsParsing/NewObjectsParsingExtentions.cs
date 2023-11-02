using iCode.Extentions.ObjectMadeFeedable;
using iCode.Extentions.IEnumerableExtentions;
using iCode.Extentions.StringExtentions;
using iCode.Extentions.StreamReaderExtentions;


namespace iCode.Extentions.NewObjectsParsing
{
    public static class NewObjectsParsingExtentions
    {
        public static T? newObject<T>(this string lineToParse, string separator, string[]? parsingOrder = null)
        {

            object[] paramters = lineToParse.Split(separator, StringSplitOptions.TrimEntries).Transform(x => x.ParseValue()).ToArray();
            object retValue;

            if(parsingOrder!=null)
            {
                retValue = typeof(T).NewThenFeed(parsingOrder, paramters);
            }
            else
            {
                retValue = typeof(T).NewWithParams(paramters);
            }
            
            if (retValue != null)
            {
                return (T)retValue;
            }
            else
            {
                return default;
            }
        }

        public static IEnumerable<T> newObjectsAndEnumerate<T>(this IEnumerable<string> list, string separator)
        {
            return list.Transform(line => line.newObject<T>(separator));
        }

        public static IEnumerable<T> AsParsedObjectsEnumerable<T>(this StreamReader input, string separator)
        {
            return input.AsLinesEnumerable().newObjectsAndEnumerate<T>(separator);
        }
    }  
}