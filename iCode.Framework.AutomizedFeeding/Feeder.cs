using System;
using System.Reflection;
using iCode.Extentions.IEnumerableExtentions;


namespace iCode.Framework.AutomizedFeeding
{

    public static class Feeder
    {
        static Dictionary<string, int> AsFeedDictionary(string[] feedingOrder)
        {
            return new Dictionary<string, int>(feedingOrder.Select((fieldName, index) => new KeyValuePair<string, int>(fieldName, index)));
        }

        static void FeedAttribute(MemberInfo attribute, object objectToFeed, object food)
        {
            Type infoType = (attribute is FieldInfo) ? ((FieldInfo)attribute).FieldType : ((PropertyInfo)attribute).PropertyType;

            object value = Convert.ChangeType(food, infoType);

            ((dynamic)Convert.ChangeType(attribute, attribute.GetType())).SetValue(objectToFeed, value);
        }


        public static T Feed<T>(string[] feedingOrder, T objectToFeed, params object[] parameters)
        {
            return FeedInGivenOrder<T>(objectToFeed, AsFeedDictionary(feedingOrder), parameters);
        }

        public static T Feed<T>(T objectToFeed, params object[] parameters)
        {
            return FeedWithInternalDefinedOrder<T>(objectToFeed, parameters);
        }

        static T FeedWithInternalDefinedOrder<T>( T objectToFeed,  params object[] parameters)
        {
            if (objectToFeed is IFeedingInternalOrder)
            {
                FeedFeedableObject((IFeedingInternalOrder)objectToFeed,parameters);
                return objectToFeed;
            }
            else //suppose FeedOredere ( definition of attribute with [order] tag
            {
                return FeedOrderedObject(objectToFeed, parameters);
            }

        }
       
        static T FeedInGivenOrder<T>(T objectToFeed, Dictionary<string, int> feedingDictionary, params object[] food)
        {
            if(objectToFeed == null)
            {
                throw new ArgumentNullException($"objectToFeed should be instantiated before feeding");
            }

            var objectType = objectToFeed.GetType();
            var attributes = ((MemberInfo[])objectType.GetProperties()).Concat(objectType.GetFields());

            foreach (var attribute in attributes)
            {
                string name = attribute.Name;
                if (feedingDictionary.ContainsKey(name))
                {
                    FeedAttribute(attribute, objectToFeed, food[feedingDictionary[name]]);
                }
            }
            return objectToFeed;
        }

        static IFeedingInternalOrder FeedFeedableObject(IFeedingInternalOrder objectToFeed, params object[] food) 
        {
            return FeedInGivenOrder(objectToFeed, objectToFeed.GetFeedingDictionary(), food);
        }
                       
            

        static T FeedOrderedObject<T>(T objectToFeed, params object[] food)
        {
            if (objectToFeed == null)
            {
                throw new ArgumentNullException($"objectToFeed should be instantiated before feeding");
            }

            var objectType = objectToFeed.GetType();
            var attributes = ((MemberInfo[])objectType.GetProperties()).Concat(objectType.GetFields());

            var orderedAttributes = from attribute in attributes
                                    where Attribute.IsDefined(attribute, typeof(OrderAttribute))
                                    orderby ((OrderAttribute)attribute
                                            .GetCustomAttributes(typeof(OrderAttribute), false)
                                            .Single()).Order
                                    select attribute;
            int index = 0;

            foreach (var attribute in orderedAttributes)
            {
                if(index< food.Length)
                {
                    FeedAttribute(attribute, objectToFeed, food[index++]);
                }
                else
                {
                    break;
                }
            }

            if(index == 0)
            {
                throw new ArgumentException($" {typeof(T)} is not a ordered Type.");
            }
            return objectToFeed;
        }

    }
      
}
