using System.Collections.Concurrent;
using System.Text;


namespace DataFlow.Extensions;

public static class ParallelQueryExtensions
{
    /// <summary>
    /// Merge two ordered enumerables into a single ordered enumerable.
    /// Note: This operation requires sequential processing and will lose parallelism.
    /// </summary>
    public static IEnumerable<T> MergeOrdered<T>(this ParallelQuery<T> first, ParallelQuery<T> second, Func<T, T, bool> isFirstLessThanOrEqualToSecond)
    {
        // Convert to sequential for merging since this operation is inherently sequential
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

    public static ParallelQuery<T> Take<T>(this ParallelQuery<T> sequence, int start, int count)
        => sequence.Skip(start).Take(count);



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
    public static void Do<T>(this ParallelQuery<T> items, Action action)
    {
        items.ForEach(_ => action());
    }

    public static void Do<T>(this ParallelQuery<T> items)
    {
        items.ForEach(_ => { });
    }


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

    /// <summary>
    /// Computes the sum of a sequence of integer values in a thread-safe manner.
    /// Uses a long accumulator to prevent intermediate overflow.
    /// </summary>
    /// <exception cref="OverflowException">Thrown if the final sum is outside the range of Int32.</exception>
    public static int Sum(this ParallelQuery<int> source)
    {
        long sum = 0;
        // ForAll is a terminal operation in PLINQ, suitable for side-effects like this.
        source.ForAll(item => Interlocked.Add(ref sum, item));

        if (sum > int.MaxValue || sum < int.MinValue)
        {
            throw new OverflowException("The sum of the sequence is outside the bounds of a 32-bit integer.");
        }

        return (int)sum;
    }

    /// <summary>
    /// Computes the sum of a sequence of long values in a thread-safe manner.
    /// </summary>
    public static long Sum(this ParallelQuery<long> source)
    {
        long sum = 0;
        source.ForAll(item => Interlocked.Add(ref sum, item));
        return sum;
    }

    /// <summary>
    /// Computes the sum of a sequence of float values in a thread-safe manner.
    /// </summary>
    public static float Sum(this ParallelQuery<float> source)
    {
        float sum = 0;
        object lockObj = new object();
        // A lock is used because Interlocked does not support float.
        source.ForAll(item =>
        {
            lock (lockObj)
            {
                sum += item;
            }
        });
        return sum;
    }

    /// <summary>
    /// Computes the sum of a sequence of decimal values in a thread-safe manner.
    /// </summary>
    public static decimal Sum(this ParallelQuery<decimal> source)
    {
        decimal sum = 0;
        object lockObj = new object();
        // A lock is used because Interlocked does not support decimal.
        source.ForAll(item =>
        {
            lock (lockObj)
            {
                sum += item;
            }
        });
        return sum;
    }


    public static bool IsNullOrEmpty<T>(this ParallelQuery<T>? sequence)
    {
        if (sequence == null) return true;
        return !sequence.Any();
    }
}

