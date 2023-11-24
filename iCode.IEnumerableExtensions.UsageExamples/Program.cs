using Confluent.Kafka.Admin;
using iCode.Extensions;
using iCode.TestTools.Fake;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using iCode.Framework;
using System;

namespace iCode.Extensions
{
    public static class Rgx
    {
        public const string CHAR  = ".";  //  Any character except new line.
        public const string SPACE = @"\s"; // space or tab
        public const string ALPHNUM = @"\w"; // A-Z, a-z , 0-9 or _ (underscore) .
        public const string NUM = @"\d"; // digits
        public const string ALPHA = "[a - zA - Z]"; // space or tab

        public const string BLABLA = ".*";  //  Any character except new line.
        public const string SPACES = @"\s*"; // space or tab(0 or many)
        public const string ALPHNUMS = @"\w+"; // A-Z, a-z , 0-9 or _ (underscore) . One or plus
        public const string NUMS = @"\d+"; // digits
        public const string ALPHAS = "[a - zA - Z]*"; // space or tab
        public const string WORD = SPACES + ALPHNUMS;
  

        //public static string ZeorOrMany(string input) => input + "*";
        //public static string OneOrMany(string input) => input + "*";

        public static string Group( this string input)
        {
            Guard.AgainstNullArgument(nameof(input), input);

            if(input.Length == 1 && input!=")" && input != "(") return input;
            if(input.Length == 2 && input[0] == '\\' && input != ")" && input != "(") return input;

            if(input.Length>=3 && ( input.StartsEnds("(",")") ||  input.StartsEnds("[","]" )) )
            {
                int count = 0;

                // verify that we dont have expression like "() ..()" or "[]..[]"
                input.Where((_,idx)=> 0< idx && idx< input.Length-2)
                        .Classify(x => x == '[', x => x == ']')
                        .ForEachByClassification(_ => count++, x => count++)
                        .Until ( (_,_) => count < 0) ;

                if (count != 0)   throw new ArgumentException($"{nameof(input)}  Check  parantheses: {input}");

                // count == 0
                return  input;                        
            }

            return $"(:{input})"; 
        }
        public static string Any(this string input) => $"{input.Group()}*"; //zero and plus
        public static string Many(this string input) => $"{input.Group()}+"; //one or plus
        public static string May(this string input) => $"{input.Group()}?";  //zero or one
        public static string Get(this string input, string groupName = "") =>  groupName.IsNullOrEmpty()?  $"({input})" : $"(?<{groupName}>{input})";
        public static string OneOf(this string input,params string[] parameters) => input + parameters.Cumul((a,b)=> $"{a}|{b}");
        public static string OneOf(this string input, params char[] parameters) => input +  $"[{parameters.Select(x=>x.ToString()).Cumul((a, b) => a+b)}]";
        public static string Many(this string input, int Limit_inf, int limit_sup) => $"{input.Group()}{{{Limit_inf},{limit_sup}}}"; // between limit_inf and limit_sup times
        public static string InSpaces(this string input) => SPACES + input + SPACES; 

    }
}

namespace iCode.IEnumerableExtensions.UsageExamples
{
    using iCode.Extensions;
    internal class Program
    {
        
        static void Main(string[] args)
        {
            int count =0;
            string[] input = new string[40000];
            Directory.EnumerateFiles(@"C:\Users\Bilel_Alstom\Desktop\codeSource\iCode", "*.cs", SearchOption.AllDirectories).SelectMany(x => new StreamReader(x).AsLines()).ForEach(line => { input[count] = line;    Console.WriteLine(count++); }).Go();
            
            //string line1 = " myFreind : hisName";
            //string line2 = "value: value1";

            //var str = Rgx.BLABLA + ":" + Rgx.ALPHNUMS.Get("mine").InSpaces();

            //Console.WriteLine(str);
            //var rgx = new Regex(str);
            //var result =rgx.Match(line1+line2);
            //var tmp = result.Groups["mine"].Value;


            //using (var reader = Fake.StreamReader(new List<string> {line1,line2 }))
            //{
            //    reader.AsLines().Select(line => rgx.Match(line).Captures[0].Value).ForEach(x=>Console.WriteLine(x));
            //}
        }
    }
}