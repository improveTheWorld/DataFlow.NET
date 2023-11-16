
namespace iCode.Extentions.IEnumerableExtentions
{
    public static class IEnumerableExtentions
    {
        public static IEnumerable<T> Range<T>(this IEnumerable<T> list, Func<int /*index*/, bool> SelectElementIndex)
        {
            int index = 0;

            foreach (T element in list)
            {
                if (SelectElementIndex(index++))
                {
                    yield return element;
                }

            }

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
             
      
        public static void ForEach<T>(this IEnumerable<T> sequence, Action<T> action)
        {
            foreach (var item in sequence)
                action(item);
        }

        public static void ForEach<T>(this IEnumerable<T> sequence, Action<T,int> action)
        {
            int index = 0;
            foreach (var item in sequence)
                action(item, index++);
        }

        public static T? Cumulate<T>(this IEnumerable<T> sequence, Func<T, T, T> cumulate)
        {
            T cumul = sequence.IsNullOrEmpty() ? default : sequence.First();

            sequence.Skip(1).ForEach(x => cumul = cumulate(cumul, x));
           
            return cumul;
        }

        static bool IsNullOrEmpty<T>(this IEnumerable<T> sequence)
        {
            if (sequence == null)
            {
                return true;
            }

            var collection = sequence as ICollection<T>;
            if (collection != null)
            {
                return collection.Count == 0;
            }

            return !sequence.Any();
        }
    }
}
