
using System;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using System.Security.AccessControl;
using System.Text;
using System.Threading.Channels;
using static DataFlow.Extensions.Spy_IAsyncEnumerableExtension;

namespace DataFlow.Extensions
{

    public static class ParallelAsyncQueryExtensions
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
        //public static ParallelAsyncQuery<T> MergeOrdered<T>(
        //this ParallelAsyncQuery<T> first,
        //ParallelAsyncQuery<T> second,
        //Func<T, T, bool> isFirstLessThanOrEqualToSecond)
        //{
        //    return first._MergeOrdered(second, isFirstLessThanOrEqualToSecond).AsParallel();
        //}

        //private static async IAsyncEnumerable<T> _MergeOrdered<T>(
        //this ParallelAsyncQuery<T> first,
        //ParallelAsyncQuery<T> second,
        //Func<T, T, bool> isFirstLessThanOrEqualToSecond)
        //{
        //    await using var enum1 = first?.GetAsyncEnumerator();
        //    await using var enum2 = second?.GetAsyncEnumerator();

        //    bool hasNext1 = enum1 != null && await enum1.MoveNextAsync();
        //    bool hasNext2 = enum2 != null && await enum2.MoveNextAsync();

        //    while (hasNext1 && hasNext2)
        //    {
        //        if (isFirstLessThanOrEqualToSecond(enum1!.Current, enum2!.Current))
        //        {
        //            yield return enum1.Current;
        //            hasNext1 = await enum1.MoveNextAsync();
        //        }
        //        else
        //        {
        //            yield return enum2.Current;
        //            hasNext2 = await enum2.MoveNextAsync();
        //        }
        //    }

        //    var remainingEnumerator = hasNext1 ? enum1 : hasNext2 ? enum2 : null;

        //    while (remainingEnumerator != null && await remainingEnumerator.MoveNextAsync())
        //    {
        //        yield return remainingEnumerator.Current;
        //    }
        //}



        public static ParallelAsyncQuery<T> ForEach<T>(this ParallelAsyncQuery<T> items, Action<T, int> action)
        {
            return items.Select((x, idx) =>
            {
                action(x, idx);
                return x;
            });
        }
        public static ParallelAsyncQuery<T> ForEach<T>(this ParallelAsyncQuery<T> items, Action<T> action)
        {
            return items.Select(x =>
            {
                action(x);
                return x;
            });
        }


        public static async Task Do<T>(this ParallelAsyncQuery<T> items, Action action)
        {
            items.ForEach(_ => action());
        }

        public static async Task Do<T>(this ParallelAsyncQuery<T> items)
        {
            items.ForEach(_ => {; });
        }



        public static async Task<StringBuilder> BuildString(this ParallelAsyncQuery<string> items, StringBuilder str = null, string separator = ", ", string before = "{", string after = "}")
        {
            if (str is null) str = new StringBuilder();

            if (!before.IsNullOrEmpty())
                str.Append(before);

            await items.ForEach((x, idx) => { if (idx > 0) str.Append(separator); str.Append(x); }).Do();

            if (!after.IsNullOrEmpty())
                str.Append(after);
            return str;
        }
        public static async Task<StringBuilder> BuildString(this ParallelAsyncQuery<string> items, string separator = ", ", string before = "{", string after = "}")
        {
            return await items.BuildString(new StringBuilder(), separator, before, after);
        }

        public static async Task<T?> Sum<T>(this ParallelAsyncQuery<T> items)
        {
            T? sum = default;
            await items.ForEach(item => sum += (dynamic)item).Do();
            return sum;
        }



        //------------------------------------------- FIRST

        /// <summary>
        /// Returns the first element of a sequence.
        /// </summary>
        public static async Task<T> First<T>(this ParallelAsyncQuery<T> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            await using var enumerator = source.GetAsyncEnumerator();
            if (await enumerator.MoveNextAsync())
                return enumerator.Current;

            throw new InvalidOperationException("Sequence contains no elements");
        }

      

        //------------------------------------------- FIRST OR DEFAULT

        /// <summary>
        /// Returns the first element of a sequence, or a default value if no element is found.
        /// </summary>
        public static async Task<T?> FirstOrDefault<T>(this ParallelAsyncQuery<T> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            await using var enumerator = source.GetAsyncEnumerator();
            if (await enumerator.MoveNextAsync())
                return enumerator.Current;

            return default(T);
        }

        ///// <summary>
        ///// Returns the first element that satisfies a condition or a default value if no such element is found.
        ///// </summary>
        //public static async Task<T?> FirstOrDefault<T>(this ParallelAsyncQuery<T> source, Func<T, bool> predicate)
        //{
        //    if (source == null)
        //        throw new ArgumentNullException(nameof(source));
        //    if (predicate == null)
        //        throw new ArgumentNullException(nameof(predicate));

        //    await foreach (var item in source)
        //    {
        //        if (predicate(item))
        //            return item;
        //    }

        //    return default(T);
        //}

    //    /// <summary>
    //    /// Async predicate version of FirstOrDefault
    //    /// </summary>
    //    public static async Task<T?> FirstOrDefault<T>(this ParallelAsyncQuery<T> source, Func<T, Task<bool>> predicate)
    //    {
    //        if (source == null)
    //            throw new ArgumentNullException(nameof(source));
    //        if (predicate == null)
    //            throw new ArgumentNullException(nameof(predicate));

    //        await foreach (var item in source)
    //        {
    //            if (await predicate(item))
    //                return item;
    //        }

    //        return default(T);
    //    }


    }


    public static class ParallelAsyncQuery_DeepLoopExtensions
    {

        //--------------------------------------  ParallelAsyncQuery<ParallelAsyncQuery<T>> 
        //public static ParallelAsyncQuery<ParallelAsyncQuery<T>> ForEach<T>(this ParallelAsyncQuery<ParallelAsyncQuery<T>> items, Action<T> action)
        //=> items.ForEach(row => row.ForEach(item => action(item)));
        //public static ParallelAsyncQuery<ParallelAsyncQuery<T>> ForEach<T>(this ParallelAsyncQuery<ParallelAsyncQuery<T>> items, Action<T, int> action)
        //=> items.ForEach((row, x) => row.ForEach(item => action(item, x)));

        //public static ParallelAsyncQuery<ParallelAsyncQuery<T>> ForEach<T>(this ParallelAsyncQuery<ParallelAsyncQuery<T>> items, Action<T, int, int> action)
        //=> items.ForEach((row, x) => row.ForEach((item, y) => action(item, x, y)));

        //public static ParallelAsyncQuery<ParallelAsyncQuery<R>> Select<T, R>(this ParallelAsyncQuery<ParallelAsyncQuery<T>> items, Func<T, R> map)
        //=> items.Select(row => row.Select(item => map(item)));
        //public static ParallelAsyncQuery<ParallelAsyncQuery<R>> Select<T, R>(this ParallelAsyncQuery<ParallelAsyncQuery<T>> items, Func<T, int, R> map)
        //=> items.Select((row, x) => row.Select(item => map(item, x)));

        //public static ParallelAsyncQuery<ParallelAsyncQuery<R>> Select<T, R>(this ParallelAsyncQuery<ParallelAsyncQuery<T>> items, Func<T, int, int, R> map)
        //=> items.Select((row, x) => row.Select((item, y) => map(item, x, y)));

        //public static ParallelAsyncQuery<T> Flat<T>(this ParallelAsyncQuery<ParallelAsyncQuery<T>> items)
        //=> items.SelectMany(x => x);
        //public static ParallelAsyncQuery<T> Flat<T>(this ParallelAsyncQuery<ParallelAsyncQuery<T>> items, T endOfEnumerable)
        //=> items.SelectMany(x => x.Append(endOfEnumerable));
        public static ParallelAsyncQuery<R> Flat<T, R>(this ParallelAsyncQuery<ParallelAsyncQuery<T>> items, Func<ParallelAsyncQuery<T>, R> group)
        => items.Select(x => group(x));



        //public static ParallelAsyncQuery<ParallelAsyncQuery<T>> Where<T>(this ParallelAsyncQuery<ParallelAsyncQuery<T>> items, Func<T, bool> predicate)
        //{
        //    foreach (var row  in items)
        //    {
        //        yield return row.Where(predicate);               
        //    }
        //}
    }



    public static class ParallelAsyncQuery_CasesExtension
    {
        //------------------------------------------ Cases
        public static ParallelAsyncQuery<(int categoryIndex, T item)> Cases<C, T>(this ParallelAsyncQuery<(C category, T item)> items, params C[] categories) where C : notnull
        {
            var Dict = new Dictionary<C, int>(categories.Select((category, idx) => new KeyValuePair<C, int>(category, idx)));
            return items.Select(x => (Dict.ContainsKey(x.category) ? Dict[x.category] : Dict.Count, x.item));
        }


        //public static ParallelAsyncQuery<ParallelAsyncQuery<(int categoryIndex,  T item)>> Cases<C, T>(this ParallelAsyncQuery<ParallelAsyncQuery<(C category, T item)>> items, params C[] categories) where C : notnull
        //{
        //    var Dict = new Dictionary<C, int>(categories.Select((category, idx) => new KeyValuePair<C, int>(category, idx)));
        //    return items.Select(x => (Dict.ContainsKey(x.category) ? Dict[x.category] : Dict.Count,  x.item));
        //}

        static int getFilterIndex<T>(this Func<T, bool>[] filters, T item)
        {

            int CategoryIndex = 0;
            foreach (var predicate in filters)
            {
                if (predicate(item))
                    return CategoryIndex;
                else
                    CategoryIndex++;
            }

            return CategoryIndex;
        }

        public static ParallelAsyncQuery<(int category, T item)> Cases<T>(this ParallelAsyncQuery<T> items, params Func<T, bool>[] filters)
        => items.Select(item => (filters.getFilterIndex(item), item));




        //----------------------------------------------- SelectCase

        public static ParallelAsyncQuery<(int category, T item, R? newItem)> SelectCase<T, R>(this ParallelAsyncQuery<(int category, T item)> items, params Func<T, R>[] selectors)
        => items.Select(x => (x.category, x.item, (x.category < selectors.Length) ? selectors[x.category](x.item) : default));

        public static ParallelAsyncQuery<(int category, T, R? item)> SelectCase<T, R>(this ParallelAsyncQuery<(int category, T item)> items, params Func<T, int, R>[] selectors)
        => items.Select((x, idx) => (x.category, x.item, (x.category < selectors.Length) ? selectors[x.category](x.item, idx) : default));

        //-----------------with newItem

        public static ParallelAsyncQuery<(int category, T item, Y? newItem)> SelectCase<T, R, Y>(this ParallelAsyncQuery<(int category, T item, R newItem)> items, params Func<R, Y>[] selectors)
       => items.Select(x => (x.category, x.item, (x.category < selectors.Length) ? selectors[x.category](x.newItem) : default));

        public static ParallelAsyncQuery<(int category, T item, Y? newItem)> SelectCase<T, R, Y>(this ParallelAsyncQuery<(int category, T item, R newItem)> items, params Func<R, int, Y>[] selectors)
        => items.Select((x, idx) => (x.category, x.item, (x.category < selectors.Length) ? selectors[x.category](x.newItem, idx) : default));

        //------------------------------------------- ForEachCase

        public static ParallelAsyncQuery<(int category, T item)> ForEachCase<T>(this ParallelAsyncQuery<(int category, T item)> items, params Action[] actions)
        => items.ForEach(x => { if (x.category < actions.Length) actions[x.category](); });

        public static ParallelAsyncQuery<(int category, T item)> ForEachCase<T>(this ParallelAsyncQuery<(int category, T item)> items, params Action<T>[] actions)
        => items.ForEach(x => { if (x.category < actions.Length) actions[x.category](x.item); });

        public static ParallelAsyncQuery<(int category, T item)> ForEachCase<T>(this ParallelAsyncQuery<(int category, T item)> items, params Action<T, int>[] actions)

        => items.ForEach((x, index) => { if (x.category < actions.Length) actions[x.category](x.item, index); });


        //-----------------with newItem
        public static ParallelAsyncQuery<(int category, T item, R newItem)> ForEachCase<T, R>(this ParallelAsyncQuery<(int category, T item, R newItem)> items, params Action[] actions)
        => items.ForEach(x => { if (x.category < actions.Length) actions[x.category](); });

        public static ParallelAsyncQuery<(int category, T item, R newItem)> ForEachCase<T, R>(this ParallelAsyncQuery<(int category, T item, R newItem)> items, params Action<R>[] actions)
        => items.ForEach(x => { if (x.category < actions.Length) actions[x.category](x.newItem); });

        public static ParallelAsyncQuery<(int category, T item, R newItem)> ForEachCase<T, R>(this ParallelAsyncQuery<(int category, T item, R newItem)> items, params Action<R, int>[] actions)
        => items.ForEach((x, index) => { if (x.category < actions.Length) actions[x.category](x.newItem, index); });



        //------------------------------------AllCases
        public static ParallelAsyncQuery<T> UnCase<T>(this ParallelAsyncQuery<(int category, T item)> items)
        => items.Select(x => x.item);

        //------------------------------------AllCases
        public static ParallelAsyncQuery<T> UnCase<T, Y>(this ParallelAsyncQuery<(int category, T item, Y newItem)> items)
        => items.Select(x => x.item);

        public static ParallelAsyncQuery<R> AllCases<T, R>(this ParallelAsyncQuery<(int category, T item, R newItem)> items, bool filter = true) where T : class
        => filter ? items.Select(x => x.newItem).Where(x => x is not null && !x.Equals(default)) : items.Select(x => x.newItem);

       
    }





    public static class Spy_IAsyncEnumerableExtension
    {

        public const string BEFORE = "---------{\n";
        public const string AFTER = "\n-------}";
        public const string SEPARATOR = "\n";
        public static ParallelAsyncQuery<string> Spy(this ParallelAsyncQuery<string> items, string tag, bool timeStamp = false, string separator = SEPARATOR, string before = BEFORE, string after = AFTER)
        {
            return items.Spy<string>(tag, x => x, timeStamp, separator, before, after);
        }

        //public static async ParallelAsyncQuery<T> Spy<T>(this ParallelAsyncQuery<T> items, string tag, Func<T, string> customDispay, bool timeStamp = false, string separator = SEPARATOR, string before = BEFORE, string after = AFTER)
        //{
        //    string startedAt = string.Empty;
        //    Stopwatch stopwatch = new();
        //    if (timeStamp)
        //    {
        //        DateTime now = DateTime.Now;
        //        startedAt = $"[{now.Hour}:{now.Minute}:{now.Second}.{now.Millisecond}]";
        //        stopwatch = new Stopwatch();

        //        // Start the stopwatch
        //        stopwatch.Start();
        //    }

        //    Console.WriteLine(startedAt);
        //    if (!tag.IsNullOrEmpty())
        //        Console.Write(tag); Console.Write(" :");

        //    Console.Write(before);
        //    int i = 0;
        //    await foreach (var item in items)
        //    {
        //        if (i != 0) Console.Write(separator);
        //        Console.Write(customDispay(item));
        //        yield return item;

        //        i++;
        //    }

        //    Console.Write(after);
        //    if (timeStamp)
        //    {
        //        // Stop the stopwatch
        //        stopwatch.Stop();
        //        Console.Write($"[{stopwatch.Elapsed.TotalMilliseconds} ms]");
        //    }

        //}

        public static ParallelAsyncQuery<T> Spy<T>(
        this ParallelAsyncQuery<T> items,
        string tag,
        Func<T, string> customDisplay,
        bool timeStamp = false,
        string separator = "\n",
        string before = "---------{\n",
        string after = "\n-------}")
        {
            string startedAt = string.Empty;
            Stopwatch stopwatch = new();

            if (timeStamp)
            {
                DateTime now = DateTime.Now;
                startedAt = $"[{now:HH:mm:ss.fff}]";
                stopwatch = Stopwatch.StartNew();
            }

            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"{startedAt} {tag}");
            Console.ResetColor();
            Console.Write($" :{before}");

            int count = 0;
            items.ForEach(item =>
            {
                if (count > 0) Console.Write(separator);
                Console.Write(customDisplay(item));
                count++;
            });


            Console.Write(after);

            if (timeStamp)
            {
                stopwatch.Stop();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($" [{stopwatch.Elapsed.TotalMilliseconds:F0}ms, {count} items]");
                Console.ResetColor();
            }

            Console.WriteLine();
            return items;
        }

        public static async Task Display(this ParallelAsyncQuery<string?> items,
        string tag = "Displaying", string separator = SEPARATOR,
        string before = BEFORE, string after = AFTER)
        {
            Console.WriteLine();
            if (!tag.IsNullOrEmpty())
                Console.Write(tag); Console.Write(" :");

            Console.Write(before);
            int i = 0;
            await items.ForEach(item =>
            {
                if (i != 0) Console.Write(separator);
                Console.Write($"{i} :  {item}");
                i++;
            }).Do();

            Console.Write(after);
        }


        //------------------------------------------- TO COLLECTIONS

        /// <summary>
        /// Creates a List from an ParallelAsyncQuery.
        /// </summary>
        public static async Task<List<T>> ToList<T>(this ParallelAsyncQuery<T> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var list = new List<T>();
            await source.ForEach(item => list.Add(item)).Do();
            return list;
        }

        /// <summary>
        /// Creates an array from an ParallelAsyncQuery.
        /// </summary>
        public static async Task<T[]> ToArray<T>(this ParallelAsyncQuery<T> source)
        {
            var list = await source.ToList();
            return list.ToArray();
        }

        /// <summary>
        /// Creates a Dictionary from an ParallelAsyncQuery.
        /// </summary>
        public static async Task<Dictionary<TKey, TValue>> ToDictionary<T, TKey, TValue>(
            this ParallelAsyncQuery<T> source,
            Func<T, TKey> keySelector,
            Func<T, TValue> valueSelector,
            IEqualityComparer<TKey>? comparer = null) where TKey : notnull
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (keySelector == null)
                throw new ArgumentNullException(nameof(keySelector));
            if (valueSelector == null)
                throw new ArgumentNullException(nameof(valueSelector));

            var dictionary = new Dictionary<TKey, TValue>(comparer ?? EqualityComparer<TKey>.Default);
            await foreach (var item in source)
            {
                dictionary.Add(keySelector(item), valueSelector(item));
            }
            return dictionary;
        }

        //------------------------------------------- AGGREGATE

        /// <summary>
        /// Applies an accumulator function over a sequence.
        /// </summary>
        public static async Task<T> Aggregate<T>(this ParallelAsyncQuery<T> source, Func<T, T, T> func)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            await using var enumerator = source.GetAsyncEnumerator();

            if (!await enumerator.MoveNextAsync())
                throw new InvalidOperationException("Sequence contains no elements");

            T result = enumerator.Current;
            while (await enumerator.MoveNextAsync())
            {
                result = func(result, enumerator.Current);
            }

            return result;
        }

        /// <summary>
        /// Aggregate with seed value
        /// </summary>
        public static async Task<TAccumulate> Aggregate<T, TAccumulate>(
            this ParallelAsyncQuery<T> source,
            TAccumulate seed,
            Func<TAccumulate, T, TAccumulate> func)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (func == null)
                throw new ArgumentNullException(nameof(func));

            TAccumulate result = seed;
            await foreach (var item in source)
            {
                result = func(result, item);
            }

            return result;
        }
    }

}



