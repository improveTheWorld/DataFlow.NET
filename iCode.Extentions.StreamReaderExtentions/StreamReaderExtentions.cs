using iCode.Framework;

namespace iCode.Extentions.StreamReaderExtentions
{
    public static class StreamReaderExtentions
    {
        public static IEnumerable<string?> AsLines(this StreamReader file)
        {
            while(!file.EndOfStream)
            {
                yield return  file.ReadLine();
            }
            file.Close();
        }
    }
}
