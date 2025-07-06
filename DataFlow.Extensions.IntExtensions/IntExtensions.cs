namespace DataFlow.Extensions
{
    public static class IntExtensions
    {

        public static bool IsMultiple(this int value, int x)
        {
            return value % x == 0;
        }

    }
}
