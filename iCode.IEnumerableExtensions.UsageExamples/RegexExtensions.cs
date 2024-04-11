using iCode.Log;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NamedCapture = System.ValueTuple<string, System.Text.RegularExpressions.Capture>;


namespace iCode.Extensions
{
    static public  class RegexExtensions
    {

        //IEnumerable Lines Capture 
        public static IEnumerable<(string line, IEnumerable<NamedCapture> captures)> Captures(this IEnumerable<string> lines, string pattern, params string[] filterGroup)
        {
            Regex regx = new Regex(pattern);
            return lines.Select(line => (line, line.Captures(regx,filterGroup)));
        }

        //One line Capture
        public static IEnumerable<NamedCapture> Captures(this string line, Regex regx, params string[] filterGroup)
        {
            return regx.Matches(line)
                        .Where(x => x.Success && (filterGroup.IsNullOrEmpty() || filterGroup.Select(n => x.Groups.ContainsKey(n)).FirstOrDefault(x => x)))
                        .SelectMany<Match, Group>(match => match.Groups)
                        .Where(group => group.Name != "0")
                        .SelectMany(group => group.Captures.Select(cap => (group.Name, cap)));
        }

        //IEnumerable Lines parsing and replacement  
        public static IEnumerable<string> Replace(this IEnumerable<(string line, IEnumerable<NamedCapture> captures)> captured , Func<string /*group*/, string /*captured*/, string> replace)
        {
            return captured.Select(_ => _.captures.Replace(_.line, replace));
        }

        //IEnumerable Lines parsing and replacement 
        public static IEnumerable<string> ReplaceByGroup(this IEnumerable<(string line, IEnumerable<NamedCapture> captures)> captured, params (string groupName, Func<string /*capture*/, string> replace)[] replacements)
        {
            return captured.Select(_ => _.captures.ReplaceByGroup(_.line, replacements));
        }


        //one line parsing and replacement
        public static string Replace(this IEnumerable<NamedCapture> captures, string line, Func<string /*group*/, string /*captured*/, string> replace)
        {

            return line.ReplaceSorted(captures.SortByCaptureIndex().Values, replace);
        }

        //one line parsing and replacement
        public static string ReplaceByGroup(this IEnumerable<NamedCapture> captures, string line, params (string groupName, Func<string /*capture*/, string> replace)[] remplacements)
        {
            return line.ReplaceSortedByGroup(captures.SortByCaptureIndex().Values, remplacements);
        }


        ////////////////////////////////////////////// internal kitchen

        static SortedList<int, NamedCapture> SortByCaptureIndex(this IEnumerable<(string groupName, Capture capture)> captures)
        {
            Dictionary<int, (string groupName, Capture capture)> inDict = new(captures.Select(x => new KeyValuePair<int, NamedCapture>(x.capture.Index, (x.groupName, x.capture))));
            return  new(inDict);
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
