using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using iCode.Framework;
using iCode.Extensions;
using iCode.Data.StringMapper;
using System.Reflection.Metadata.Ecma335;

namespace iCode.Data
{
    public static class StringEnumerable_Mapper
    {       
        public static IEnumerable<EnumerablePlus<(string groupName, int startIndex, int length), string>> Slices(this IEnumerable<string> lines, Regxs regxs)
       => lines.Select(l =>l.Slices(regxs).Plus(l));

 
        public static IEnumerable<IEnumerable<(string groupName, string subpart)>> Map(this IEnumerable<string> lines, params string[] patterns)
        => lines.Select(l => l.Map(new Regxs(patterns)));

        //public static IEnumerable<IEnumerable<(string groupName, R subpart)>> SelectCase<R>(this IEnumerable<IEnumerable<(string groupName, string subpart)>> linesSubparts, params (string groupName, Func<string, R> transformation)[] transformations)
        //{
        //    var grpTransformations = new Dictionary<string /*groupName*/, Func<string, R> /*transformation*/>();

        //    transformations.ForEach(_ => grpTransformations[_.groupName] = _.transformation).Do();

        //    return linesSubparts.Select(part => (part.groupName, grpTransformations[part.groupName](part.subpart)));
        //}

        //public static IEnumerable<IEnumerable<(string groupName, string subpart)>> ForEachCase<T>(this IEnumerable<IEnumerable<(string groupName, string subpart)>> linesSubparts, params (string groupName, Action<string> action)[] actions)
        //{
        //    var grpTransformations = new Dictionary<string /*groupName*/, Action<string> /*transformation*/>();

        //    actions.ForEach(_ => grpTransformations[_.groupName] = _.action).Do();

        //    return linesSubparts.ForEach(part => grpTransformations[part.groupName](part.subpart));
        //}


        //public static IEnumerable<string> AllCases(this IEnumerable<IEnumerable<(string groupName, string subpart)>> linesSubparts)
        //{
        //    return linesSubparts.Select(lineParts => lineParts?.Cumul((a, b) => a + b.subpart,string.Empty)??string.Empty);
        //}

        public static StringBuilder Append(StringBuilder builder, IEnumerable<string> lines , Regxs regxs, params (string groupName, Func<string,string> transformation)[] transformations)
        {

            var rgxRequests = new Dictionary<string /*groupName*/, Func<string, string> /*transformation*/>();
            transformations.ForEach(_ => rgxRequests[_.groupName] = _.transformation).Do();

            foreach (var line in  lines)
            {
                builder.Build(line,line.Slices(regxs), rgxRequests);
                builder.AppendLine();
            }
            return builder;
        }

        //public static IEnumerable<string> Map(this IEnumerable<string> lines, Regxs regxs, params (string groupName, Func<string, string> transformation)[] transformations)
        //{
        //    var rgxRequests = new Dictionary<string /*groupName*/, Func<string, string> /*transformation*/>();
        //    transformations.ForEach(_ => rgxRequests[_.groupName] = _.transformation).Do();

        //    return lines.Select(line => new StringBuilder().Build(line, regxs, rgxRequests).ToString());
        //}

        public static IEnumerable<T?> CSVs<T>(this IEnumerable<string> lines, string[] schema, string separator = ";")
                                                      => lines.Where(line=>!line.IsNullOrWhiteSpace())
                                                              .Select(line => line.GetCSV<T>(schema,separator))
                                                              .Where(csv => !csv?.Equals(default(T)) ?? false);
    }


   // public static StringBuilder Build(this string line, Regxs regxs, StringBuilder builder = null)
   //=> regxs.Build(line, builder);

   // public static class StringEnumerablePlusRgx_Mapper
   // {
   //     public static EnumerablePlus<string, Regxs> Add(this EnumerablePlus<string, Regxs> items, Regex rgx, params (string /*groupName*/, Func<string, string> /*map*/)[] requests)
   //     {
   //         items._Plus.Add(rgx, requests);
   //         return items;
   //     }
   //     public static EnumerablePlus<string, Regxs> Add(this EnumerablePlus<string, Regxs> items, Regex rgx, string groupName, Func<string, string> map)
   //     {
   //         items._Plus.Add(rgx, groupName, map);
   //         return items;
   //     }

   //     public static IEnumerable<string> Enumerate(this EnumerablePlus<string, Regxs> items)
   //     {
   //         return items.Select(line => items._Plus.Build(line));
   //     }

   //     public static IEnumerable<string> Cases(this EnumerablePlus<string, Regxs> items)
   //     {
   //         return items.Select(line => items._Plus.Build(line));
   //     }


   // }
}

   
