
namespace iCode.Extensions
{
    public static class StreamReaderExtensions
    {
        public static IEnumerable<string> AsLines(this StreamReader file, bool autoClose = true)
        {
            while (!file.EndOfStream)
            {
                yield return file.ReadLine();
            }

            if(autoClose) file.Close();
 
        }
        public static IEnumerable<string> AsLines(this string filePath)
        {
           return new StreamReader(filePath).AsLines();
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
