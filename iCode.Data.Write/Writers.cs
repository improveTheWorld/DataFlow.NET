using iCode.Extensions;
namespace iCode.Data
{
    static public class Writers
    {
        public static void Write(this IEnumerable<string> lines, StreamWriter file)
        {
            lines.ForEach(line => file.WriteLine(line));
            file.Close();
        }
    }
}