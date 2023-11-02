using iCode.Log;
namespace iCode.Extentions.IEnumerableExtentions
{
    public static class IEnumerableExtentions
    {
        public static IEnumerable<Y> Transform<T, Y>(this IEnumerable<T> list, Func<T, bool> condition, Func<T, Y> transform)
        {

            list.Trace($"-------------------- Begin Transform" );
            foreach (T element in list)
            {
                if (condition(element))
                {
                    var ret = transform(element);
                    list.Trace($" element transformed to --  {ret} --");
                    yield return ret;
                }
            }

            list.Trace($"Transform End-----------------");
        }

        public static IEnumerable<T> Range<T>(this IEnumerable<T> list, Func<int /*index*/, bool> SelectElementIndex)
        {
            int index = 0;
            list.Trace($"-------------------- Begin Range");

            foreach (T element in list)
            {
                if (SelectElementIndex(index++))
                {
                    list.Trace($"Range {index} accepted --  {element}--");
                    yield return element;
                }
                else
                {
                    list.Trace($"Range {index} Removed --  {element}--");
                }
            }

            list.Trace($"Range End-----------------");
        }

       

        public static IEnumerable<T> CombineOrdered<T>(this IEnumerable<T> ordered1, IEnumerable<T> ordered2, Func<T, T, bool> isFirstParamInferiorOrEqualToSecond)
        {

            IEnumerator<T>? enum1 = null;
            IEnumerator<T>? enum2 = null;

            if(ordered1 != null)    enum1 = ordered1.GetEnumerator();
            if(ordered2 != null)    enum2 = ordered2.GetEnumerator();


            bool notEmpty1 = false;
            bool notEmpty2 = false;

            if (enum1 != null)   notEmpty1 = enum1.MoveNext();
            if (enum2 != null)   notEmpty2 = enum2.MoveNext();

            while (notEmpty1 && notEmpty2)
            {
                if (isFirstParamInferiorOrEqualToSecond(enum1.Current, enum2.Current))
                {
                    yield return enum1.Current;
                    notEmpty1 = enum1.MoveNext();
                }
                else
                {
                    yield return enum2.Current;
                    notEmpty2 = enum2.MoveNext();
                }
            }

            IEnumerator<T> remainingData = null;

            if (notEmpty1)
            {
                remainingData = enum1;
            }
            else if (notEmpty2)
            {
                remainingData = enum2;
            }

            if (remainingData != null)
            {
                do
                {
                    yield return remainingData.Current;
                }
                while (remainingData.MoveNext());
            }

        }



        public static void ApplyForeach<T>(this IEnumerable<T> list, IEnumerable<( Func<T, bool> condition, Action<T> action)> ConditionalActions)
        {
            foreach (T element in list)
            {
                foreach(var conditionAction in ConditionalActions)
                {
                    if(conditionAction.condition(element))
                    {
                        conditionAction.action(element);
                    }
                }                        
            }
        }


        public static void ApplyForeach<T>(this IEnumerable<T> list, Func<T, bool> condition, Action<T> action)
        {
            ApplyForeach<T>(list, new (Func<T, bool> , Action<T> )[] { (condition, action) });
        }

        public static void ApplyForeach<T>(this IEnumerable<T> list, Action<T> action)
        {
            ApplyForeach<T>(list, _ => true, action);
        }

        public static void ApplyForeach<T>(this IEnumerable<T> list, IEnumerable<Action<T>> actions)
        {
            foreach(T element in list)
            {
                foreach (Action<T> action in actions)
                {
                    action(element);
                }
            }           

        }



        public static IEnumerable<Y> Transform<X, Y>(this IEnumerable<X> list, Func<X, Y> transform)
        {
            return Transform<X, Y>(list, _ => true, transform);
        }

        public static int IndexOf<T>(this IEnumerable<T> list,T instance)
        {
            int index = 0;
            foreach(var element in list )
            {
                if (element.Equals(instance))
                {
                    return index;
                }
                index++;
            }
            return -1;
        }

        public static void forEachExceptLast<T>(this IEnumerable<T> list, Action<T> apply, out T? lastElement)
        {
            var enumerator = list.GetEnumerator();

            if(!enumerator.MoveNext())
            {
                lastElement = default;
                return; 
            }
            else
            {
                lastElement = enumerator.Current;
                while(enumerator.MoveNext() && lastElement != null)
                {
                    apply(lastElement);
                    lastElement = enumerator.Current;
                }
            }
        }
         
        public static string Serialize<T>(this IEnumerable<T> list, string separator= ";" , Func<T,string>SerializeElement = null)
        {
            if(SerializeElement== null)
            {
                SerializeElement = (x=>x.ToString());
            }
            string retValue = "";
            
            list.forEachExceptLast<T>(x => retValue += SerializeElement(x)+separator, out T lastElement);

            return retValue+ SerializeElement(lastElement);
        }
    }

}
