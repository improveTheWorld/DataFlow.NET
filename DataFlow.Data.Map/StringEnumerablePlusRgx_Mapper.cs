using System.Text;
using DataFlow.Framework;
using DataFlow.Extensions;
using DataFlow.Data.StringMapper;

namespace DataFlow.Data;

public static class StringEnumerable_Mapper
{       
    public static IEnumerable<EnumerableWithNote<(string groupName,( int startIndex, int length) slice), string>> Slices(this IEnumerable<string> lines, Regxes regxs)
   => lines.Select(l =>l.Slices(regxs).WithNote(l));

    public static IEnumerable<(string groupName, string subpart)> Map(this IEnumerable<string> lines, params string[] patterns)
    => lines.SelectMany(l => l.Map(new Regxes(patterns)).Append((Regxes.UNMATCHED.EOF, Environment.NewLine)));

    public static IEnumerable<List<(string groupName, string subpart)>> MapLines(this IEnumerable<string> lines, params string[] patterns)
    {
        var regxs = new Regxes(patterns);
        return lines.Select(line=> line.Map(regxs).ToList());
    }

   
    public static StringBuilder Append(StringBuilder builder, IEnumerable<string> lines , Regxes regxs, params (string groupName, Func<string,string> transformation)[] transformations)
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


    public static IEnumerable<T?> CSVs<T>(this IEnumerable<string> lines, string[] schema, string separator = ";")
                                                  => lines.Where(line=>!line.IsNullOrWhiteSpace())
                                                          .Select(line => line.GetCSV<T>(schema,separator))
                                                          .Where(csv => !csv?.Equals(default(T)) ?? false);
}



