namespace iCode.Extentions.IntExtentions
{
    public static class IntExtentions
    {

        public static bool IsMultiple(this int value, int x)
        {
            return value % x == 0;
        }

    }
}
