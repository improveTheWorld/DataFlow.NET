using iCode.Extensions;
using System.Text;

namespace iCode.Data
{
    static public class Writers
    {
        public static void WriteText(this IEnumerable<string> lines, StreamWriter file)
        {
            lines.ForEach(line => file.WriteLine(line));
            file.Close();
        }
        public static void WriteText(this IEnumerable<string> lines, string path)
        {
            lines.WriteText(new StreamWriter(path));

        }
        public static void WriteCSV<T>(this IEnumerable<T> records, StreamWriter file, bool withTitle = true, string separator = ";") where T : struct
        {
            if(withTitle)
            {
                file.WriteLine(CSV_Mapper.csv<T>());
            }
            records.ForEach(record => file.WriteLine(CSV_Mapper.csv<T>(record,separator)));
            file.Close();
        }
        public static void WriteCSV<T>(this IEnumerable<T> records, string path, bool withTitle = true, string separator = ";") where T : struct
        {
            records.WriteCSV(new StreamWriter(path), withTitle, separator);
        }
    }

   
}