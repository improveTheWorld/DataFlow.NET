using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using iCode.Framework;
using iCode.Framework.Rgx;
using iCode.Extensions;
using iCode.Data.StringMapper;
using System.Security.Cryptography.X509Certificates;

namespace iCode.Data
{
    public static class StringEnumerable_Mapper
    {

        //public static string[] csvSchema(string title, string separator = ";")
        //{
        //    return title.Split(separator, StringSplitOptions.TrimEntries);
        //}
        //public static IEnumerable<T? > GetCSV<T>(this IEnumerable<string /*line*/> items, string separator = ";", params string[] schema) //where T : struct
        //                                              => items
        //                                               .Where(x => !x.IsNullOrWhiteSpace()).Spy("bfore")
        //                                               .Select(line => line.GetCSV<T>(schema, separator))
        //                                               .Where(csv => !csv?.Equals(default(T)) ?? false);

        //public static IEnumerable<T> csvWithSchema<T>(this IEnumerable<string > lines, string separator = ";") where T : struct
        //{
        //    string[] schema = null;

        //    return lines.SkipWhile(line => line.IsNullOrWhiteSpace())
        //    .Cases(
        //             (_, idx) => idx == 0,
        //             (x, _) => true
        //    )
        //    .DoCase(
        //        x => { schema = csvSchema(x); },
        //        x => {}
        //    )
        //    .Select(x=>x.item)
        //    .Where(x => !x.IsNullOrWhiteSpace())
        //    .Select(x => x.csv<T>(separator, schema)?? null)
        //    .Where(csv => !csv.Equals(default(T)));
        //}
                                                    

        public static EnumerablePlus<string, Rgxs> Map(this IEnumerable<string> items, Func<string, string> defaultMap)
                                         => items.Plus(new Rgxs(defaultMap));
        
        public static EnumerablePlus<string, Rgxs> Map(this IEnumerable<string> items, Regex rgx, params (string /*groupName*/, Func<string, string> /*map*/)[] requests)
                                       => items.Plus(new Rgxs()).Add(rgx, requests);

 
        public static EnumerablePlus<string, Rgxs> Map(this IEnumerable<string> items, Regex rgx, string groupName, Func<string, string> map)
                                        => items.Plus(new Rgxs()).Add(rgx, groupName, map);

        public static IEnumerable<T?> CSVs<T>(this IEnumerable<string> lines, string[] schema, string separator = ";")
                                                      => lines.Where(line=>!line.IsNullOrWhiteSpace())
                                                              .Select(line => line.GetCSV<T>(schema,separator))
                                                              .Where(csv => !csv?.Equals(default(T)) ?? false);
    }

    public static class StringEnumerablePlusRgx_Mapper
    {
        public static EnumerablePlus<string, Rgxs> Add(this EnumerablePlus<string, Rgxs> items, Regex rgx, params (string /*groupName*/, Func<string, string> /*map*/)[] requests)
        {
            items._Plus.Add(rgx, requests);
            return items;
        }
        public static EnumerablePlus<string, Rgxs> Add(this EnumerablePlus<string, Rgxs> items, Regex rgx, string groupName, Func<string, string> map)
        {
            items._Plus.Add(rgx, groupName, map);
            return items;
        }

        public static IEnumerable<string> Enumerate(this EnumerablePlus<string, Rgxs> items)
        {
            return items.Select(line => items._Plus.Map(line));
        }


    }
}

   
