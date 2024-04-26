using iCode.Extensions;
using System.Reflection.Metadata.Ecma335;
using System.Xml.Linq;

namespace iCode.Data
{
    public static class Read
    {
        public static IEnumerable<string /*line*/> text(StreamReader file, bool autoClose = true)
        {
            while (!file.EndOfStream)
            {
                yield return file.ReadLine();
            }

            if (autoClose) file.Close();
        }
        public static IEnumerable<string /*line*/> text(string path, bool autoClose = true)
        {
            return text(new StreamReader(path), autoClose);
        }

        public static IEnumerable<T?> csv<T>(string path, string separator = ";", bool autoClose = true, params string[] schema)
        {
            // skip white lines
            var csvLines = Read.text(path, autoClose)
                  .SkipWhile(line => line.IsNullOrWhiteSpace());


            //string[] csvSchema;

            if (schema.IsNullOrEmpty())
            {
                string title = csvLines.First();
                schema = title.Split(separator, StringSplitOptions.TrimEntries);
            }
           

            return csvLines.Skip(1) // skip title
                    .CSVs<T>(schema, separator);


            
        }
           
    }
}
    