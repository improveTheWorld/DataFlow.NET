namespace DataFlow.Extensions;

//---------------------------------------------------IEnumerable<IEnumerable<T>>

public static class EnumeratorExtensions
{
    /// <summary>
    /// Advances the enumerator to the next element in the sequence,
    /// providing the result in an out parameter.
    /// </summary>
    /// <typeparam name="T">The type of the elements.</typeparam>
    /// <param name="enumerator">The enumerator to advance.</param>
    /// <param name="value">When this method returns, contains the element at the new
    /// position, or default(T) if the end of the sequence was reached.</param>
    /// <returns>
    /// true if the enumerator was successfully advanced to the next element;
    /// false if the enumerator has passed the end of the sequence.
    /// </returns>
    public static bool TryGetNext<T>(this IEnumerator<T> enumerator, out T? value)
    {
        if (enumerator.MoveNext())
        {
            value = enumerator.Current;
            return true;
        }

        value = default(T);
        return false;
    }
    public static T? GetNext<T>(this IEnumerator<T> enumerator)
    {
        if (enumerator.MoveNext())
        {
            return enumerator.Current;

        }
        else

            return default(T);
    }
}


