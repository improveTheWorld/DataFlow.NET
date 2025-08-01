﻿using DataFlow.Extensions;
using System.Text;

namespace DataFlow.Framework
{
    public static class RegexWrap

    {
       
        public const string CHAR = ".";  //  Any character except new line.
        public const string SPACE = @"\s"; // space or tab
        public const string ALPHNUM = @"\w"; // A-Z, a-z , 0-9 or _ (underscore) .
        public const string NUM = @"\d"; // digits
        public const string ALPHA = "[a-zA-Z]"; // space or tab

        public const string ANY_CHARS = ".*";  //  Any character except new line.
        public const string SPACES = @"\s+"; // space or tab(0 or many)
        public const string MAYBE_SPACES = @"\s*"; // space or tab(0 or many)
        public const string ALPHNUMS = @"\w+"; // A-Z, a-z , 0-9 or _ (underscore) . One or plus
        public const string NUMS = @"\d+"; // digits
        public const string ALPHAS = "[a-zA-Z]+"; // Alphabetic
        public const string WORD = MAYBE_SPACES + ALPHNUMS + MAYBE_SPACES;
        public static string WORDS = MAYBE_SPACES + ALPHNUMS + Many(SPACE + ALPHNUMS) + MAYBE_SPACES;


        public static string Group(this string input)
        {
            Guard.AgainstNullArgument(nameof(input), input);

            if (input.Length == 1 && input != ")" && input != "(") return input;
            if (input.Length == 2 && input[0] == '\\' && input != ")" && input != "(") return input;

            if (input.Length >= 3 && (input.IsBetween("(", ")") || input.IsBetween("[", "]")))
            {
                int count1 = 0;
                int count2 = 0;

                // verify that we dont have expression like "() ..()" or "[]..[]"
                input.Where((_, idx) => 0 < idx && idx < input.Length - 2)
                        .Cases(
                            x => x == '[',
                            x => x == ']',
                            x => x == '(',
                            x => x == ')')                        
                        .ForEachCase(
                           () => count1++,
                           () => count1--,
                           () => count2++,
                           () => count2--)
                         .Until(() => count1 < 0 || count2 < 0 )
                        .Do();

                if (count1 != 0 || count2 !=0) throw new ArgumentException($"{nameof(input)}  Check  parantheses: {input}");

                // count == 0
                return input;
            }

            return $"(?:{input})";
        }
        public static string Any(this string input) => $"{input.Group()}*"; //zero and plus
        public static string Many(this string input) => $"{input.Group()}+"; //one or plus
        public static string MayBe(this string input) => $"{input.Group()}?";  //zero or one
        public static string As(this string input, string groupName = "") => groupName.IsNullOrEmpty() ? $"({input})" : $"(?<{groupName}>{input})";
        public static string OneOf(params string[] parameters) => parameters.Aggregate((a, b) => $"{a}|{b}");
        public static string OneOf(params char[] parameters) => $"[{parameters.Select(x => x.ToString()).Aggregate((a, b) => a + b)}]";
        public static string Many(this string input, int Limit_inf, int limit_sup) => $"{input.Group()}{{{Limit_inf},{limit_sup}}}"; // between limit_inf and limit_sup times
        public static string InSpaces(this string input) => SPACES + input + SPACES;
        public static string Words(int nbrWords)
        {
            StringBuilder resultBuilder = new();
            while (nbrWords > 0) { resultBuilder.Append(SPACES + ALPHNUMS); nbrWords--; };
            return resultBuilder.Append(SPACES).ToString();
        }
    }
}
