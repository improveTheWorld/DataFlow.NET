
namespace iCode.Extensions
{
    public static class StreamReaderExtensions
    {
        public static IEnumerable<string> AsLines(this StreamReader file)
        {
            while (!file.EndOfStream)
            {
                yield return file.ReadLine();
            }
            file.Close();
        }
    }
}

namespace iCode.Extensions
{ 
    public static class StreamWriterExtensions
    {
        public static void Write(this StreamWriter file, IEnumerable<string> lines)
        {
            lines.ForEach(l => file.WriteLine(l));
            file.Close();
        }
       
    }
}
