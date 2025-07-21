
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

    public static class IAsyncEnumerableExtensions
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
        public static async IAsyncEnumerable<T> MergeOrdered<T>(
        this IAsyncEnumerable<T> first,
        IAsyncEnumerable<T> second,
        Func<T, T, bool> isFirstLessThanOrEqualToSecond)
        {
            await using var enum1 = first?.GetAsyncEnumerator();
            await using var enum2 = second?.GetAsyncEnumerator();

            bool hasNext1 = enum1 != null && await enum1.MoveNextAsync();
            bool hasNext2 = enum2 != null && await enum2.MoveNextAsync();

            while (hasNext1 && hasNext2)
            {
                if (isFirstLessThanOrEqualToSecond(enum1!.Current, enum2!.Current))
                {
                    yield return enum1.Current;
                    hasNext1 = await enum1.MoveNextAsync();
                }
                else
                {
                    yield return enum2.Current;
                    hasNext2 = await enum2.MoveNextAsync();
                }
            }

            var remainingEnumerator = hasNext1 ? enum1 : hasNext2 ? enum2 : null;

            while (remainingEnumerator != null && await remainingEnumerator.MoveNextAsync())
            {
                yield return remainingEnumerator.Current;
            }
        }


        public static IAsyncEnumerable<T> Take<T>(this IAsyncEnumerable<T> sequence, int start, int count)
                => sequence.Take(new Range(start, start + count - 1));

        public static async IAsyncEnumerable<T> Take<T>(
        this IAsyncEnumerable<T> sequence,
        Range range)
        {
            if (range.Start.IsFromEnd || range.End.IsFromEnd)
                throw new ArgumentException("Range with IsFromEnd not supported for async sequences");

            int currentIndex = 0;
            int endIndex = range.End.Value;
            int start = range.Start.Value;

            await foreach (var item in sequence)
            {
                if (currentIndex >= start && currentIndex < endIndex)
                {
                    yield return item;
                }

                currentIndex++;

                if (currentIndex >= endIndex)
                    break;
            }
        }
        public static async IAsyncEnumerable<T> Until<T>(this IAsyncEnumerable<T> items, Func<bool> stopCondition)
        {
            if (stopCondition == null)
                throw new ArgumentNullException(nameof(stopCondition)); // Ensure there's a stop condition

            await foreach (var item in items)
            {
                yield return item;

                if (stopCondition())
                {
                    break;
                }
            }
        }

        public static async IAsyncEnumerable<T> Until<T>(this IAsyncEnumerable<T> items, Func<T, bool> stopCondition)
        {

            if (stopCondition == null)
                throw new ArgumentNullException(nameof(stopCondition)); // Ensure there's a stop condition

            await foreach (var item in items)
            {
                yield return item;

                if (stopCondition(item))
                {
                    break;
                }
            }
        }

        public static async IAsyncEnumerable<T> Until<T>(this IAsyncEnumerable<T> items, Func<T, int, bool> stopCondition)
        {
            if (stopCondition == null)
                throw new ArgumentNullException(nameof(stopCondition)); // Ensure there's a stop condition

            int index = 0;

            await foreach (var item in items)
            {
                yield return item;

                if (stopCondition(item, index++))
                {
                    break;
                }
            }
        }

        public static async IAsyncEnumerable<T> Until<T>(this IAsyncEnumerable<T> items, int lastItemIdx)
        {
            int index = 0;

            await foreach (var item in items)
            {
                yield return item;

                if (lastItemIdx == index++) break;
            }
        }

        public static IAsyncEnumerable<T> ForEach<T>(this IAsyncEnumerable<T> items, Action<T, int> action)
        {
            return items.Select((x, idx) =>
            {
                action(x, idx);
                return x;
            });
        }
        public static IAsyncEnumerable<T> ForEach<T>(this IAsyncEnumerable<T> items, Action<T> action)
        {
            return items.Select(x =>
            {
                action(x);
                return x;
            });
        }


        public static async Task Do<T>(this IAsyncEnumerable<T> items, Action action)
        {
            await foreach (var item in items)
            {
                action();
            }
        }

        public static async Task Do<T>(this IAsyncEnumerable<T> items)
        {
            await foreach (var item in items) ;
        }


        public static async Task<T?> Cumul<T>(this IAsyncEnumerable<T> sequence, Func<T?, T, T> cumulate)
        {
            T? cumul = await sequence.IsNullOrEmpty() ? default : await sequence.First();

            await sequence.Skip(1).ForEach(x => cumul = cumulate(cumul, x)).Do();

            return cumul;
        }

        public static async Task<TResult?> Cumul<T, TResult>(this IAsyncEnumerable<T> sequence, Func<TResult?, T, TResult> cumulate, TResult? initial)
        {
            TResult? cumul = initial;

            await sequence.ForEach(x => cumul = cumulate(cumul, x)).Do();

            return cumul;
        }

        public static async Task<StringBuilder> BuildString(this IAsyncEnumerable<string> items, StringBuilder str = null, string separator = ", ", string before = "{", string after = "}")
        {
            if (str is null) str = new StringBuilder();

            if (!before.IsNullOrEmpty())
                str.Append(before);

            await items.ForEach((x, idx) => { if (idx > 0) str.Append(separator); str.Append(x); }).Do();

            if (!after.IsNullOrEmpty())
                str.Append(after);
            return str;
        }
        public static async Task<StringBuilder> BuildString(this IAsyncEnumerable<string> items, string separator = ", ", string before = "{", string after = "}")
        {
            return await items.BuildString(new StringBuilder(), separator, before, after);
        }

        public static async Task<T?> Sum<T>(this IAsyncEnumerable<T> items)
        {
            T? sum = default;
            await items.ForEach(item => sum += (dynamic)item).Do();
            return sum;
        }

        public static async Task<bool> IsNullOrEmpty<T>(this IAsyncEnumerable<T> sequence)
        {
            if (sequence == null) return true;
            return !await sequence.Any();
        }



        /// <summary>
        /// Determines whether any element of a sequence satisfies a condition.
        /// </summary>
        public static async Task<bool> Any<T>(this IAsyncEnumerable<T> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            await using var enumerator = source.GetAsyncEnumerator();
            return await enumerator.MoveNextAsync();
        }

        /// <summary>
        /// Determines whether any element of a sequence satisfies a condition.
        /// </summary>
        public static async Task<bool> Any<T>(this IAsyncEnumerable<T> source, Func<T, bool> predicate)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            await foreach (var item in source)
            {
                if (predicate(item))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Async predicate version of Any
        /// </summary>
        public static async Task<bool> Any<T>(this IAsyncEnumerable<T> source, Func<T, Task<bool>> predicate)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            await foreach (var item in source)
            {
                if (await predicate(item))
                    return true;
            }
            return false;
        }

        //------------------------------------------- FIRST

        /// <summary>
        /// Returns the first element of a sequence.
        /// </summary>
        public static async Task<T> First<T>(this IAsyncEnumerable<T> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            await using var enumerator = source.GetAsyncEnumerator();
            if (await enumerator.MoveNextAsync())
                return enumerator.Current;

            throw new InvalidOperationException("Sequence contains no elements");
        }

        /// <summary>
        /// Returns the first element in a sequence that satisfies a specified condition.
        /// </summary>
        public static async Task<T> First<T>(this IAsyncEnumerable<T> source, Func<T, bool> predicate)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            await foreach (var item in source)
            {
                if (predicate(item))
                    return item;
            }

            throw new InvalidOperationException("No element satisfies the condition in predicate");
        }

        /// <summary>
        /// Async predicate version of First
        /// </summary>
        public static async Task<T> First<T>(this IAsyncEnumerable<T> source, Func<T, Task<bool>> predicate)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            await foreach (var item in source)
            {
                if (await predicate(item))
                    return item;
            }

            throw new InvalidOperationException("No element satisfies the condition in predicate");
        }

        //------------------------------------------- FIRST OR DEFAULT

        /// <summary>
        /// Returns the first element of a sequence, or a default value if no element is found.
        /// </summary>
        public static async Task<T?> FirstOrDefault<T>(this IAsyncEnumerable<T> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            await using var enumerator = source.GetAsyncEnumerator();
            if (await enumerator.MoveNextAsync())
                return enumerator.Current;

            return default(T);
        }

        /// <summary>
        /// Returns the first element that satisfies a condition or a default value if no such element is found.
        /// </summary>
        public static async Task<T?> FirstOrDefault<T>(this IAsyncEnumerable<T> source, Func<T, bool> predicate)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            await foreach (var item in source)
            {
                if (predicate(item))
                    return item;
            }

            return default(T);
        }

        /// <summary>
        /// Async predicate version of FirstOrDefault
        /// </summary>
        public static async Task<T?> FirstOrDefault<T>(this IAsyncEnumerable<T> source, Func<T, Task<bool>> predicate)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            await foreach (var item in source)
            {
                if (await predicate(item))
                    return item;
            }

            return default(T);
        }

        //------------------------------------------- SKIP

        /// <summary>
        /// Bypasses a specified number of elements in a sequence and then returns the remaining elements.
        /// </summary>
        public static async IAsyncEnumerable<T> Skip<T>(
            this IAsyncEnumerable<T> source,
            int count)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            int skipped = 0;
            await foreach (var item in source)
            {
                if (skipped >= count)
                    yield return item;
                else
                    skipped++;
            }
        }

        /// <summary>
        /// Bypasses elements in a sequence as long as a specified condition is true and then returns the remaining elements.
        /// </summary>
        public static async IAsyncEnumerable<T> SkipWhile<T>(
            this IAsyncEnumerable<T> source,
            Func<T, bool> predicate)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            bool yielding = false;
            await foreach (var item in source)
            {
                if (!yielding && !predicate(item))
                    yielding = true;

                if (yielding)
                    yield return item;
            }
        }

        /// <summary>
        /// Async predicate version of SkipWhile
        /// </summary>
        public static async IAsyncEnumerable<T> SkipWhile<T>(
            this IAsyncEnumerable<T> source,
            Func<T, Task<bool>> predicate)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            bool yielding = false;
            await foreach (var item in source)
            {
                if (!yielding && !await predicate(item))
                    yielding = true;

                if (yielding)
                    yield return item;
            }
        }

        /// <summary>
        /// Bypasses elements with index-based predicate
        /// </summary>
        public static async IAsyncEnumerable<T> SkipWhile<T>(
            this IAsyncEnumerable<T> source,
            Func<T, int, bool> predicate)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            bool yielding = false;
            int index = 0;
            await foreach (var item in source)
            {
                if (!yielding && !predicate(item, index))
                    yielding = true;

                if (yielding)
                    yield return item;

                index++;
            }
        }
    }
    public static class IAsyncEnumerable_dataSource
    {

        /// <summary>
        /// Throttles an asynchronous sequence, emitting items at a specified interval.
        /// </summary>
        /// <returns>An IAsyncEnumerable that yields items from the source sequence with a delay between each item.</returns>
        public static async IAsyncEnumerable<T> Throttle<T>(
            this IAsyncEnumerable<T> source,
            TimeSpan interval,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                yield return item;
                try
                {
                    await Task.Delay(interval, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        public static async IAsyncEnumerable<T> Throttle<T>(
            this IAsyncEnumerable<T> source,
            double intervalInMs,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var interval = TimeSpan.FromMilliseconds(intervalInMs);
            await foreach (var item in source.WithCancellation(cancellationToken))
            {
                yield return item;
                try
                {
                    await Task.Delay(interval, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

    }

        public static class IAsyncEnumerable_DeepLoopExtensions
    {
        /// <summary>
        /// Throttles a synchronous sequence, converting it to an asynchronous one that emits items at a specified interval.
        /// </summary>
        /// <returns>An IAsyncEnumerable that yields items from the source sequence with a delay between each item.</returns>
       

        //--------------------------------------  IAsyncEnumerable<IAsyncEnumerable<T>> 
        //public static IAsyncEnumerable<IAsyncEnumerable<T>> ForEach<T>(this IAsyncEnumerable<IAsyncEnumerable<T>> items, Action<T> action)
        //=> items.ForEach(row => row.ForEach(item => action(item)));
        //public static IAsyncEnumerable<IAsyncEnumerable<T>> ForEach<T>(this IAsyncEnumerable<IAsyncEnumerable<T>> items, Action<T, int> action)
        //=> items.ForEach((row, x) => row.ForEach(item => action(item, x)));

        //public static IAsyncEnumerable<IAsyncEnumerable<T>> ForEach<T>(this IAsyncEnumerable<IAsyncEnumerable<T>> items, Action<T, int, int> action)
        //=> items.ForEach((row, x) => row.ForEach((item, y) => action(item, x, y)));

        //public static IAsyncEnumerable<IAsyncEnumerable<R>> Select<T, R>(this IAsyncEnumerable<IAsyncEnumerable<T>> items, Func<T, R> map)
        //=> items.Select(row => row.Select(item => map(item)));
        //public static IAsyncEnumerable<IAsyncEnumerable<R>> Select<T, R>(this IAsyncEnumerable<IAsyncEnumerable<T>> items, Func<T, int, R> map)
        //=> items.Select((row, x) => row.Select(item => map(item, x)));

        //public static IAsyncEnumerable<IAsyncEnumerable<R>> Select<T, R>(this IAsyncEnumerable<IAsyncEnumerable<T>> items, Func<T, int, int, R> map)
        //=> items.Select((row, x) => row.Select((item, y) => map(item, x, y)));

        //public static IAsyncEnumerable<T> Flat<T>(this IAsyncEnumerable<IAsyncEnumerable<T>> items)
        //=> items.SelectMany(x => x);
        //public static IAsyncEnumerable<T> Flat<T>(this IAsyncEnumerable<IAsyncEnumerable<T>> items, T endOfEnumerable)
        //=> items.SelectMany(x => x.Append(endOfEnumerable));
        public static IAsyncEnumerable<R> Flat<T, R>(this IAsyncEnumerable<IAsyncEnumerable<T>> items, Func<IAsyncEnumerable<T>, R> group)
        => items.Select(x => group(x));



        //public static IAsyncEnumerable<IAsyncEnumerable<T>> Where<T>(this IAsyncEnumerable<IAsyncEnumerable<T>> items, Func<T, bool> predicate)
        //{
        //    foreach (var row  in items)
        //    {
        //        yield return row.Where(predicate);               
        //    }
        //}
    }



    public static class IAsyncEnumerable_CasesExtension
    {
        //------------------------------------------ Cases
        public static IAsyncEnumerable<(int categoryIndex, T item)> Cases<C, T>(this IAsyncEnumerable<(C category, T item)> items, params C[] categories) where C : notnull
        {
            var Dict = new Dictionary<C, int>(categories.Select((category, idx) => new KeyValuePair<C, int>(category, idx)));
            return items.Select(x => (Dict.ContainsKey(x.category) ? Dict[x.category] : Dict.Count, x.item));
        }


        //public static IAsyncEnumerable<IAsyncEnumerable<(int categoryIndex,  T item)>> Cases<C, T>(this IAsyncEnumerable<IAsyncEnumerable<(C category, T item)>> items, params C[] categories) where C : notnull
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

        public static IAsyncEnumerable<(int category, T item)> Cases<T>(this IAsyncEnumerable<T> items, params Func<T, bool>[] filters)
        => items.Select(item => (filters.getFilterIndex(item), item));




        //----------------------------------------------- SelectCase

        public static IAsyncEnumerable<(int category, T item, R? newItem)> SelectCase<T, R>(this IAsyncEnumerable<(int category, T item)> items, params Func<T, R>[] selectors)
        => items.Select(x => (x.category, x.item, (x.category < selectors.Length) ? selectors[x.category](x.item) : default));

        public static IAsyncEnumerable<(int category, T, R? item)> SelectCase<T, R>(this IAsyncEnumerable<(int category, T item)> items, params Func<T, int, R>[] selectors)
        => items.Select((x, idx) => (x.category, x.item, (x.category < selectors.Length) ? selectors[x.category](x.item, idx) : default));

        //-----------------with newItem

        public static IAsyncEnumerable<(int category, T item, Y? newItem)> SelectCase<T, R, Y>(this IAsyncEnumerable<(int category, T item, R newItem)> items, params Func<R, Y>[] selectors)
       => items.Select(x => (x.category, x.item, (x.category < selectors.Length) ? selectors[x.category](x.newItem) : default));

        public static IAsyncEnumerable<(int category, T item, Y? newItem)> SelectCase<T, R, Y>(this IAsyncEnumerable<(int category, T item, R newItem)> items, params Func<R, int, Y>[] selectors)
        => items.Select((x, idx) => (x.category, x.item, (x.category < selectors.Length) ? selectors[x.category](x.newItem, idx) : default));

        //------------------------------------------- ForEachCase

        public static IAsyncEnumerable<(int category, T item)> ForEachCase<T>(this IAsyncEnumerable<(int category, T item)> items, params Action[] actions)
        => items.ForEach(x => { if (x.category < actions.Length) actions[x.category](); });

        public static IAsyncEnumerable<(int category, T item)> ForEachCase<T>(this IAsyncEnumerable<(int category, T item)> items, params Action<T>[] actions)
        => items.ForEach(x => { if (x.category < actions.Length) actions[x.category](x.item); });

        public static IAsyncEnumerable<(int category, T item)> ForEachCase<T>(this IAsyncEnumerable<(int category, T item)> items, params Action<T, int>[] actions)

        => items.ForEach((x, index) => { if (x.category < actions.Length) actions[x.category](x.item, index); });


        //-----------------with newItem
        public static IAsyncEnumerable<(int category, T item, R newItem)> ForEachCase<T, R>(this IAsyncEnumerable<(int category, T item, R newItem)> items, params Action[] actions)
        => items.ForEach(x => { if (x.category < actions.Length) actions[x.category](); });

        public static IAsyncEnumerable<(int category, T item, R newItem)> ForEachCase<T, R>(this IAsyncEnumerable<(int category, T item, R newItem)> items, params Action<R>[] actions)
        => items.ForEach(x => { if (x.category < actions.Length) actions[x.category](x.newItem); });

        public static IAsyncEnumerable<(int category, T item, R newItem)> ForEachCase<T, R>(this IAsyncEnumerable<(int category, T item, R newItem)> items, params Action<R, int>[] actions)
        => items.ForEach((x, index) => { if (x.category < actions.Length) actions[x.category](x.newItem, index); });



        //------------------------------------AllCases
        public static IAsyncEnumerable<T> UnCase<T>(this IAsyncEnumerable<(int category, T item)> items)
        => items.Select(x => x.item);

        //------------------------------------AllCases
        public static IAsyncEnumerable<T> UnCase<T, Y>(this IAsyncEnumerable<(int category, T item, Y newItem)> items)
        => items.Select(x => x.item);

        public static IAsyncEnumerable<R> AllCases<T, R>(this IAsyncEnumerable<(int category, T item, R newItem)> items, bool filter = true) where T : class
        => filter ? items.Select(x => x.newItem).Where(x => x is not null && !x.Equals(default)) : items.Select(x => x.newItem);

        public static async IAsyncEnumerable<R> Select<T, R>(this IAsyncEnumerable<T> items, Func<T, R> Selector)
        {
            await foreach (var item in items)
            {
                yield return Selector(item); // Assuming T can be cast to R
            }
        }
        public static async IAsyncEnumerable<R> Select<T, R>(this IAsyncEnumerable<T> items, Func<T, int, R> Selector)
        {
            int idx = 0;
            await foreach (var item in items)
            {
                yield return Selector(item, idx); // Assuming T can be cast to R
                idx++;
            }
        }
        public static async IAsyncEnumerable<string> ToLines(this IAsyncEnumerable<string> slices, string separator)
        {
            string sum = "";
            await foreach (var slice in slices)
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
        public static async IAsyncEnumerable<string> AllCases(this IAsyncEnumerable<(int category, string item)> items, string separator)
        {
            string sum = "";
            await foreach (var (_, item) in items)
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





    public static class Spy_IAsyncEnumerableExtension
    {

        public const string BEFORE = "---------{\n";
        public const string AFTER = "\n-------}";
        public const string SEPARATOR = "\n";
        public static IAsyncEnumerable<string> Spy(this IAsyncEnumerable<string> items, string tag, bool timeStamp = false, string separator = SEPARATOR, string before = BEFORE, string after = AFTER)
        {
            return items.Spy<string>(tag, x => x, timeStamp, separator, before, after);
        }

        //public static async IAsyncEnumerable<T> Spy<T>(this IAsyncEnumerable<T> items, string tag, Func<T, string> customDispay, bool timeStamp = false, string separator = SEPARATOR, string before = BEFORE, string after = AFTER)
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

        public static async IAsyncEnumerable<T> Spy<T>(
       this IAsyncEnumerable<T> items,
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
            await foreach (var item in items)
            {
                if (count > 0) Console.Write(separator);
                Console.Write(customDisplay(item));
                yield return item;
                count++;
            }

            Console.Write(after);

            if (timeStamp)
            {
                stopwatch.Stop();
                Console.ForegroundColor = ConsoleColor.Green;
                Console.Write($" [{stopwatch.Elapsed.TotalMilliseconds:F0}ms, {count} items]");
                Console.ResetColor();
            }

            Console.WriteLine();
        }

        public static async Task Display(this IAsyncEnumerable<string?> items,
        string tag = "Displaying", string separator = SEPARATOR,
        string before = BEFORE, string after = AFTER)
        {
            Console.WriteLine();
            if (!tag.IsNullOrEmpty())
                Console.Write(tag); Console.Write(" :");

            Console.Write(before);
            int i = 0;
            await foreach (var item in items)
            {
                if (i != 0) Console.Write(separator);
                Console.Write($"{i} :  {item}");
                i++;
            }
            Console.Write(after);
        }



        //------------------------------------------- WHERE

        /// <summary>
        /// Filters a sequence of values based on a predicate.
        /// </summary>
        public static async IAsyncEnumerable<T> Where<T>(
            this IAsyncEnumerable<T> source,
            Func<T, bool> predicate)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            await foreach (var item in source)
            {
                if (predicate(item))
                    yield return item;
            }
        }

        /// <summary>
        /// Filters with async predicate
        /// </summary>
        public static async IAsyncEnumerable<T> Where<T>(
            this IAsyncEnumerable<T> source,
            Func<T, Task<bool>> predicate)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            await foreach (var item in source)
            {
                if (await predicate(item))
                    yield return item;
            }
        }

        /// <summary>
        /// Filters with index-based predicate
        /// </summary>
        public static async IAsyncEnumerable<T> Where<T>(
            this IAsyncEnumerable<T> source,
            Func<T, int, bool> predicate)

        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            int index = 0;
            await foreach (var item in source)
            {
                if (predicate(item, index))
                    yield return item;
                index++;
            }
        }

        //------------------------------------------- TAKE (Standard LINQ version)

        /// <summary>
        /// Returns a specified number of contiguous elements from the start of a sequence.
        /// </summary>
        public static async IAsyncEnumerable<T> Take<T>(
            this IAsyncEnumerable<T> source,
            int count)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (count <= 0)
                yield break;

            int taken = 0;
            await foreach (var item in source)
            {
                if (taken >= count)
                    break;

                yield return item;
                taken++;
            }
        }

        /// <summary>
        /// Returns elements from a sequence as long as a specified condition is true.
        /// </summary>
        public static async IAsyncEnumerable<T> TakeWhile<T>(
            this IAsyncEnumerable<T> source,
            Func<T, bool> predicate)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            await foreach (var item in source)
            {
                if (!predicate(item))
                    break;

                yield return item;
            }
        }

        /// <summary>
        /// TakeWhile with async predicate
        /// </summary>
        public static async IAsyncEnumerable<T> TakeWhile<T>(
            this IAsyncEnumerable<T> source,
            Func<T, Task<bool>> predicate)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

            await foreach (var item in source)
            {
                if (!await predicate(item))
                    break;

                yield return item;
            }
        }

        //------------------------------------------- SELECT MANY

        /// <summary>
        /// Projects each element to an IAsyncEnumerable and flattens the resulting sequences.
        /// </summary>
        public static async IAsyncEnumerable<TResult> SelectMany<T, TResult>(
            this IAsyncEnumerable<T> source,
            Func<T, IAsyncEnumerable<TResult>> selector)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (selector == null)
                throw new ArgumentNullException(nameof(selector));

            await foreach (var item in source)
            {
                await foreach (var subItem in selector(item))
                {
                    yield return subItem;
                }
            }
        }

        /// <summary>
        /// SelectMany with result selector
        /// </summary>
        public static async IAsyncEnumerable<TResult> SelectMany<T, TCollection, TResult>(
            this IAsyncEnumerable<T> source,
            Func<T, IAsyncEnumerable<TCollection>> collectionSelector,
            Func<T, TCollection, TResult> resultSelector)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (collectionSelector == null)
                throw new ArgumentNullException(nameof(collectionSelector));
            if (resultSelector == null)
                throw new ArgumentNullException(nameof(resultSelector));

            await foreach (var item in source)
            {
                await foreach (var subItem in collectionSelector(item))
                {
                    yield return resultSelector(item, subItem);
                }
            }
        }

        //------------------------------------------- DISTINCT

        /// <summary>
        /// Returns distinct elements from a sequence.
        /// </summary>
        public static async IAsyncEnumerable<T> Distinct<T>(
            this IAsyncEnumerable<T> source,
            IEqualityComparer<T>? comparer = null)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var seen = new HashSet<T>(comparer ?? EqualityComparer<T>.Default);

            await foreach (var item in source)
            {
                if (seen.Add(item))
                    yield return item;
            }
        }

        //------------------------------------------- CONCAT

        /// <summary>
        /// Concatenates two sequences.
        /// </summary>
        public static async IAsyncEnumerable<T> Concat<T>(
            this IAsyncEnumerable<T> first,
            IAsyncEnumerable<T> second)
        {
            if (first == null)
                throw new ArgumentNullException(nameof(first));
            if (second == null)
                throw new ArgumentNullException(nameof(second));

            await foreach (var item in first)
            {
                yield return item;
            }

            await foreach (var item in second)
            {
                yield return item;
            }
        }

        //------------------------------------------- APPEND / PREPEND

        /// <summary>
        /// Appends a value to the end of the sequence.
        /// </summary>
        public static async IAsyncEnumerable<T> Append<T>(
            this IAsyncEnumerable<T> source,
            T element)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            await foreach (var item in source)
            {
                yield return item;
            }

            yield return element;
        }

        /// <summary>
        /// Prepends a value to the beginning of the sequence.
        /// </summary>
        public static async IAsyncEnumerable<T> Prepend<T>(
            this IAsyncEnumerable<T> source,
            T element)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            yield return element;

            await foreach (var item in source)
            {
                yield return item;
            }
        }

        //------------------------------------------- COUNT

        /// <summary>
        /// Returns the number of elements in a sequence.
        /// </summary>
        //public static async Task<int> Count<T>(this IAsyncEnumerable<T> source)
        //{
        //    if (source == null)
        //        throw new ArgumentNullException(nameof(source));

        //    int count = 0;
        //    await foreach (var item in source)
        //    {
        //        count++;
        //    }
        //    return count;
        //}

        /// <summary>
        /// Returns the number of elements that satisfy a condition.
        /// </summary>
        //public static async Task<int> Count<T>(this IAsyncEnumerable<T> source, Func<T, bool> predicate)
        //{
        //    if (source == null)
        //        throw new ArgumentNullException(nameof(source));
        //    if (predicate == null)
        //        throw new ArgumentNullException(nameof(predicate));

        //    int count = 0;
        //    await foreach (var item in source)
        //    {
        //        if (predicate(item))
        //            count++;
        //    }
        //    return count;
        //}

        //------------------------------------------- ALL

        /// <summary>
        /// Determines whether all elements of a sequence satisfy a condition.
        /// </summary>
        //public static async Task<bool> All<T>(this IAsyncEnumerable<T> source, Func<T, bool> predicate)
        //{
        //    if (source == null)
        //        throw new ArgumentNullException(nameof(source));
        //    if (predicate == null)
        //        throw new ArgumentNullException(nameof(predicate));

        //    await foreach (var item in source)
        //    {
        //        if (!predicate(item))
        //            return false;
        //    }
        //    return true;
        //}

        /// <summary>
        /// All with async predicate
        /// </summary>
        //public static async Task<bool> All<T>(this IAsyncEnumerable<T> source, Func<T, Task<bool>> predicate)
        //{
        //    if (source == null)
        //        throw new ArgumentNullException(nameof(source));
        //    if (predicate == null)
        //        throw new ArgumentNullException(nameof(predicate));

        //    await foreach (var item in source)
        //    {
        //        if (!await predicate(item))
        //            return false;
        //    }
        //    return true;
        //}

        //------------------------------------------- TO COLLECTIONS

        /// <summary>
        /// Creates a List from an IAsyncEnumerable.
        /// </summary>
        public static async Task<List<T>> ToList<T>(this IAsyncEnumerable<T> source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            var list = new List<T>();
            await foreach (var item in source)
            {
                list.Add(item);
            }
            return list;
        }

        /// <summary>
        /// Creates an array from an IAsyncEnumerable.
        /// </summary>
        public static async Task<T[]> ToArray<T>(this IAsyncEnumerable<T> source)
        {
            var list = await source.ToList();
            return list.ToArray();
        }

        /// <summary>
        /// Creates a Dictionary from an IAsyncEnumerable.
        /// </summary>
        public static async Task<Dictionary<TKey, TValue>> ToDictionary<T, TKey, TValue>(
            this IAsyncEnumerable<T> source,
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
        public static async Task<T> Aggregate<T>(this IAsyncEnumerable<T> source, Func<T, T, T> func)
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
            this IAsyncEnumerable<T> source,
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


        //------------------------------------------- BUFFER/BATCH

        /// <summary>
        /// Buffers elements into batches of specified size
        /// </summary>
        public static async IAsyncEnumerable<T[]> Buffer<T>(
            this IAsyncEnumerable<T> source,
            int size)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (size <= 0)
                throw new ArgumentOutOfRangeException(nameof(size));

            var buffer = new List<T>(size);

            await foreach (var item in source)
            {
                buffer.Add(item);

                if (buffer.Count == size)
                {
                    yield return buffer.ToArray();
                    buffer.Clear();
                }
            }

            if (buffer.Count > 0)
            {
                yield return buffer.ToArray();
            }
        }
    }

    public static class IAsyncEnumeratorExtensions
    {
        public static async Task<(bool, T?)> TryGetNext<T>(this IAsyncEnumerator<T> enumerator)
        {
            if (await enumerator.MoveNextAsync())
            {
                return (true, enumerator.Current);
            }
            return (false, default(T));
        }
        public static async Task<T?> GetNext<T>(this IAsyncEnumerator<T> enumerator)
        {
            if (await enumerator.MoveNextAsync())
            {
                return enumerator.Current;

            }
            else

                return default(T);
        }
    }

    public static class AsyncEnumerableFromPollingExtensions
    {
        public static IAsyncEnumerable<T> Poll<T>(
      this Func<T> pollAction,
      TimeSpan pollingInterval,
      CancellationToken cancellationToken = default)
        {
            // Call the main overload with a stop condition that never triggers.
            return pollAction.Poll(pollingInterval, (item, elapsed) => false, cancellationToken);
        }
        /// <summary>
        /// Represents a method that attempts to retrieve an item.
        /// This is the standard "TryGet" pattern.
        /// </summary>
        /// <typeparam name="T">The type of the item to retrieve.</typeparam>
        /// <param name="item">When this method returns, contains the retrieved item if the
        /// retrieval succeeded, or the default value for T if it failed.</param>
        /// <returns>true if an item was successfully retrieved; otherwise, false.</returns>
        public delegate bool TryPollAction<T>(out T item);

        /// <summary>
        /// Creates an IAsyncEnumerable<T> by polling a function that uses the "TryGet" pattern.
        /// </summary>
        /// <typeparam name="T">The type of item to poll for.</typeparam>
        /// <param name="tryPollAction">
        /// The source function to be called periodically, following the TryGet pattern.
        /// </param>
        /// <param name="pollingInterval">
        /// The time to wait between calls to the tryPollAction.
        /// </param>
        /// <param name="stopCondition">x²x²x²x 
        /// A function evaluated after each poll. It receives the success status, the polled 
        /// item (which may be default), and the total elapsed time. Polling stops when it 
        /// returns true.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token to stop the polling process externally.
        /// </param>
        /// <returns>
        /// An IAsyncEnumerable<T> that yields items as they are discovered by the tryPollAction.
        /// </returns>
        public static async IAsyncEnumerable<T> Poll<T>(
            this TryPollAction<T> tryPollAction,
            TimeSpan pollingInterval,
            Func<T, TimeSpan, bool> stopCondition,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            while (!cancellationToken.IsCancellationRequested)
            {
                // 1. Poll for the next element using the TryGet pattern
                bool success = tryPollAction(out T item);

                // 2. Check the master stop condition
                if (!success || stopCondition(item, stopwatch.Elapsed))
                {
                    yield break;
                }

                // 3. If the poll was successful, yield the item.
                //    No need for a default check; the 'success' bool is the source of truth.
                else
                {
                    yield return item;
                }

                // 4. Wait for the specified polling period
                try
                {
                    await Task.Delay(pollingInterval, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

        }

        /// <summary>
        /// Creates an IAsyncEnumerable<T> by treating the source function as a polling action.
        /// </summary>
        /// <remarks>
        /// This creates a fluent API allowing you to write: myPollFunc.Poll(...)
        /// </remarks>
        /// <typeparam name="T">The type of item to poll for.</typeparam>
        /// <param name="pollAction">
        /// The source function to be called periodically. It will be extended by this method.
        /// </param>
        /// <param name="pollingInterval">
        /// The time to wait between calls to the pollAction.
        /// </param>
        /// <param name="stopCondition">
        /// A function that is evaluated after each poll. Polling stops when it returns true.
        /// </param>
        /// <param name="cancellationToken">
        /// A cancellation token to stop the polling process externally.
        /// </param>
        /// <returns>
        /// An IAsyncEnumerable<T> that yields items as they are discovered by the pollAction.
        /// </returns>
        public static async IAsyncEnumerable<T> Poll<T>(
            this Func<T> pollAction,
            TimeSpan pollingInterval,
            Func<T, TimeSpan, bool> stopCondition,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var stopwatch = Stopwatch.StartNew();

            while (!cancellationToken.IsCancellationRequested)
            {
                T item = pollAction();

                if (stopCondition(item, stopwatch.Elapsed))
                {
                    yield break;
                }

                if (!EqualityComparer<T>.Default.Equals(item, default(T)))
                {
                    yield return item;
                }

                try
                {
                    await Task.Delay(pollingInterval, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Creates an IAsyncEnumerable<T> by polling a "TryGet" function indefinitely until cancelled or no more elements.
        /// </summary>
        public static IAsyncEnumerable<T> Poll<T>(
            this TryPollAction<T> tryPollAction,
            TimeSpan pollingInterval,
            CancellationToken cancellationToken = default)
        {
            // Call the main overload with a stop condition that never triggers.
            return tryPollAction.Poll(pollingInterval, (item, elapsed) => false, cancellationToken);
        }

    }
}







