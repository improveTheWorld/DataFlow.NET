using iCode.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace iCode.Framework
{

    using RgxRequests = Dictionary<string /*groupName*/, Func<string, string> /*transformation*/>;
    using static iCode.Framework.RegxsExt;

    public class Regxs
    {
        HashSet<Regex> regexs = new HashSet<Regex>();
      
        public Regxs(params Regex[] Regs)
        {
            Regs
                .ForEach(r => regexs.Add(r))
                .Do();
        }

        public Regxs(params string[] patterns)
        {
            patterns
                .ForEach(p => regexs.Add(new Regex(p)))
                .Do();
        }

       
        public Regxs Add(Regex regex)
        {
            regexs.Add(regex);
            return this;
        }
        public Regxs Add(string pattern)
        {
            regexs.Add(new Regex(pattern));
            return this;
        }

        static IEnumerable<(string groupName, int startIndex, int Length)> Slices(IEnumerable<(string groupName, Capture capture)> capturesSortedByIndex, int MaxIndex)
        {
            int parserIndex = 0;

            foreach (var item in capturesSortedByIndex)
            {
                if(item.capture.Index > parserIndex) yield return (UNMATCHED_SLICE, parserIndex, item.capture.Index - parserIndex);
                yield return (item.groupName, item.capture.Index, item.capture.Length);
                parserIndex = item.capture.Index + item.capture.Length;

            }

            if(parserIndex < MaxIndex) yield return (UNMATCHED_SLICE, parserIndex, MaxIndex - parserIndex);
        }

        public IEnumerable<(string groupName, int startIndex, int Length) > Slices(ref string line)
        {
            foreach (var regex in regexs)
            {
                var result = regex.Matches(line)
                        .Where(x => x.Success)
                        .SelectMany<Match, Group>(match => match.Groups)
                        .Where(group => group.Name != "0");


                // Case regex matched
                if (!result.IsNullOrEmpty())
                {
                    return Slices(getCapturesSortedByIndex(result),line.Length);
                }
            }            
            return new (string groupName, int startIndex, int Length)[1] { (UNMATCHED_LINE, 0,-1) }; //ToDo: Unmatched Line? what about matched without any group( regex without group Caption)??
        }

        public IEnumerable<(string groupName, string subpart)> Map(string line)
        {
            return Slices(ref line)
                .Cases(
                    _ => _.groupName == UNMATCHED_LINE,  
                    _ => true    //Slices
                )
                .SelectCase(
                    _ => (UNMATCHED_LINE,  line),
                    _ => (_.groupName, line.Substring(_.startIndex, _.Length))
                )
                .AllCases();
        }

        
        // 
        IEnumerable<(string groupName, Capture capture)> getCapturesSortedByIndex(IEnumerable<Group> result)
        {
            Dictionary<int, (string groupName, Capture capture)> inDict =
                new(result
                    .SelectMany(group => group.Captures.Select(cap => new KeyValuePair<int, (string groupName, Capture capture)>(cap.Index, (group.Name, cap)))));
            return (new SortedList<int, (string groupName, Capture capture)> (inDict)).Values;
        }

       

    }

    public static class RegxsExt
    {

        //public const string LAST_SLICE = "%LastLineSlice%";
        public const string UNMATCHED_LINE = "%UnmatchedLine%";
        public const string UNMATCHED_SLICE = "%UnmatchedSlice%";
        public static bool isUnmatched(this (string groupName, string subpart) r) => r.groupName.StartsWith("%Unmatched");

        public static IEnumerable<(string groupName, string subpart)> Map(this string line, Regxs regxs)
        => regxs.Map(line);
        public static IEnumerable<(string groupName, R subpart)> SelectCase<R>(this IEnumerable<(string groupName, string subpart)> lineSubparts, params (string groupName, Func<string, R> transformation)[] transformations)
        {
            var grpTransformations = new Dictionary<string /*groupName*/, Func<string, R> /*transformation*/>();

            transformations.ForEach(_ => grpTransformations[_.groupName] = _.transformation).Do();


            return lineSubparts.Select(part => (part.groupName, grpTransformations[part.groupName](part.subpart)));
        }

        public static IEnumerable<(string groupName, string subpart)> ForEachCase(this IEnumerable<(string groupName, string subpart)> lineSubparts, params (string groupName, Action<string> action)[] actions)
        {
            var grpActions = new Dictionary<string /*groupName*/, Action<string> /*action*/>();

            actions.ForEach(_ => grpActions[_.groupName] = _.action).Do();

            return lineSubparts.ForEach(part => grpActions[part.groupName](part.subpart));
        }

        public static IEnumerable<(string groupName, string subpart)> ForEachCase(this IEnumerable<(string groupName, string subpart)> lineSubparts, params (string groupName, Action<string, int> action)[] actions)
        {
            var grpActions = new Dictionary<string /*groupName*/, Action<string,int> /*action*/>();

            actions.ForEach(_ => grpActions[_.groupName] = _.action).Do();

            return lineSubparts.ForEach((part, idx) => grpActions[part.groupName](part.subpart, idx));
        }

        public static IEnumerable<(string groupName, string subpart)> ForEachCase(this IEnumerable<(string groupName, string subpart)> lineSubparts, params (string groupName, Action action)[] actions)
        {
            var grpActions = new Dictionary<string /*groupName*/, Action /*action*/>();

            actions.ForEach(_ => grpActions[_.groupName] = _.action).Do();

            return lineSubparts.ForEach(part => grpActions[part.groupName]());
        }

        public static IEnumerable<(string groupName, int startIndex, int Length)> Slices(this string line, Regxs regxs)
       => regxs.Slices(ref line);

        public static string toString(this IEnumerable<(string groupName, int startIndex, int Length)> slices, string line, params (string groupName, Func<string, string> transformation)[] transformations)
        {
            
            var rgxRequests = new Dictionary<string /*groupName*/, Func<string, string> /*transformation*/>();
            transformations.ForEach(_ => rgxRequests[_.groupName] = _.transformation).Do();
            return new StringBuilder().Build(line, slices, rgxRequests).ToString(); 
        }

        public static StringBuilder Build(this StringBuilder builder, string line, IEnumerable<(string groupName, int startIndex, int Length)> slices, RgxRequests transformations )
        {
            slices.ForEach(x =>
            {
                Func<string, string> map;
                if (transformations.TryGetValue(x.groupName, out map))
                {
                    if (x.groupName == UNMATCHED_LINE) builder.Append(map(line));
                    else builder.Append(map(line.Substring(x.startIndex, x.Length)));
                }
                else 
                {
                    if (x.groupName == UNMATCHED_LINE) builder.Append(line);
                    else builder.Append(builder.Append(line.AsSpan().Slice(x.startIndex, x.Length)));
                }
            })
            .Do();
            return builder;
        }

    }
}
