using iCode.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace iCode.Framework.Rgx
{

    using RgxRequests = Dictionary<ValueTuple<Regex /*regex*/, string /*groupName*/>, Func<string, string> /*transformation*/>;
    public class Rgxs
    {
        RgxRequests regexRequests = new RgxRequests();
        HashSet<Regex> regexs = new HashSet<Regex>();
        Func<string, string>? defaultMap;
        public Rgxs(Func<string,string> map = null)
        {
            defaultMap = map;
        }

        public Rgxs Add(Regex regex, string groupName, Func<string, string> map)
        {
            regexs.Add(regex);
            regexRequests.Add((regex, groupName), map);
            return this;
        }
        public Rgxs Add(Regex regex, params (string /*groupName*/, Func<string,string> /*map*/)[] requests)
        {
            regexs.Add(regex);
            foreach (var request in requests)
            {
                regexRequests.Add((regex,request.Item1),request.Item2);
            }
            return this;
        }
        public string Map(string line)
        {
            foreach(var regex in regexs)
            {
                var result = regex.Matches(line)
                        .Where(x => x.Success)
                        .SelectMany<Match, Group>(match => match.Groups)
                        .Where(group => group.Name != "0");


               // Case regex matched
                if (!result.IsNullOrEmpty())
                {
                    int parserIndex = 0;
                    StringBuilder resultBuilder = new();

                    getCapturesSortedByIndex(result).ForEach(_ =>
                    {
                        Func<string, string> replace = regexRequests[(regex, _.groupName)];
                   
                        resultBuilder.Append(line.AsSpan().Slice(parserIndex, _.capture.Index - parserIndex))
                                .Append(replace == null ? _.capture.Value : replace( _.capture.Value));
                        parserIndex = _.capture.Index + _.capture.Length;
                    });

                    return resultBuilder
                            .Append(line.AsSpan().Slice(parserIndex))
                            .ToString();
                }                      
            }

            //case line does not matchs any regex

            //if a default map was defined apply it
            if (defaultMap != null)
            {
                return defaultMap(line);
            }
            else // return line without map
            {
                return line;
            }
            
        }

        // 
        static IEnumerable<(string groupName, Capture capture)> getCapturesSortedByIndex(IEnumerable<Group> result)
        {
            Dictionary<int, (string groupName, Capture capture)> inDict =
                new(result
                    .SelectMany(group => group.Captures.Select(cap => new KeyValuePair<int, (string groupName, Capture capture)>(cap.Index, (group.Name, cap)))));
            return (new SortedList<int, (string groupName, Capture capture)> (inDict)).Values;
        }

    }
}
