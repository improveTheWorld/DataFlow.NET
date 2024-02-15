using iCode.Extensions;


namespace iCode.Extensions
{
    public static class NewObjectsParsingExtensions
    {
        public static T? AsObject<T>(this object[] paramters,  string[]? parsingOrder = null)
        {
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