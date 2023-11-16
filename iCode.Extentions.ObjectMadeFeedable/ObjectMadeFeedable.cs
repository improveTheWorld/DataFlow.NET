using iCode.Extentions.IEnumerableExtentions;
using iCode.Framework.AutomizedFeeding;

namespace iCode.Extentions.ObjectMadeFeedable
{
    public static class ObjectMadeFeedable
    {

        

        public static object NewWithParams(this Type objectType, params object[] parameters)
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
                return NewThenFeed(objectType, parameters);
            }
        }

        public static object NewThenFeed(this Type objectType, params object[] parameters)
        {
            if (objectType == null)
            {
                throw new ArgumentNullException(nameof(objectType));
            }
            else if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            return objectType.GetConstructor(Array.Empty<Type>()).Invoke(Array.Empty<object>()).Feed((parameters));
        }

      
        public static object NewThenFeed(this Type newObjectType, string[] feedingDictionary, params object[] parameters)
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

            return newObjectType.GetConstructor(Array.Empty<Type>()).Invoke(Array.Empty<object>()).Feed(feedingDictionary, parameters);
        }

       

        public static T Feed<T>(this T objectToFeed, params object[] parameters)
        {
            return Feeder.Feed(objectToFeed,  parameters);
        }

        public static T Feed<T>(this T objectToFeed, string[] feedingDictionary, params object[] parameters)
        {
            return Feeder.Feed<T>( feedingDictionary, objectToFeed, parameters);
        }
    }
}
