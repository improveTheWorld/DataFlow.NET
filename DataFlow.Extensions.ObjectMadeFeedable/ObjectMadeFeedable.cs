using DataFlow.Framework.AutomizedFeeding;

namespace DataFlow.Extensions
{
    public static class NEW
    {  
        public static T? Get<T>(object[] paramters, string[]? parsingOrder = null)
        {
            object retValue;

            if (parsingOrder != null)
            {
                retValue = NEW.ThenFeed(typeof(T),parsingOrder, paramters);
            }
            else
            {
                retValue = NEW.WithParams(typeof(T),paramters);
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
        public static object WithParams(Type objectType, params object[] parameters)
        {

            //: try:
        //1. construuctor accepting types in given objects order,
        //2. try non-param contructor and feed ( with internal order if feedable or in order if orderable)
        //3. throw error if fail
        //
            if (objectType == null)
            {
                throw new ArgumentNullException(nameof(objectType));
            }
            else if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            try
            {
                return objectType.GetConstructor(parameters.Select(x => x.GetType()).ToArray()).Invoke(parameters);
            }
            catch(Exception ex)
            {
                return ThenFeed(objectType, parameters);
            }
        }

        public static object ThenFeed(Type objectType, params object[] parameters)
        {
            if (objectType == null)
            {
                throw new ArgumentNullException(nameof(objectType));
            }
            else if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            return Feeder.Feed(objectType.GetConstructor(Array.Empty<Type>()).Invoke(Array.Empty<object>()), parameters);
        }

      
        public static object ThenFeed(Type newObjectType, string[] feedingDictionary, params object[] parameters)
        {
            if (newObjectType == null)
            {
                throw new ArgumentNullException(nameof(newObjectType));
            }
            else if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }
            else if(feedingDictionary == null)
            {
                throw new ArgumentNullException(nameof(feedingDictionary));  
            }

            return Feeder.Feed(newObjectType.GetConstructor(Array.Empty<Type>()).Invoke(Array.Empty<object>()), feedingDictionary, parameters);
        }
    }
}
