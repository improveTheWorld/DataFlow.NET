using System;
using System.Text;
using System;
using System.Security.AccessControl;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Data.SqlTypes;
using System.Linq;
using System.Diagnostics.Metrics;
using static DataFlow.Extensions.Spy_IEnumerableExtension;
using System.Collections.Generic;

namespace DataFlow.Extensions
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
      
       
        public static IEnumerable<T> Until<T>(this IEnumerable<T> items, Func< bool> stopCondition)
        {
            if (stopCondition == null)
                throw new ArgumentNullException(nameof(stopCondition)); // Ensure there's a stop condition

            foreach (var item in items)
            {
                yield return item;

                if (stopCondition())
                {
                    break;
                }
            }
        }
       
        public static IEnumerable<T> Until<T>(this IEnumerable<T> items, Func<T, bool> stopCondition)
        {

            if (stopCondition == null)
                throw new ArgumentNullException(nameof(stopCondition)); // Ensure there's a stop condition

            foreach (var item in items)
            {
                yield return item;

                if (stopCondition(item))
                {
                    break;
                }
            }
        }

        public static IEnumerable<T> Until<T>(this IEnumerable<T> items, Func<T, int, bool> stopCondition)
        {
            if (stopCondition == null)
                throw new ArgumentNullException(nameof(stopCondition)); // Ensure there's a stop condition

            int index = 0;

            foreach (var item in items)
            {
                yield return item;

                if (stopCondition(item, index++))
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

        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> items, Action<T,int> action)
        {
            return items.Select((x,idx) =>
            {
                action(x,idx);
                return x;
            });
        }
        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> items , Action<T> action)
        {
            return items.Select(x =>
                        {
                            action(x);
                            return x;
                        });
        }

       
        public static void Do<T>(this IEnumerable<T> items, Action action)
        {
            foreach( var item in items)
            {
                action();
            }
        }

        public static void Do<T>(this IEnumerable<T> items)
        {
            foreach (var item in items) ;
        }


        public static T? Cumul<T>(this IEnumerable<T> sequence, Func<T?, T, T> cumulate)
        {
            T? cumul = sequence.IsNullOrEmpty() ? default : sequence.First();

            sequence.Skip(1).ForEach(x => cumul = cumulate(cumul, x)).Do();

            return cumul;
        }

        public static TResult? Cumul<T, TResult>(this IEnumerable<T> sequence,  Func<TResult?, T, TResult> cumulate, TResult? initial)
        {
            TResult? cumul = initial;

            sequence.ForEach(x => cumul = cumulate(cumul, x)).Do();

            return cumul;
        }

        public static StringBuilder BuildString(this IEnumerable<string> items, StringBuilder str = null, string separator = ", ", string before = "{", string after = "}" )
        {
            if( str is null) str = new StringBuilder();

            if (!before.IsNullOrEmpty())
                str.Append(before);

            items.ForEach((x,idx) => { if(idx > 0) str.Append(separator); str.Append(x); }).Do();

            if(!after.IsNullOrEmpty())
                str.Append(after);
            return str;
        }
        public static StringBuilder BuildString(this IEnumerable<string> items, string separator = ", ", string before = "{", string after = "}")
        {
            return items.BuildString(new StringBuilder(), separator, before, after);
        }

        public static T? Sum<T>(this IEnumerable<T> items)  
        {
            T? sum = default;
            items.ForEach(item => sum += (dynamic)item).Do();
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


    public static class IEnumerable_DeepLoopExtensions
    {

        //--------------------------------------  IEnumerable<IEnumerable<T>> 
        //public static IEnumerable<IEnumerable<T>> ForEach<T>(this IEnumerable<IEnumerable<T>> items, Action<T> action)
        //=> items.ForEach(row => row.ForEach(item => action(item)));
        //public static IEnumerable<IEnumerable<T>> ForEach<T>(this IEnumerable<IEnumerable<T>> items, Action<T, int> action)
        //=> items.ForEach((row, x) => row.ForEach(item => action(item, x)));

        //public static IEnumerable<IEnumerable<T>> ForEach<T>(this IEnumerable<IEnumerable<T>> items, Action<T, int, int> action)
        //=> items.ForEach((row, x) => row.ForEach((item, y) => action(item, x, y)));

        //public static IEnumerable<IEnumerable<R>> Select<T, R>(this IEnumerable<IEnumerable<T>> items, Func<T, R> map)
        //=> items.Select(row => row.Select(item => map(item)));
        //public static IEnumerable<IEnumerable<R>> Select<T, R>(this IEnumerable<IEnumerable<T>> items, Func<T, int, R> map)
        //=> items.Select((row, x) => row.Select(item => map(item, x)));

        //public static IEnumerable<IEnumerable<R>> Select<T, R>(this IEnumerable<IEnumerable<T>> items, Func<T, int, int, R> map)
        //=> items.Select((row, x) => row.Select((item, y) => map(item, x, y)));

        public static IEnumerable<T> Flat<T>(this IEnumerable<IEnumerable<T>> items)
        => items.SelectMany(x => x);
        public static IEnumerable<T> Flat<T>(this IEnumerable<IEnumerable<T>> items, T endOfEnumerable)
        => items.SelectMany(x => x.Append(endOfEnumerable));
        public static IEnumerable<R> Flat<T,R>(this IEnumerable<IEnumerable<T>> items, Func<IEnumerable<T>,R> group)
        => items.Select(x => group(x));



        //public static IEnumerable<IEnumerable<T>> Where<T>(this IEnumerable<IEnumerable<T>> items, Func<T, bool> predicate)
        //{
        //    foreach (var row  in items)
        //    {
        //        yield return row.Where(predicate);               
        //    }
        //}
    }



    public static class IEnumerable_CasesExtension
    {
        //------------------------------------------ Cases
        public static IEnumerable<(int categoryIndex, T item)> Cases<C,T>(this IEnumerable<(C category, T item)> items, params C[] categories) where C: notnull
        {
            var Dict = new Dictionary<C, int> (categories.Select((category, idx) => new KeyValuePair<C, int>(category, idx)));
            return items.Select(x => (Dict.ContainsKey(x.category) ? Dict[x.category] : Dict.Count,  x.item));
        }


        //public static IEnumerable<IEnumerable<(int categoryIndex,  T item)>> Cases<C, T>(this IEnumerable<IEnumerable<(C category, T item)>> items, params C[] categories) where C : notnull
        //{
        //    var Dict = new Dictionary<C, int>(categories.Select((category, idx) => new KeyValuePair<C, int>(category, idx)));
        //    return items.Select(x => (Dict.ContainsKey(x.category) ? Dict[x.category] : Dict.Count,  x.item));
        //}

        static int getFilterIndex<T>(this Func<T, bool>[] filters, T item)
        {

            int CategoryIndex = 0;
            foreach( var predicate in filters)
            {
                if (predicate(item))
                    return CategoryIndex;
                else 
                    CategoryIndex++;
            }

            return CategoryIndex;
        }

        public static IEnumerable<(int category, T item)> Cases<T>(this IEnumerable<T> items, params Func<T, bool>[] filters)
        => items.Select(item => (filters.getFilterIndex(item), item));

       


        //----------------------------------------------- SelectCase
     
        public static IEnumerable<(int category, T item, R? newItem)> SelectCase<T, R>(this IEnumerable<(int category, T item)> items, params Func<T, R>[] selectors)
        => items.Select(x => (x.category, x.item, (x.category < selectors.Length) ? selectors[x.category](x.item) :  default));

        public static IEnumerable<(int category, T,  R? item)> SelectCase<T, R>(this IEnumerable<(int category, T item)> items, params Func<T, int, R>[] selectors)
        => items.Select((x, idx) => (x.category,x.item, (x.category < selectors.Length) ?  selectors[x.category](x.item, idx) : default));

        //-----------------with newItem

        public static IEnumerable<(int category, T item,Y? newItem)> SelectCase<T, R,Y>(this IEnumerable<(int category, T item, R newItem)> items, params Func<R, Y>[] selectors)
       => items.Select(x => (x.category, x.item, (x.category < selectors.Length) ? selectors[x.category](x.newItem) : default));

        public static IEnumerable<(int category, T item, Y? newItem)> SelectCase<T, R, Y>(this IEnumerable<(int category, T item, R newItem)> items, params Func<R, int, Y>[] selectors)
        => items.Select((x, idx) => (x.category, x.item, (x.category < selectors.Length) ? selectors[x.category](x.newItem, idx) : default));

        //------------------------------------------- ForEachCase

        public static IEnumerable<(int category, T item)> ForEachCase<T>(this IEnumerable<(int category, T item)> items, params Action[] actions)
        => items.ForEach(x => { if (x.category < actions.Length) actions[x.category](); });

        public static IEnumerable<(int category, T item)> ForEachCase<T>(this IEnumerable<(int category, T item)> items, params Action<T>[] actions)
        => items.ForEach(x => { if (x.category < actions.Length) actions[x.category](x.item); });

        public static IEnumerable<(int category, T item)> ForEachCase<T>(this IEnumerable<(int category, T item)> items, params Action<T, int>[] actions)

        => items.ForEach((x, index) => { if (x.category < actions.Length) actions[x.category](x.item, index); });


        //-----------------with newItem
        public static IEnumerable<(int category, T item, R newItem)> ForEachCase<T,R>(this IEnumerable<(int category, T item, R newItem)> items, params Action[] actions)
        => items.ForEach(x => { if (x.category < actions.Length) actions[x.category](); });

        public static IEnumerable<(int category, T item, R newItem)> ForEachCase<T, R>(this IEnumerable<(int category, T item, R newItem)> items, params Action<R>[] actions)
        => items.ForEach(x => { if (x.category < actions.Length) actions[x.category](x.newItem); });

        public static IEnumerable<(int category, T item, R newItem)> ForEachCase<T, R>(this IEnumerable<(int category, T item, R newItem)> items, params Action<R, int>[] actions)
        => items.ForEach((x, index) => { if (x.category < actions.Length) actions[x.category](x.newItem, index); });



        //------------------------------------AllCases
        public static IEnumerable<T> UnCase<T>(this IEnumerable<(int category, T item)> items)
        => items.Select(x => x.item);

        //------------------------------------AllCases
        public static IEnumerable<T> UnCase<T,Y>(this IEnumerable<(int category, T item, Y newItem)> items)
        => items.Select(x => x.item);

        public static IEnumerable<R> AllCases<T, R>(this IEnumerable<(int category, T item, R newItem)> items, bool filter = true) where T : class
        => filter ? items.Select(x => x.newItem).Where(x => x is not null && !x.Equals(default)) : items.Select(x => x.newItem);

        public static IEnumerable<string> ToLines(this IEnumerable< string > slices, string separator)
        {
            string sum = "";
            foreach (var slice in slices)
            {
                if (slice != separator)
                    sum += slice;
                else
                {
                    yield return sum;
                    sum = "";
                }

            }
        }
        public static IEnumerable<string> AllCases(this IEnumerable<(int category, string item)> items, string separator)
        {
            string sum = "";
            foreach (var (_, item) in items)
            {
                if (item != separator)
                    sum += item;
                else
                {
                    yield return sum;
                    sum = "";
                }

            }
        }

    }




    //---------------------------------------------------IEnumerable<IEnumerable<T>>



    static public class IEnumerableIEnumerable_Cases_Extensions    
    {
        //------------------------------------------------------ Cases

        //public static IEnumerable<IEnumerable<(int categoryIndex,  T item)>> Cases<C, T>(this IEnumerable<IEnumerable<(C category, T item)>> items, params C[] categories) where C : notnull
        //{
        //    var Dict = new Dictionary<C, int>(categories.Select((category, idx) => new KeyValuePair<C, int>(category, idx)));
        //    return items.Select(x => (Dict.ContainsKey(x.category) ? Dict[x.category] : Dict.Count,  x.item));
        //}

        //static int getFilterIndex<T>(this Func<T, bool>[] filters, T item)
        //{
        //    int CategoryIndex = filters.Length;
        //    filters.Until((filter, idx) =>
        //    {
        //        if (filter(item))
        //        {
        //            CategoryIndex = idx;
        //            return true;
        //        }
        //        else return false;
        //    });
        //    return CategoryIndex;
        //}

        //public static IEnumerable<IEnumerable<(int category, T item)>> Cases<T>(this IEnumerable<IEnumerable<T>> items, params Func<T, bool>[] filters)
        //=> items.Select(item => (filters.getFilterIndex(item), item));



        ////--------------------------------SelectCase
     
        //public static IEnumerable<IEnumerable<(int category, R item)>> SelectCase<T, R>(this IEnumerable<IEnumerable<(int category, T item)>> items, params Func<T, R>[] selectors)
        //=> items.Select(x => (x.category, selectors[x.category](x.item)));

        //public static IEnumerable<IEnumerable<(int category, R item)>> SelectCase<T, R>(this IEnumerable<IEnumerable<(int category, T item)>> items, params Func<T, int, R>[] selectors)
        //=> items.Select((x, idx) => (x.category, selectors[x.category](x.item, idx)));



        ////--------------------------------ForEachCase
  
        //public static IEnumerable<IEnumerable<(int category, T item)>> ForEachCase<T>(this IEnumerable<IEnumerable<(int category, T item)>> items, params Action[] actions)
        //=> items.ForEach(x => { if (x.category < actions.Length) actions[x.category](); });

        //public static IEnumerable<IEnumerable<(int category, T item)>> ForEachCase<T>(this IEnumerable<IEnumerable<(int category, T item)>> items, params Action<T>[] actions)
        //=> items.ForEach(x => { if (x.category < actions.Length) actions[x.category](x.item); });

        //public static IEnumerable<IEnumerable<(int category, T item)>> ForEachCase<T>(this IEnumerable<IEnumerable<(int category, T item)>> items, params Action<T, int>[] actions)
        //=> items.ForEach((x, index) => { if (x.category < actions.Length) actions[x.category](x.item, index); });


        ////------------------------------------AllCases
        //public static IEnumerable<IEnumerable<T>> AllCases<T>(this IEnumerable<IEnumerable<(int category, T item)>> items)
        //=> items.Select(x => x.item);
      
    }



    public static class Spy_IEnumerableExtension
    {

        public const string BEFORE = "---------{\n";
        public const string AFTER = "\n-------}";
        public const string SEPARATOR = "\n";
        public static IEnumerable<string> Spy(this IEnumerable<string> items, string tag, bool timeStamp = false, string separator = SEPARATOR, string before = BEFORE, string after = AFTER )
        => items.Spy<string>(tag, x => x, timeStamp, separator, before, after);

        public static IEnumerable<T> Spy<T>(this IEnumerable<T> items, string tag, Func<T, string> customDispay, bool timeStamp = false, string separator = SEPARATOR, string before = BEFORE, string after = AFTER)
        {
            string startedAt = string.Empty;
            Stopwatch stopwatch = new();
            if ( timeStamp)
            {
                DateTime now = DateTime.Now;
                startedAt = $"[{now.Hour}:{now.Minute}:{now.Second}.{now.Millisecond}]";
                stopwatch = new Stopwatch();

                // Start the stopwatch
                stopwatch.Start();
            }
          
            Console.WriteLine(startedAt);
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
            if (timeStamp)
            {
                // Stop the stopwatch
                stopwatch.Stop();
                Console.Write($"[{stopwatch.Elapsed.TotalMilliseconds} ms]" );
            }
            
        }
    }

    public static class consoleMapper
    {
        
        public static void Display(this IEnumerable<string> items, string tag = "Displaying", string separator = SEPARATOR, string before = BEFORE, string after = AFTER)
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

    static public class DictionnaryExtensions
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
   

