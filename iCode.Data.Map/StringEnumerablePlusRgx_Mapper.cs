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

namespace iCode.Data
{
    public static class StringEnumerable_Mapper
    {

        public static IEnumerable<T /*csv_struct*/> csv<T>(this IEnumerable<string /*line*/> items, string separator = ";") where T : struct
                                                      => items
                                                       .Where(x => !string.IsNullOrWhiteSpace(x))
                                                       .Select(line => line.csv<T>(separator))
                                                       .Where(csv => !csv.Equals(default(T)));

        public static EnumerablePlus<string, Rgxs> Map(this IEnumerable<string> items, Func<string, string> defaultMap)
                                         => items.Plus(new Rgxs(defaultMap));
        
        public static EnumerablePlus<string, Rgxs> Map(this IEnumerable<string> items, Regex rgx, params (string /*groupName*/, Func<string, string> /*map*/)[] requests)
                                       => items.Plus(new Rgxs()).Add(rgx, requests);

 
        public static EnumerablePlus<string, Rgxs> Map(this IEnumerable<string> items, Regex rgx, string groupName, Func<string, string> map)
                                        => items.Plus(new Rgxs()).Add(rgx, groupName, map);
            

     
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

   
