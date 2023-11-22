namespace iCode.Extensions.IntExtensions
{
    public static class IntExtensions
    {

        public static bool IsMultiple(this int value, int x)
        {
            return value % x == 0;
        }

    }
}
