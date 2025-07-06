using DataFlow.Extensions;
using DataFlow.Framework.AutomizedFeeding;

namespace DataFlow.Framework
{     
    public static class NEW
    {
        public static T? GetNew<T>(params string[] parameters)
        {
            try
            {
                return (T)NEW_ExactConstructor(typeof(T), parameters);
            }
            catch 
            {
                return (T)NEW_InternalOrder(typeof(T), parameters);
            }
                     
        }
        public static T? GetNew<T>(string[] schema, params object[] parameters)
        {
           return (T) NEW_GivenSchema(typeof(T), schema, parameters);  
        }

        static object NEW_ExactConstructor(Type objectType, params object[] parameters)
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
            
            return objectType.GetConstructor(parameters.Select(x => x.GetType()).ToArray()).Invoke(parameters);
            
        }

        public static object NEW_InternalOrder(Type objectType, params object[] parameters)
        {
            if (objectType == null)
            {
                throw new ArgumentNullException(nameof(objectType));
            }
            else if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }

            return Feeder.Feed_InternalOrder(objectType.GetConstructor(Array.Empty<Type>()).Invoke(Array.Empty<object>()), parameters);
        }


        static object NEW_GivenSchema(Type newObjectType, string[] schema, params object[] parameters)
        {
            if (newObjectType == null)
            {
                throw new ArgumentNullException(nameof(newObjectType));
            }
            else if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters));
            }
            else if (schema == null)
            {
                throw new ArgumentNullException(nameof(schema));
            }

            object instance;
            try
            {
                instance = Activator.CreateInstance(newObjectType);
            }
            catch
            {
                instance = newObjectType.GetConstructor(Array.Empty<Type>()).Invoke(Array.Empty<object>());
            }

            
            return  Feeder.Feed_WithSchema(instance, Feeder.GetSchemaDictionary(schema), parameters);
            
        }
    }
}

