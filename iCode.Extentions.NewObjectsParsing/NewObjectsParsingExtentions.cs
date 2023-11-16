using iCode.Extentions.ObjectMadeFeedable;
using iCode.Extentions.IEnumerableExtentions;
using iCode.Extentions.StringExtentions;
using iCode.Extentions.StreamReaderExtentions;


namespace iCode.Extentions.NewObjectsParsing
{
    public static class NewObjectsParsingExtentions
    {
        public static T? AsObject<T>(this string CSV_Line, string CSV_Seperator, string[]? parsingOrder = null)
        {

            object[] paramters = CSV_Line.Split(CSV_Seperator, StringSplitOptions.TrimEntries).Select(x => x.Convert()).ToArray();
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
    }  
}