using System.Text.RegularExpressions;
using System.Text;
namespace iCode.Extensions
{
    public static class IEnumerableExtensions
    {
        public static IEnumerable<T> CombineOrdered<T>(this IEnumerable<T> ordered1, IEnumerable<T> ordered2, Func<T, T, bool> isFirstParamInferiorOrEqualToSecond)
        {

            IEnumerator<T>? enum1 = null;
            IEnumerator<T>? enum2 = null;

            if (ordered1 != null) enum1 = ordered1.GetEnumerator();
            if (ordered2 != null) enum2 = ordered2.GetEnumerator();


            bool notEmpty1 = false;
            bool notEmpty2 = false;

            if (enum1 != null) notEmpty1 = enum1.MoveNext();
            if (enum2 != null) notEmpty2 = enum2.MoveNext();

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

        public static IEnumerable<T> Take<T>(this IEnumerable<T> sequence, int start, int count)
        {
            return sequence.Take(new Range(start, start + count - 1));
        }


        public static IEnumerable<(int category, T item)> Classify<T>(this IEnumerable<T> items, params Func<T, bool>[] filters)
        {
            foreach (var item in items)
            {
                for (int category = 0; category < filters.Length; category++)
                {
                    if (filters[category](item))
                    {
                        yield return (category, item);
                    }
                }
            }
        }

        public static void ForEachByClassification<T>(this IEnumerable<(int category, T item)> sequence, params Action<T>[] actions)
        {
            sequence.Where(x => x.category < actions.Length).
                    ForEach(x => actions[x.category](x.item));
        }

        public static void ForEachByClassification<T>(this IEnumerable<(int category, T item)> sequence, params Action<T, int>[] actions)
        {
            sequence.Where(x => x.category < actions.Length).
                    ForEach((x, index) => actions[x.category](x.item, index));
        }

        public static IEnumerable<TResult?> SelectByClassification<T, TResult>(this IEnumerable<(int category, T item)> sequence, params Func<T, TResult>[] selectors)
        {
            return sequence.Where(x => x.category < selectors.Length)
                            .Select(x => selectors[x.category](x.item));

        }

        public static IEnumerable<object?> SelectByClassification<T>(this IEnumerable<(int category, T item)> sequence, params Func<T, int, object>[] selectors)
        {
            return sequence.Where(x => x.category < selectors.Length)
                           .Select((x, idx) => selectors[x.category](x.item, idx));
        }


        public static IEnumerable<object?> SelectByClassification<T>(this IEnumerable<(int category, T item)> sequence, params Func<T, object>[] selectors)
        {
            return sequence.Where(x => x.category < selectors.Length)
                            .Select(x => selectors[x.category](x.item));
        }

        public static IEnumerable<T> Until<T>(this IEnumerable<T> items, Func<T, int, bool> stopCondition)
        {

            int index = 0;

            foreach (var item in items)
            {
                yield return item;

                if (stopCondition != null && stopCondition(item, index++))
                {
                    break;
                }
            }
        }

        public static IEnumerable<T> Until<T>(this IEnumerable<T> items, int lastItemIdx)
        {

            int index = 0;

            foreach (var item in items)
            {
                yield return item;

                if (lastItemIdx == index++) break;
            }
        }

        public static void ForEach<T>(this IEnumerable<T> sequence, Action<T, int> action)
        {
            int index = 0;
            foreach (var item in sequence)
                action(item, index++);
        }

        public static void ForEach<T>(this IEnumerable<T> sequence, Action<T> action)
        {
            foreach (var item in sequence)
                action(item);
        }

       
        public static T? Cumul<T>(this IEnumerable<T> sequence, Func<T?, T, T> cumulate)
        {
            T? cumul = sequence.IsNullOrEmpty() ? default : sequence.First();

            sequence.Skip(1).ForEach(x => cumul = cumulate(cumul, x));

            return cumul;
        }

        public static TResult? Cumul<T, TResult>(this IEnumerable<T> sequence, TResult? initial, Func<TResult?, T, TResult> cumulate)
        {
            TResult? cumul = initial;

            sequence.ForEach(x => cumul = cumulate(cumul, x));

            return cumul;
        }

        public static T? Sum<T>(this IEnumerable<T> sequence)
        {
            if (sequence == null || !sequence.Any())
                return default(T?);

            dynamic sum = default(T);
            foreach (var item in sequence)
            {
                sum += (dynamic)item;
            }

            return sum;
        }

        public static bool IsNullOrEmpty<T>(this IEnumerable<T> sequence)
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

    static public class  DictionnaryExtensions
    {
        public static bool AddOrUpdate<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue value)
        {
            if (dict.ContainsKey(key))
            {
                dict[key] = value;
                return false;
            }
            else
            {
                dict.Add(key, value);
                return true;
            }
        }

        public static TValue? GetOrNull<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key) where TKey : notnull
        {
            if (dict.ContainsKey(key))
            {
                return dict[key];
            }
            else
            {
                return default;
            }
        }
    }

   
}
