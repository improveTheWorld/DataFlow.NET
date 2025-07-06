using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;


namespace DataFlow.Extensions.ParallelQueryExtensions
{
    public static class ParallelQueryExtensions
    {
        /// <summary>
        /// Merge two ordered enumerables into a single ordered enumerable.
        /// Note: This operation requires sequential processing and will lose parallelism.
        /// </summary>
        public static IEnumerable<T> MergeOrdered<T>(this ParallelQuery<T> first, ParallelQuery<T> second, Func<T, T, bool> isFirstLessThanOrEqualToSecond)
        {
            // Convert to sequential for merging since this operation is inherently sequential
            using var enum1 = first?.AsSequential().GetEnumerator();
            using var enum2 = second?.AsSequential().GetEnumerator();

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

        public static ParallelQuery<T> Take<T>(this ParallelQuery<T> sequence, int start, int count)
            => sequence.Skip(start).Take(count);

        /// <summary>
        /// Takes elements until a condition is met. Note: This breaks parallelism.
        /// </summary>
        public static IEnumerable<T> Until<T>(this ParallelQuery<T> items, Func<bool> stopCondition)
        {
            if (stopCondition == null)
                throw new ArgumentNullException(nameof(stopCondition));

            foreach (var item in items.AsSequential())
            {
                if (stopCondition())
                    break;
                yield return item;
            }
        }

        public static IEnumerable<T> Until<T>(this ParallelQuery<T> items, Func<T, bool> stopCondition)
        {
            if (stopCondition == null)
                throw new ArgumentNullException(nameof(stopCondition));

            foreach (var item in items.AsSequential())
            {
                if (stopCondition(item))
                    break;
                yield return item;
            }
        }

        public static IEnumerable<T> Until<T>(this ParallelQuery<T> items, Func<T, int, bool> stopCondition)
        {
            if (stopCondition == null)
                throw new ArgumentNullException(nameof(stopCondition));

            int index = 0;
            foreach (var item in items.AsSequential())
            {
                if (stopCondition(item, index++))
                    break;
                yield return item;
            }
        }

        public static IEnumerable<T> Until<T>(this ParallelQuery<T> items, int lastItemIdx)
        {
            return items.AsSequential().Take(lastItemIdx + 1);
        }

        // Fixed ForEach methods - these maintain parallelism
        public static ParallelQuery<T> ForEach<T>(this ParallelQuery<T> items, Action<T, int> action)
        {
            return items.Select((x, idx) =>
            {
                action(x, idx);
                return x;
            });
        }

        public static ParallelQuery<T> ForEach<T>(this ParallelQuery<T> items, Action<T> action)
        {
            return items.Select(x =>
            {
                action(x);
                return x;
            });
        }

        // Fixed Do methods
        public static void Do<T>(this ParallelQuery<T> items, Action action)
        {
            items.ForAll(_ => action());
        }

        public static void Do<T>(this ParallelQuery<T> items)
        {
            items.ForAll(_ => { });
        }

        // Fixed Cumul - this is inherently sequential
        public static T? Cumul<T>(this ParallelQuery<T> sequence, Func<T?, T, T> cumulate)
        {
            var sequentialItems = sequence.AsSequential();
            if (!sequentialItems.Any()) return default;

            T? cumul = sequentialItems.First();
            foreach (var item in sequentialItems.Skip(1))
            {
                cumul = cumulate(cumul, item);
            }
            return cumul;
        }

        public static TResult? Cumul<T, TResult>(this ParallelQuery<T> sequence, Func<TResult?, T, TResult> cumulate, TResult? initial)
        {
            TResult? cumul = initial;
            foreach (var item in sequence.AsSequential())
            {
                cumul = cumulate(cumul, item);
            }
            return cumul;
        }

        // Fixed BuildString - this is inherently sequential due to StringBuilder
        public static StringBuilder BuildString(this ParallelQuery<string> items, StringBuilder? str = null, string separator = ", ", string before = "{", string after = "}")
        {
            str ??= new StringBuilder();

            if (!string.IsNullOrEmpty(before))
                str.Append(before);

            var itemsArray = items.ToArray(); // Materialize first
            for (int i = 0; i < itemsArray.Length; i++)
            {
                if (i > 0) str.Append(separator);
                str.Append(itemsArray[i]);
            }

            if (!string.IsNullOrEmpty(after))
                str.Append(after);

            return str;
        }

        public static StringBuilder BuildString(this ParallelQuery<string> items, string separator = ", ", string before = "{", string after = "}")
        {
            return items.BuildString(new StringBuilder(), separator, before, after);
        }

        // Fixed Sum - use built-in parallel sum when possible
        public static T Sum<T>(this ParallelQuery<T> items) where T : struct
        {
            // Use built-in parallel aggregation
            return items.Aggregate(default(T), (acc, item) => (dynamic)acc + (dynamic)item);
        }

        public static bool IsNullOrEmpty<T>(this ParallelQuery<T>? sequence)
        {
            if (sequence == null) return true;
            return !sequence.Any();
        }
    }

    public static class ParallelQuery_DeepLoopExtensions
    {
        public static ParallelQuery<T> Flat<T>(this ParallelQuery<ParallelQuery<T>> items)
            => items.SelectMany(x => x);

        public static ParallelQuery<T> Flat<T>(this ParallelQuery<ParallelQuery<T>> items, T endOfEnumerable)
            => items.SelectMany(x => x.Append(endOfEnumerable));

        public static ParallelQuery<R> Flat<T, R>(this ParallelQuery<ParallelQuery<T>> items, Func<ParallelQuery<T>, R> group)
            => items.Select(group);
    }

    public static class ParallelQuery_CasesExtension
    {
        public static ParallelQuery<(int categoryIndex, T item)> Cases<C, T>(this ParallelQuery<(C category, T item)> items, params C[] categories) where C : notnull
        {
            var dict = categories.Select((category, idx) => new { category, idx })
                                .ToDictionary(x => x.category, x => x.idx);

            return items.Select(x => (dict.TryGetValue(x.category, out var index) ? index : dict.Count, x.item));
        }

        public static ParallelQuery<(int category, T item)> Cases<T>(this ParallelQuery<T> items, params Func<T, bool>[] filters)
        {
            return items.Select(item =>
            {
                for (int i = 0; i < filters.Length; i++)
                {
                    if (filters[i](item))
                        return (i, item);
                }
                return (filters.Length, item);
            });
        }

        // SelectCase methods
        public static ParallelQuery<(int category, T item, R? newItem)> SelectCase<T, R>(this ParallelQuery<(int category, T item)> items, params Func<T, R>[] selectors)
            => items.Select(x => (x.category, x.item, x.category < selectors.Length ? selectors[x.category](x.item) : default(R)));

        public static ParallelQuery<(int category, T item, R? newItem)> SelectCase<T, R>(this ParallelQuery<(int category, T item)> items, params Func<T, int, R>[] selectors)
            => items.Select((x, idx) => (x.category, x.item, x.category < selectors.Length ? selectors[x.category](x.item, idx) : default(R)));

        // ForEachCase methods
        public static ParallelQuery<(int category, T item)> ForEachCase<T>(this ParallelQuery<(int category, T item)> items, params Action[] actions)
            => items.ForEach(x => { if (x.category < actions.Length) actions[x.category](); });

        public static ParallelQuery<(int category, T item)> ForEachCase<T>(this ParallelQuery<(int category, T item)> items, params Action<T>[] actions)
            => items.ForEach(x => { if (x.category < actions.Length) actions[x.category](x.item); });

        // UnCase methods
        public static ParallelQuery<T> UnCase<T>(this ParallelQuery<(int category, T item)> items)
            => items.Select(x => x.item);

        public static ParallelQuery<T> UnCase<T, Y>(this ParallelQuery<(int category, T item, Y newItem)> items)
            => items.Select(x => x.item);

        public static ParallelQuery<R> AllCases<T, R>(this ParallelQuery<(int category, T item, R newItem)> items, bool filter = true)
            => filter ? items.Select(x => x.newItem).Where(x => x != null && !x.Equals(default(R)))
                     : items.Select(x => x.newItem);

        // ToLines - inherently sequential
        public static IEnumerable<string> ToLines(this ParallelQuery<string> slices, string separator)
        {
            var sum = new StringBuilder();
            foreach (var slice in slices.AsSequential())
            {
                if (slice != separator)
                    sum.Append(slice);
                else
                {
                    yield return sum.ToString();
                    sum.Clear();
                }
            }
            if (sum.Length > 0)
                yield return sum.ToString();
        }
    }

    public static class Spy_ParallelQueryExtension
    {
        public const string BEFORE = "---------{\n";
        public const string AFTER = "\n-------}";
        public const string SEPARATOR = "\n";

        public static ParallelQuery<string> Spy(this ParallelQuery<string> items, string tag, bool timeStamp = false, string separator = SEPARATOR, string before = BEFORE, string after = AFTER)
            => items.Spy(tag, x => x, timeStamp, separator, before, after);

        public static ParallelQuery<T> Spy<T>(this ParallelQuery<T> items, string tag, Func<T, string> customDisplay, bool timeStamp = false, string separator = SEPARATOR, string before = BEFORE, string after = AFTER)
        {
            // Note: Spy operations are inherently sequential due to console output ordering
            var stopwatch = timeStamp ? Stopwatch.StartNew() : null;
            var startTime = timeStamp ? DateTime.Now : default;

            if (timeStamp)
                Console.WriteLine($"[{startTime:HH:mm:ss.fff}]");

            if (!string.IsNullOrEmpty(tag))
                Console.Write($"{tag} :");

            Console.Write(before);

            var results = new ConcurrentBag<(int index, T item, string display)>();
            var itemsWithIndex = items.Select((item, index) => new { item, index });

            itemsWithIndex.ForAll(x =>
            {
                var display = customDisplay(x.item);
                results.Add((x.index, x.item, display));
            });

            // Sort by index to maintain order for display
            var sortedResults = results.OrderBy(x => x.index).ToList();

            for (int i = 0; i < sortedResults.Count; i++)
            {
                if (i > 0) Console.Write(separator);
                Console.Write(sortedResults[i].display);
            }

            Console.Write(after);

            if (timeStamp && stopwatch != null)
            {
                stopwatch.Stop();
                Console.Write($"[{stopwatch.Elapsed.TotalMilliseconds} ms]");
            }

            return sortedResults.Select(x => x.item).AsParallel();
        }
    }

    public static class ConsoleMapper
    {
        public static void Display(this ParallelQuery<string> items, string tag = "Displaying", string separator = Spy_ParallelQueryExtension.SEPARATOR, string before = Spy_ParallelQueryExtension.BEFORE, string after = Spy_ParallelQueryExtension.AFTER)
        {
            Console.WriteLine();
            if (!string.IsNullOrEmpty(tag))
                Console.Write($"{tag} :");

            Console.Write(before);
            var itemsArray = items.ToArray();
            for (int i = 0; i < itemsArray.Length; i++)
            {
                if (i > 0) Console.Write(separator);
                Console.Write(itemsArray[i]);
            }
            Console.Write(after);
        }
    }

    public static class DictionaryExtensions
    {
        public static bool AddOrUpdate<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue value) where TKey : notnull
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
            return dict.TryGetValue(key, out var value) ? value : default;
        }
    }

    // Extension for string null/empty checks
    public static class StringExtensions
    {
        public static bool IsNullOrEmpty(this string? str) => string.IsNullOrEmpty(str);
    }
}
