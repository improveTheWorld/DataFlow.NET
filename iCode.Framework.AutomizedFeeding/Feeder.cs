using System;
using System.Reflection;
using iCode.Extensions;


namespace iCode.Framework.AutomizedFeeding
{
    //todo : convert this to internalwithout breaking Unit tests
    public static class Feeder
    {
        static Dictionary<string, int> AsFeedDictionary(string[] fillingOrder)
        {
            return new Dictionary<string, int>(fillingOrder.Select((fieldName, index) => new KeyValuePair<string, int>(fieldName, index)));
        }

        static void FillAttribute(MemberInfo attribute, object objectToFill, object valuesStore)
        {
            Type infoType = (attribute is FieldInfo) ? ((FieldInfo)attribute).FieldType : ((PropertyInfo)attribute).PropertyType;

            object value = Convert.ChangeType(valuesStore, infoType);

            ((dynamic)Convert.ChangeType(attribute, attribute.GetType())).SetValue(objectToFill, value);
        }


        public static T Feed<T>(string[] fillingOrder, T objectToFill, params object[] parameters)
        {
            return FillInGivenOrder<T>(objectToFill, AsFeedDictionary(fillingOrder), parameters);
        }

        public static T Feed<T>(T objectToFill, params object[] parameters)
        {
            return FillWithInternalDefinedOrder<T>(objectToFill, parameters);
        }

        static T FillWithInternalDefinedOrder<T>( T objectToFill,  params object[] parameters)
        {
            if (objectToFill is IFeedingInternalOrder)
            {
                FillFeedableObject((IFeedingInternalOrder)objectToFill,parameters);
                return objectToFill;
            }
            else //suppose FeedOredere ( definition of attribute with [order] tag
            {
                return FillOrderedObject(objectToFill, parameters);
            }

        }
       
        static T FillInGivenOrder<T>(T objectToFill, Dictionary<string, int> CorrespendanceTable, params object[] ValueStore)
        {
            if(objectToFill == null)
            {
                throw new ArgumentNullException($"objectToFill should be instantiated before feeding");
            }

            var objectType = objectToFill.GetType();
            var attributes = ((MemberInfo[])objectType.GetProperties()).Concat(objectType.GetFields());
            int valueIndex = 0;
            attributes.Select(attribute => CorrespendanceTable.TryGetValue(attribute.Name, out valueIndex) ? new { attribute, valueIndex } : null)
                      .Where(x=>x!= null)
                      .ForEach(x => FillAttribute(x.attribute, objectToFill, ValueStore[x.valueIndex]));

            return objectToFill;
        }

        static IFeedingInternalOrder FillFeedableObject(IFeedingInternalOrder objectToFill, params object[] valuesStore) 
        {
            return FillInGivenOrder(objectToFill, objectToFill.GetFeedingDictionary(), valuesStore);
        }
                       
            

        static T FillOrderedObject<T>(T objectToFill, params object[] valuesStore)
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

            valuesStore.Zip(orderedAttributes).ForEach((att)=>  FillAttribute(att.Second, objectToFill, att.First));

           
            return objectToFill;
        }

    }
      
}
