using System.Text.RegularExpressions;
using System.Text;
using System;

namespace iCode.Extensions
{

    public static class IEnumerableExtensions
    {
        /// <summary>
        /// Merge two ordered enumerables into a single ordered enumerable.
        /// </summary>
        /// <typeparam name="T">The type of elements in the enumerables.</typeparam>
        /// <param name="first">The first ordered enumerable.</param>
        /// <param name="second">The second ordered enumerable.</param>
        /// <param name="isFirstParamInferiorOrEqualToSecond">A function that determines if an element from the first enumerable is less than or equal to an element from the second enumerable.</param>
        /// <returns>An ordered enumerable that combines the elements from both input enumerables.</returns>
        /// <remarks>
        /// This method merges two ordered enumerables into a single ordered enumerable. It compares elements from each enumerable using the provided comparison function and yields the elements in the correct order.
        /// 
        /// The method handles the following cases:
        /// - If one of the enumerables is empty, the elements from the other enumerable are yielded.
        /// - If both enumerables are empty, an empty enumerable is returned.
        /// - If elements from both enumerables are available, they are compared using the provided function and the smaller element is yielded first.
        /// - Once one of the enumerables is exhausted, the remaining elements from the other enumerable are yielded.
        /// </remarks>
        public static IEnumerable<T> MergeOrdered<T>(this IEnumerable<T> first, IEnumerable<T> second, Func<T, T, bool> isFirstLessThanOrEqualToSecond)
        {
            using var enum1 = first?.GetEnumerator();
            using var enum2 = second?.GetEnumerator();

            bool hasNext1 = enum1?.MoveNext() ?? false;
            bool hasNext2 = enum2?.MoveNext() ?? false;

            while (hasNext1 && hasNext2)
            {
                if (isFirstLessThanOrEqualToSecond(enum1.Current, enum2.Current))
                {
                    yield return enum1.Current;
                    hasNext1 = enum1.MoveNext();
                }
                else
                {
                    yield return enum2.Current;
                    hasNext2 = enum2.MoveNext();
                }
            }

            var remainingEnumerator = hasNext1 ? enum1 : hasNext2 ? enum2 : null;

            while (remainingEnumerator?.MoveNext() ?? false)
            {
                yield return remainingEnumerator.Current;
            }
        }


        public static IEnumerable<T> Take<T>(this IEnumerable<T> sequence, int start, int count)
                => sequence.Take(new Range(start, start + count - 1));
      
        public static IEnumerable<(int category, T item)> Cases<T>(this IEnumerable<T> items, params Func<T, bool>[] filters)
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

        public static IEnumerable<(int category, T item)> Cases<T>(this IEnumerable<T> items, params Func<T, int, bool>[] filters)
        { 
            int idx = 0;
            foreach (var item in items)
            {
                for (int category = 0; category < filters.Length; category++)
                {
                    if (filters[category](item, idx))
                    {
                        yield return (category, item);
                    }
                }
                idx++;
            }
        }

        public static void ForEachCase<T>(this IEnumerable<(int category, T item)> sequence, params Action<T>[] actions)
        => sequence.Where(x => x.category < actions.Length).ForEach(x => actions[x.category](x.item));


        public static void ForEachCase<T>(this IEnumerable<(int category, T item)> sequence, params Action<T, int>[] actions)
        => sequence.Where(x => x.category < actions.Length).ForEach((x, index) => actions[x.category](x.item, index));
  
                                   

        public static IEnumerable<(int category, TResult item)> SelectCase<T, TResult>(this IEnumerable<(int category, T item)> sequence, params Func<T, TResult>[] selectors)
        => sequence.Where(x => x.category < selectors.Length).Select(x => (x.category , selectors[x.category](x.item)));



        public static IEnumerable<(int category, TResult item)> SelectCase<T, TResult>(this IEnumerable<(int category, T item)> sequence, params Func<T, int, TResult>[] selectors)
        => sequence.Where(x => x.category < selectors.Length).Select((x, idx) => (x.category, selectors[x.category](x.item, idx)));

        public static IEnumerable<(int category, T item)> DoCase<T>(this IEnumerable<(int category, T item)> sequence, params Action[] actions)
        {
            return sequence.Where(x => x.category < actions.Length)
            .Select(x =>
            {
                actions[x.category]();
                return x;
            });
        }
        public static IEnumerable<(int category, T item)> DoCase<T>(this IEnumerable<(int category, T item)> sequence, params Action<T>[] actions)
        {
            return sequence.Where(x => x.category < actions.Length)
            .Select(x =>
            {
                actions[x.category](x.item);
                return x;
            });
        }

        public static IEnumerable<(int category, T item)> DoCase<T>(this IEnumerable<(int category, T item)> sequence, params Action<T, int>[] actions)
        {
            return sequence.Where(x => x.category < actions.Length)
             .Select((x, idx) =>
             {
                 actions[x.category](x.item, idx);  
                 return x;
             });
        }

        public static IEnumerable<T> Until<T>(this IEnumerable<T> items, Func<T, bool> stopCondition)
        {
            foreach (var item in items)
            {
                yield return item;

                if (stopCondition != null && stopCondition(item))
                {
                    break;
                }
            }
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


        //public static IEnumerable<T> Update<T>(this IEnumerable<T> items, Action<T, int> action)
        //    => items.Select((x, idx) =>
        //    {
        //        action(x, idx);
        //        return x;
        //    });

        //public static IEnumerable<T> Update<T>(this IEnumerable<T> items, Action<T> action)
        //    => items.Select(x =>
        //    {
        //        action(x);
        //        return x;
        //    });           


        public static void ForEach<T>(this IEnumerable<T> sequence, Action<T, int> action)
        {
            int index = 0;
            foreach (var item in sequence) action(item, index++);
                           
        }

        public static void ForEach<T>(this IEnumerable<T> sequence, Action<T> action)
        {
            foreach (var item in sequence) action(item);       
        }

        public static IEnumerable<T> Do<T>(this IEnumerable<T> items, Action<T,int> action)
        {
            return items.Select((x,idx) =>
            {
                action(x,idx);
                return x;
            });
        }
        public static IEnumerable<T> Do<T>(this IEnumerable<T> items , Action<T> action)
        {
            return items.Select(x =>
                        {
                            action(x);
                            return x;
                        });
        }
        public static IEnumerable<T> Do<T>(this IEnumerable<T> items, Action action)
        {
            return items.Select(x =>
            {
                action();
                return x;
            });
        }

        public static T? Cumul<T>(this IEnumerable<T> sequence, Func<T?, T, T> cumulate)
        {
            T? cumul = sequence.IsNullOrEmpty() ? default : sequence.First();

            sequence.Skip(1).ForEach(x => cumul = cumulate(cumul, x));

            return cumul;
        }

        public static TResult? Cumul<T, TResult>(this IEnumerable<T> sequence,  Func<TResult?, T, TResult> cumulate, TResult? initial)
        {
            TResult? cumul = initial;

            sequence.ForEach(x => cumul = cumulate(cumul, x));

            return cumul;
        }

        public static StringBuilder BuildString(this IEnumerable<string> items, StringBuilder str = null, string separator = ", ", string before = "{", string after = "}" )
        {
            if( str is null) str = new StringBuilder();

            if (!before.IsNullOrEmpty())
                str.Append(before);

            items.ForEach((x,idx) => { if(idx > 0) str.Append(separator); str.Append(x); });

            if(!after.IsNullOrEmpty())
                str.Append(after);
            return str;
        }
        public static StringBuilder BuildString(this IEnumerable<string> items, string separator = ", ", string before = "{", string after = "}")
        {
            return items.BuildString(new StringBuilder(), separator, before, after);
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


    //public static class spyextensions
    //{
    //    public static T Spy<T>(this T item, string tag)
    //    {
    //        Console.WriteLine(tag.IsNullOrEmpty() ? $"{item}" : $"{tag}: {item}");
    //        return item;
    //    }

    //    public static string Spy(this string item, string tag)
    //    {
    //        Console.WriteLine(tag.IsNullOrEmpty() ? $"'{item}'" : $"{tag}: '{item}'");
    //        return item;
    //    }
    //}

    public static class iLogger_IEnumerableExtension
    {
        public static IEnumerable<T> Spy<T>(this IEnumerable<T> items, string tag, string separator = "|", string before = "{", string after = "}")
        {
            return items.Spy(tag, x => x is string ? $"'{x}'" : x?.ToString()??"null", separator, before, after);    
        }

        //public static IEnumerable<T> Spy<T>(this IEnumerable<T> items, string tag, Func<T, string> customDispay, string separator = "|", string before = "{", string after = "}")
        //{

        //    StringBuilder str = new StringBuilder();

        //    if (!tag.IsNullOrEmpty())
        //        str.Append(tag).Append(" :");

        //    str.Append(before);
        //    int i = 0;
        //    foreach (var item in items)
        //    {
        //        if (i != 0) str.Append(separator);
        //        str.Append(customDispay(item));
        //        yield return item;

        //        i++;
        //    }
        //    str.Append(after);
        //    Console.WriteLine(str.ToString());
        //}

        public static IEnumerable<T> Spy<T>(this IEnumerable<T> items, string tag, Func<T, string> customDispay, string separator = "|", string before = "{", string after = "}")
        {
            Console.WriteLine();
            if (!tag.IsNullOrEmpty())
                Console.Write(tag); Console.Write(" :");

            Console.Write(before);
            int i = 0;
            foreach (var item in items)
            {
                if (i != 0) Console.Write(separator);
                Console.Write(customDispay(item));
                yield return item;

                i++;
            }
            Console.Write(after);
        }
    }
    public static class consoleMapper
    {
        public static void Display(this IEnumerable<string> items, string tag = "Displaying", string separator = "| ", string before = "{", string after = "}")
        {
            Console.WriteLine();
            if (!tag.IsNullOrEmpty())
                Console.Write(tag); Console.Write(" :");

            Console.Write(before);
            int i = 0;
            foreach (var item in items)
            {
                if (i != 0) Console.Write(separator);
                Console.Write(item);
                i++;
            }
            Console.Write(after);
        }
    }
}
   

