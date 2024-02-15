namespace iCode.Extensions
{
    public static class ArrayExtensions
    {
        public static int LastIdx<T>(this ICollection<T> array) => array.Count - 1;
    }
}