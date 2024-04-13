using System;
using System.Reflection;
using iCode.Extensions;


namespace iCode.Framework.AutomizedFeeding
{
    //todo : convert this to internalwithout breaking Unit tests
    public static class Feeder
    {
        public static Dictionary<string, int> GetSchemaDictionary(string[] schema)
        {
            return new Dictionary<string, int>(schema.Select((fieldName, index) => new KeyValuePair<string, int>(fieldName, index)));
        }

        static void SetAttributeValue(MemberInfo attribute, object objectToFill, object valuesStore)
        {
            Type infoType = (attribute is FieldInfo) ? ((FieldInfo)attribute).FieldType : ((PropertyInfo)attribute).PropertyType;

            object value = Convert.ChangeType(valuesStore, infoType);

            ((dynamic)Convert.ChangeType(attribute, attribute.GetType())).SetValue(objectToFill, value);
        }


        public static T Feed_InternalOrder<T>( T objectToFill,  params object[] parameters)
        {
            if (objectToFill is IWithIntenalSchema)
            {
                Feed_InternalSchema((IWithIntenalSchema)objectToFill,parameters);
                return objectToFill;
            }
            else //suppose FeedOredere ( definition of attribute with [order] tag
            {
                return Feed_OrderAttributes(objectToFill, parameters);
            }

        }
       
        public static T Feed_WithSchema<T>(T objectToFill, Dictionary<string, int> schemaDictionay, params object[] parameters)
        {
            if(objectToFill == null)
            {
                throw new ArgumentNullException($"objectToFill should be instantiated before feeding");
            }

            var objectType = objectToFill.GetType();
            var attributes = ((MemberInfo[])objectType.GetProperties()).Concat(objectType.GetFields());
            int valueIndex = 0;

            attributes.Where(attribute => schemaDictionay.TryGetValue(attribute.Name, out valueIndex))
                .ForEach(x => SetAttributeValue(x, objectToFill, parameters[valueIndex]));

            return objectToFill;
        }

        public static IWithIntenalSchema Feed_InternalSchema(IWithIntenalSchema objectToFill, params object[] valuesStore) 
        {
            return Feed_WithSchema(objectToFill, objectToFill.GetSchema(), valuesStore);
        }
                       
            

        public static T Feed_OrderAttributes<T>(T objectToFill, params object[] valuesStore)
        {
            if (objectToFill == null)
            {
                throw new ArgumentNullException($"objectToFill should be instantiated before feeding");
            }

            if (valuesStore.IsNullOrEmpty())
            {
                throw new ArgumentNullException(nameof(valuesStore));
            }
            


            var objectType = objectToFill.GetType();
            var attributes = ((MemberInfo[])objectType.GetProperties()).Concat(objectType.GetFields());

            var orderedAttributes = from attribute in attributes
                                    where Attribute.IsDefined(attribute, typeof(OrderAttribute))
                                    orderby ((OrderAttribute)attribute
                                            .GetCustomAttributes(typeof(OrderAttribute), false)
                                            .Single()).Order
                                    select attribute;

            valuesStore.Zip(orderedAttributes).ForEach((att)=>  SetAttributeValue(att.Second, objectToFill, att.First));

           
            return objectToFill;
        }

    }
      
}
