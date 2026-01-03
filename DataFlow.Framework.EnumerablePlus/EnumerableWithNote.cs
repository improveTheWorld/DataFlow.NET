using DataFlow;
using System.Collections;


namespace DataFlow.Framework;

public class EnumerableWithNote<T, TNote> : IEnumerable<T>
{
    public TNote Note { get; set; }
    public IEnumerable<T> Enumerable;

    public EnumerableWithNote(IEnumerable<T> enumerable, TNote note)
    {
        Enumerable = enumerable;
        Note = note;
    }

    public IEnumerator<T> GetEnumerator()
    {
        return Enumerable.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return Enumerable.GetEnumerator();
    }


}

public static class EnumerableWithNoteExtensions
{
    public static void Deconstruct<T, TNote>(
    this EnumerableWithNote<T, TNote> source,
    out IEnumerable<T> enumerable,
    out TNote note)
    {
        enumerable = source;
        note = source.Note;
    }

    // Original methods
    public static EnumerableWithNote<T, TNote> WithNote<T, TNote>(this IEnumerable<T> items, TNote note)
    => new EnumerableWithNote<T, TNote>(items, note);

    public static IEnumerable<T> WithoutNote<T, TNote>(this EnumerableWithNote<T, TNote> items)
    => items;

    public static IEnumerable<T> WithoutNote<T, TNote>(this EnumerableWithNote<T, TNote> items, Action close)
    {
        close();
        return items;
    }


    public static EnumerableWithNote<(int category, T item), TNote> Cases<T, TNote>(
        this EnumerableWithNote<T, TNote> items,
        params Func<T, TNote, bool>[] filters)
    {
        var note = items.Note;
        return items.Select(item =>
        {
            for (int i = 0; i < filters.Length; i++)
                if (filters[i](item, note))
                    return (i, item);
            return (filters.Length, item);
        }).WithNote(note);

    }

    public static EnumerableWithNote<(int category, T item, R newItem), TNote> ForEachCase<T, R, TNote>(
        this EnumerableWithNote<(int category, T item, R newItem), TNote> items,
        params Action<R, TNote>[] actions)
    {
        var note = items.Note;
        return items.ForEach(x =>
        {
            if (x.category < actions.Length)
                actions[x.category](x.newItem, note);
        }).WithNote(note);
    }

  
    public static EnumerableWithNote<(int category, T item, R? newItem), TNote> SelectCase<T, R, TNote>(
 this EnumerableWithNote<(int category, T item), TNote> items,
 params Func<T, TNote, R>[] selectors)
    {
        var note = items.Note; // Capture once for performance
        return items.Select(x => (
            x.category,
            x.item,
            x.category < selectors.Length ? selectors[x.category](x.item, note) : default
        )).WithNote(note);
    }


    public static EnumerableWithNote<(int category, T item, Y? newItem), TNote> SelectCase<T, R, Y, TNote>(
        this EnumerableWithNote<(int category, T item, R newItem), TNote> items,
        params Func<R, TNote, Y>[] selectors)
    {
        var note = items.Note;
        return items.Select(x => (
            x.category,
            x.item,
            x.category < selectors.Length ? selectors[x.category](x.newItem, items.Note) : default
        )).WithNote(note);

    }


}
