using iCode.Extensions;
using System.Text;
using System.Text.RegularExpressions;


namespace iCode.Framework
{
    using NamedCapture = ValueTuple<string, Capture>;
    using FindResults = WithInfoEnumerator<WithInfoEnumerator<Capture, string /* groupName*/>, string /* LineSource*/>;
    static public  class RegexExtensions
    {
        public static FindResults Find(this string line, Regex regx, params string[] filterGroup)
        {
            return regx.Matches(line)
                        .Where(x => x.Success && (filterGroup.IsNullOrEmpty() || filterGroup.Select(n => x.Groups.ContainsKey(n)).FirstOrDefault(x => x)))
                        .SelectMany<Match, Group>(match => match.Groups)
                        .Where(group => group.Name != "0")
                        .Select(group => group.Captures.SetInfo(group.Name)).SetInfo(line);

        }

        public static string Replace(this FindResults lineResults, Func<string /*group*/, string /*captured*/, string> replace)
        {

            return lineResults.Info.ReplaceSorted(lineResults.SortByCaptureIndex().Values, replace);
        }
        public static string Replace(this FindResults lineResults, params (string groupName, Func<string /*capture*/, string> replace)[] remplacements)
        {
            return lineResults.Info.ReplaceSortedByGroup(lineResults.SortByCaptureIndex().Values, remplacements);
        }


        ////////////////////////////////////////////// internal kitchen

 
        static SortedList<int, NamedCapture> SortByCaptureIndex(this FindResults captures)
        {
            Dictionary<int, (string groupName, Capture capture)> inDict = 
                new(captures.
                    Flat().
                    Select(Capture_groupName => new KeyValuePair<int, NamedCapture>(Capture_groupName.Item1.Index, (Capture_groupName.Item2, Capture_groupName.Item1)))
                );
            return new(inDict);
        }


        // expose a fast way to use the _replaceSortedByGroup with a dictionnary
        static string ReplaceSortedByGroup(this string line, IEnumerable<NamedCapture> capturesSortedByIndex, params (string groupName, Func<string /*capture*/, string> replace)[] remplacements)
        {
            Dictionary<string, Func<string, string>> repalaceFuncDict = new(remplacements.Select(_ => new KeyValuePair<string, Func<string, string>>(_.groupName, _.replace)));
            return line.ReplaceSorted(capturesSortedByIndex, (groupname, capturedValue) =>
            {
                var replace = repalaceFuncDict.GetOrNull(groupname);
                return replace == null? capturedValue: replace(capturedValue);
               });
        }

        static string ReplaceSorted(this string line, IEnumerable<(string groupName, Capture capture)> capturesSortedByIndex, Func<string /*group*/, string /*capture*/, string> replace)
        {
            int parserIndex = 0;
            StringBuilder resultBuilder = new();
            capturesSortedByIndex.ForEach(_ =>
            {
                resultBuilder.Append(line.AsSpan().Slice(parserIndex, _.capture.Index - parserIndex))
                      .Append(replace == null ? _.capture.Value : replace(_.groupName,_.capture.Value));
                parserIndex = _.capture.Index + _.capture.Length;
            });

            return resultBuilder
                .Append(line.AsSpan().Slice(parserIndex))
                .ToString();
        }
    }
}
