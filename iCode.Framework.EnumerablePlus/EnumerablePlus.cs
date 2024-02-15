using System.Collections;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace iCode.Framework
{
    public class WithInfoEnumerator<T, P> : IEnumerable<T> // Explicitly implement IEnumerable<T>
    {
        public P Info { get; set; } // Consider making properties public if they need to be accessed outside
        private IEnumerable<T> Enumertor; // Keep fields private by convention

        public WithInfoEnumerator(IEnumerable<T> enumertor, P plus) // Constructor made public
        {
            Enumertor = enumertor;
            Info = plus;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return Enumertor.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }

    public static class EnumerablePlusExtension
    {
        public static WithInfoEnumerator<T,P> SetInfo<T,P>(this IEnumerable<T> items, P? plus )
        {
            return new WithInfoEnumerator<T,P>(items, plus);
        }

            
        public static IEnumerable<(T,P)> Flat<T,P>(this IEnumerable<WithInfoEnumerator<T,P>> items)
        {
            foreach (var itemsPlus in items)
            {
                foreach (var item in itemsPlus)
                {
                    yield return (item, itemsPlus.Info);
                }
            }
        }
    }
}


