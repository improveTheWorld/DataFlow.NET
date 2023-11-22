
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;

namespace iCode.Extensions.IEnumerableExtensions
{
    public static class IEnumerableExtensions
    {     
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

        public static IEnumerable<T> Take<T>(this IEnumerable<T> sequence, int start, int count)
        {
            return sequence.Take(new Range(start, start+count-1));
        }

        public static IEnumerable<(int,T)> Classify<T>(this IEnumerable<T> sequence, params Func<T, bool> []conditions)
        {
           foreach(var item in  sequence)
           {
                for (int idx = 0; idx < conditions.Length; idx++)
                {
                    if (conditions[idx](item))
                    {
                        yield return (idx, item);
                        break;
                    }
                }
           }
        }

        public static void ForEachByClassification<T>(this IEnumerable<(int,T)> sequence, params Action<T>[] actions)
        {
            sequence.ForEach(x => { if (x.Item1 < actions.Length) actions[x.Item1](x.Item2); });
        }

        public static IEnumerable<TResult> SelectByClassification<T, TResult>(this IEnumerable<(int, T)> sequence, params Func<T, TResult>[] selectors)
        {
            foreach (var x in sequence)
            {
                if(x.Item1 < selectors.Length) yield return selectors[x.Item1](x.Item2);
            }
        }
        public static IEnumerable<object> SelectByClassification<T>(this IEnumerable<(int, T)> sequence, params Func<T,object>[] selectors)
        {
            foreach (var x in sequence)
            {
                if (x.Item1 < selectors.Length) yield return selectors[x.Item1](x.Item2);
            }
        }

        public static void ForEachByClassification<T>(this IEnumerable<(int, T)> sequence, params Action<int, T >[] actions)
        {
            sequence.ForEach((x, idx) => { if (x.Item1 < actions.Length) actions[x.Item1](x.Item1, x.Item2); });
        }

        public static IEnumerable<TResult> SelectByClassification<T, TResult>(this IEnumerable<(int, T)> sequence, params Func<int,T, TResult>[] selectors)
        {
            foreach (var x in sequence)
            {
                if (x.Item1 < selectors.Length) yield return selectors[x.Item1](x.Item1, x.Item2);
            }
        }

        public static Dictionary<int, List<T>> ToLists<T>(this IEnumerable<(int, T)> sequence)
        {
            var lists = new Dictionary<int, List<T>>();

            foreach (var (index, item) in sequence)
            {
                if (!lists.ContainsKey(index))
                {
                    lists[index] = new List<T>();
                }

                lists[index].Add(item);
            }

            // Convert the dictionary values to an array of lists and return
            return lists;
        }


        public static IEnumerable<object> SelectByClassification<T>(this IEnumerable<(int, T)> sequence, params Func<int, T,  object>[] selectors)
        {
            foreach (var x in sequence)
            {
                if (x.Item1 < selectors.Length)  yield return selectors[x.Item1](x.Item1, x.Item2);
 
            }
        }

        public static void ForEach<T>(this IEnumerable<T> sequence, Action<T> action)
        {
            foreach (var item in sequence)
                action(item);
        }

        public static IEnumerable<TResult> SelectNonDefault<TSource, TResult>(this IEnumerable<TSource> source, Func<TSource, TResult?> selector)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (selector == null)
            {
                throw new ArgumentNullException(nameof(selector));
            }

            foreach (var item in source)
            {
                TResult? tmp = selector(item);
                if (!EqualityComparer<TResult?>.Default.Equals(tmp, default(TResult)))
                {
                    yield return tmp;
                }
            }
        }

        public static void ForEach<T>(this IEnumerable<T> sequence, Action<T,int> action)
        {
            int index = 0;
            foreach (var item in sequence)
                action(item, index++);
        }

        public static T? Cumul<T>(this IEnumerable<T> sequence, Func<T, T, T> cumulate)
        {
            T cumul = sequence.IsNullOrEmpty() ? default : sequence.First();

            sequence.Skip(1).ForEach(x => cumul = cumulate(cumul, x));
           
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


        public static T? Combine<T>(this IEnumerable<T> sequence, Func<T, T, T> cumulate)
        {
            T cumul = sequence.IsNullOrEmpty() ? default : sequence.First();

            sequence.Skip(1).ForEach(x => cumul = cumulate(cumul, x));

            return cumul;
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
}
