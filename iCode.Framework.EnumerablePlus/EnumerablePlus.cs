using System.Collections;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using iCode.Extensions;

namespace iCode.Framework
{
    public class EnumerablePlus<T, P> : IEnumerable<T> // Explicitly implement IEnumerable<T>
    {
        public P _Plus { get; set; } // Consider making properties public if they need to be accessed outside
        public IEnumerable<T> Enumerable; // Keep fields private by convention

        public EnumerablePlus(IEnumerable<T> enumerable, P plus) // Constructor made public
        {
            Enumerable = enumerable;
            _Plus = plus;
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

    public static class EnumerablePlusExtension
    {
        public static EnumerablePlus<T, P> Plus<T, P>(this IEnumerable<T> items, P plus)
        => new EnumerablePlus<T, P>(items, plus);

        public static IEnumerable<T> Minus<T, P>(this EnumerablePlus<T, P> items)
        => items;

        public static IEnumerable<T> Minus<T, P>(this EnumerablePlus<T, P> items, Action close)
        {
                close();
                return items;
        }

        public static EnumerablePlus<(int category, T item, R newItem), P> ForEachCase<T, R, P>(this EnumerablePlus<(int category, T item, R newItem), P> items, params Action<R, P>[] actions)
        {
            items.ForEach(x => { if (x.category < actions.Length) actions[x.category](x.newItem, items._Plus); });
            return items;
        }

    }


}


