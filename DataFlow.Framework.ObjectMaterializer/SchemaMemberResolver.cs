using DataFlow.Framework;
using System.Text;

internal static class SchemaMemberResolver
{
    public sealed class Result
    {
        public int[] Map { get; }
        public string[] UnresolvedSchema { get; }
        public Result(int[] map, string[] unresolved)
        {
            Map = map;
            UnresolvedSchema = unresolved;
        }
    }
    public static string[] ResolveSchemaToMembers<T>(string[] schema)
    {
        // Get member names from the plan once (properties + fields)
        var plan = MemberMaterializationPlanner.Get<T>();
        var memberNames = plan.Members.Select(m => m.Name).ToArray();

        var res = Resolve(schema, memberNames, allowFuzzyResemblance: true, maxLevenshtein: 2);

        // Build a new schema array: for each original schema[i], if mapped -> use member name; else keep original
        var resolved = new string[schema.Length];
        for (int i = 0; i < schema.Length; i++)
        {
            var mi = res.Map[i];
            resolved[i] = (mi >= 0) ? memberNames[mi] : schema[i];
        }
        return resolved;
    }
    private static Result Resolve(
        IReadOnlyList<string> schema,
        IReadOnlyList<string> memberNames,
        bool allowFuzzyResemblance = true,
        int maxLevenshtein = 2)
    {
        int n = schema.Count;
        int m = memberNames.Count;
        var map = new int[n];
        Array.Fill(map, -1);

        var schemaInfo = new NameInfo[n];
        for (int i = 0; i < n; i++) schemaInfo[i] = NameInfo.Create(schema[i]);

        var memberInfo = new NameInfo[m];
        for (int j = 0; j < m; j++) memberInfo[j] = NameInfo.Create(memberNames[j]);

        var exactDict = BuildIndex(memberInfo, ni => ni.Original, caseSensitive: true);
        var ciDict = BuildIndex(memberInfo, ni => ni.Original, caseSensitive: false);
        var normDicts = new[]
        {
            BuildIndex(memberInfo, ni => ni.NoSpacesNoApos, false),
            BuildIndex(memberInfo, ni => ni.SnakeCase, false),
            BuildIndex(memberInfo, ni => ni.CamelCase, false),
            BuildIndex(memberInfo, ni => ni.Lower, false),
        };

        var usedMembers = new bool[m];

        // Pass 1: exact
        ResolvePass(schemaInfo, memberInfo, map, usedMembers, i => Lookup(exactDict, schemaInfo[i].Original));

        // Pass 2: case-insensitive
        ResolvePass(schemaInfo, memberInfo, map, usedMembers, i => Lookup(ciDict, schemaInfo[i].Original));

        // Pass 3: normalized variants
        foreach (var dict in normDicts)
            ResolvePass(schemaInfo, memberInfo, map, usedMembers, i => LookupMany(dict, schemaInfo[i].Candidates));

        // Pass 4: resemblance (prefix/suffix/contains)
        if (allowFuzzyResemblance)
            ResolveByResemblance(schemaInfo, memberInfo, map, usedMembers);

        // Pass 5: edit distance (optional)
        if (allowFuzzyResemblance && maxLevenshtein >= 0)
            ResolveByLevenshtein(schemaInfo, memberInfo, map, usedMembers, maxLevenshtein);

        var unresolved = new List<string>();
        for (int i = 0; i < n; i++)
            if (map[i] == -1) unresolved.Add(schema[i]);

        return new Result(map, unresolved.ToArray());
    }

    private static void ResolvePass(
        IReadOnlyList<NameInfo> schemaInfo,
        IReadOnlyList<NameInfo> memberInfo,
        int[] map,
        bool[] used,
        Func<int, IReadOnlyList<int>> candidateProvider)
    {
        for (int i = 0; i < schemaInfo.Count; i++)
        {
            if (map[i] != -1) continue;
            var candidates = candidateProvider(i);
            if (candidates.Count == 0) continue;

            int chosen = -1;
            foreach (var idx in candidates)
            {
                if (!used[idx])
                {
                    if (chosen == -1) chosen = idx;
                    else if (TieBreak(memberInfo, idx, chosen))
                        chosen = idx;
                }
            }
            if (chosen != -1)
            {
                map[i] = chosen;
                used[chosen] = true;
            }
        }
    }

    private static void ResolveByResemblance(
        IReadOnlyList<NameInfo> schemaInfo,
        IReadOnlyList<NameInfo> memberInfo,
        int[] map,
        bool[] used)
    {
        for (int i = 0; i < schemaInfo.Count; i++)
        {
            if (map[i] != -1) continue;
            var s = schemaInfo[i];

            int best = -1;
            int bestScore = int.MinValue;

            for (int j = 0; j < memberInfo.Count; j++)
            {
                if (used[j]) continue;
                var m = memberInfo[j];

                int score = 0;
                if (m.Lower.StartsWith(s.Lower) || s.Lower.StartsWith(m.Lower)) score += 5;
                if (m.Lower.EndsWith(s.Lower) || s.Lower.EndsWith(m.Lower)) score += 5;
                if (m.Lower.Contains(s.Lower) || s.Lower.Contains(m.Lower)) score += 3;
                score += TokenOverlapScore(s.Tokens, m.Tokens);

                if (score > bestScore || (score == bestScore && TieBreak(memberInfo, j, best)))
                {
                    bestScore = score;
                    best = j;
                }
            }

            if (best != -1 && bestScore >= 4)
            {
                map[i] = best;
                used[best] = true;
            }
        }
    }

    private static void ResolveByLevenshtein(
        IReadOnlyList<NameInfo> schemaInfo,
        IReadOnlyList<NameInfo> memberInfo,
        int[] map,
        bool[] used,
        int maxDistance)
    {
        for (int i = 0; i < schemaInfo.Count; i++)
        {
            if (map[i] != -1) continue;
            var s = schemaInfo[i].Lower;

            int best = -1;
            int bestDist = int.MaxValue;

            for (int j = 0; j < memberInfo.Count; j++)
            {
                if (used[j]) continue;
                var m = memberInfo[j].Lower;
                int d = Levenshtein(s, m);
                if (d < bestDist || (d == bestDist && TieBreak(memberInfo, j, best)))
                {
                    bestDist = d;
                    best = j;
                }
            }

            if (best != -1 && bestDist <= maxDistance)
            {
                map[i] = best;
                used[best] = true;
            }
        }
    }

    private static bool TieBreak(IReadOnlyList<NameInfo> members, int cand, int cur)
    {
        if (cur == -1) return true;
        var a = members[cur].Lower;
        var b = members[cand].Lower;
        return b.Length < a.Length || (b.Length == a.Length && string.CompareOrdinal(b, a) < 0);
    }

    private static Dictionary<string, List<int>> BuildIndex(
        IReadOnlyList<NameInfo> items,
        Func<NameInfo, string> keySelector,
        bool caseSensitive)
    {
        var dict = caseSensitive
            ? new Dictionary<string, List<int>>(StringComparer.Ordinal)
            : new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < items.Count; i++)
        {
            var key = keySelector(items[i]);
            if (string.IsNullOrEmpty(key)) continue;
            if (!dict.TryGetValue(key, out var list))
                dict[key] = list = new List<int>(1);
            list.Add(i);
        }
        return dict;
    }

    private static IReadOnlyList<int> Lookup(Dictionary<string, List<int>> dict, string key)
        => dict.TryGetValue(key, out var list) ? (IReadOnlyList<int>)list : Array.Empty<int>();

    private static IReadOnlyList<int> LookupMany(Dictionary<string, List<int>> dict, IReadOnlyList<string> keys)
    {
        if (keys.Count == 0) return Array.Empty<int>();
        List<int>? acc = null;
        for (int i = 0; i < keys.Count; i++)
        {
            var k = keys[i];
            if (dict.TryGetValue(k, out var list))
            {
                acc ??= new List<int>(list.Count);
                acc.AddRange(list);
            }
        }
        return acc ?? (IReadOnlyList<int>)Array.Empty<int>();
    }

    private static int TokenOverlapScore(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        int score = 0;
        for (int i = 0; i < a.Count; i++)
            for (int j = 0; j < b.Count; j++)
                if (a[i] == b[j]) score += 1;
        return score;
    }

    private sealed class NameInfo
    {
        public string Original { get; private set; } = "";
        public string Lower { get; private set; } = "";
        public string NoSpacesNoApos { get; private set; } = "";
        public string SnakeCase { get; private set; } = "";
        public string CamelCase { get; private set; } = "";
        public IReadOnlyList<string> Tokens => _tokens;
        public IReadOnlyList<string> Candidates
        {
            get
            {
                if (_candidates == null) _candidates = BuildCandidates();
                return _candidates;
            }
        }

        private string[] _tokens = Array.Empty<string>();
        private IReadOnlyList<string>? _candidates;

        public static NameInfo Create(string name)
        {
            var ni = new NameInfo();
            ni.Original = name ?? "";
            ni.Lower = ni.Original.ToLowerInvariant();
            ni.NoSpacesNoApos = RemoveChars(ni.Lower, ' ', '\'');
            ni._tokens = Tokenize(ni.Original);
            ni.SnakeCase = ToSnake(ni.Original).ToLowerInvariant();
            ni.CamelCase = ToCamel(ni.Original).ToLowerInvariant();
            return ni;
        }

        private IReadOnlyList<string> BuildCandidates()
        {
            // Keep as List<string> during build, return as IReadOnlyList<string>
            var list = new List<string>(5);

            void Add(string s)
            {
                if (!string.IsNullOrEmpty(s) && !ContainsIgnoreCase(list, s))
                    list.Add(s);
            }

            Add(Original);
            Add(Lower);
            Add(NoSpacesNoApos);
            Add(SnakeCase);
            Add(CamelCase);

            return list;
        }

        private static bool ContainsIgnoreCase(List<string> list, string s)
        {
            for (int i = 0; i < list.Count; i++)
                if (string.Equals(list[i], s, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        private static string RemoveChars(string s, params char[] chars)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                var c = s[i];
                bool skip = false;
                for (int j = 0; j < chars.Length; j++)
                    if (c == chars[j]) { skip = true; break; }
                if (!skip) sb.Append(c);
            }
            return sb.ToString();
        }

        private static string[] Tokenize(string s)
        {
            if (string.IsNullOrEmpty(s)) return Array.Empty<string>();
            var parts = new List<string>();
            var word = new StringBuilder();

            void Flush()
            {
                if (word.Length > 0)
                {
                    parts.Add(word.ToString().ToLowerInvariant());
                    word.Clear();
                }
            }

            for (int i = 0; i < s.Length; i++)
            {
                char ch = s[i];
                if (char.IsLetterOrDigit(ch))
                {
                    if (word.Length > 0 && char.IsUpper(ch) && char.IsLower(word[word.Length - 1]))
                        Flush();
                    word.Append(ch);
                }
                else
                {
                    Flush();
                }
            }
            Flush();
            return parts.ToArray();
        }

        private static string ToSnake(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new StringBuilder(s.Length + 8);
            char prev = '\0';
            for (int i = 0; i < s.Length; i++)
            {
                var c = s[i];
                if (char.IsLetterOrDigit(c))
                {
                    if (prev != '\0' && char.IsUpper(c) && char.IsLower(prev))
                        sb.Append('_');
                    sb.Append(c);
                    prev = c;
                }
                else if (prev != '_')
                {
                    sb.Append('_');
                    prev = '_';
                }
            }
            return sb.ToString().Trim('_');
        }

        private static string ToCamel(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var tokens = Tokenize(s);
            if (tokens.Length == 0) return s;
            var sb = new StringBuilder();
            sb.Append(tokens[0].ToLowerInvariant());
            for (int i = 1; i < tokens.Length; i++)
            {
                var t = tokens[i];
                if (t.Length == 0) continue;
                sb.Append(char.ToUpperInvariant(t[0]));
                if (t.Length > 1) sb.Append(t.AsSpan(1));
            }
            return sb.ToString();
        }
    }

    private static int Levenshtein(string a, string b)
    {
        if (a == b) return 0;
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;

        int n = a.Length, m = b.Length;
        var prev = new int[m + 1];
        var cur = new int[m + 1];

        for (int j = 0; j <= m; j++) prev[j] = j;

        for (int i = 1; i <= n; i++)
        {
            cur[0] = i;
            char ca = a[i - 1];
            for (int j = 1; j <= m; j++)
            {
                int cost = ca == b[j - 1] ? 0 : 1;
                cur[j] = Math.Min(
                    Math.Min(cur[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
            }
            var tmp = prev; prev = cur; cur = tmp;
        }

        return prev[m];
    }
}
