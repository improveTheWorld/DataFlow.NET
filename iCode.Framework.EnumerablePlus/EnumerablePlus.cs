using System.Collections;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace iCode.Framework
{
    public class EnumerablePlus<T, P> : IEnumerable<T> // Explicitly implement IEnumerable<T>
    {
        public P _Plus { get; set; } // Consider making properties public if they need to be accessed outside
        private IEnumerable<T> Enumertor; // Keep fields private by convention

        public EnumerablePlus(IEnumerable<T> enumertor, P plus) // Constructor made public
        {
            Enumertor = enumertor;
            _Plus = plus;
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
        public static EnumerablePlus<T, P> Plus<T, P>(this IEnumerable<T> items, P? plus)
        {
            return new EnumerablePlus<T, P>(items, plus);
        }
    }       
}


