using DataFlow.Extensions;
using System.Text;

namespace DataFlow.Data
{
    static public class Writers
    {
        public static void WriteText(this IEnumerable<string> lines, StreamWriter file)
        {

            lines.ForEach(line => file.WriteLine(line)).Do();
            file.Close();
        }
        public static void WriteText(this IEnumerable<string> lines, string path, bool autoFlash = true)
        {
            var file = new StreamWriter(path);
            file.AutoFlush = autoFlash;
            lines.WriteText(file);

        }
        public static void WriteCSV<T>(this IEnumerable<T> records, StreamWriter file, bool withTitle = true, string separator = ";") where T : struct
        {
            if(withTitle)
            {
                file.WriteLine(CSV_Mapper.csv<T>());
            }
            records.ForEach(record => file.WriteLine(CSV_Mapper.csv<T>(record,separator))).Do();
            file.Close();
        }
        public static void WriteCSV<T>(this IEnumerable<T> records, string path, bool withTitle = true, string separator = ";") where T : struct
        {
            records.WriteCSV(new StreamWriter(path), withTitle, separator);
        }
        
    }

   
}