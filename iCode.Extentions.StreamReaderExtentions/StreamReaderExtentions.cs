using iCode.Framework;

namespace iCode.Extentions.StreamReaderExtentions
{
    public static class StreamReaderExtentions
    {
        public static IEnumerable<string?> AsLinesEnumerable(this StreamReader file)
        {
            return new FileEnumerable(file);
        }
    }
}
