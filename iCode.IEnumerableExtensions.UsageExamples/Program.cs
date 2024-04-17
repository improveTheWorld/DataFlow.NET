using iCode.Extensions;
using iCode.TestTools.Fake;
using System.Text.RegularExpressions;
using iCode.Log;
using iCode.Framework;
using iCode.Data;
using static iCode.Framework.Regx;
using System.Linq;
using System.Text;

namespace iCode.IEnumerableExtensions.UsageExamples
{

    internal class Program
    {

        //public static IEnumerable<string> NewIEnumerable()
        //{
        //    string[] tmp = new string[0];
        //    return tmp;
        //}
        //public static IEnumerable<string> ten1(IEnumerable<string> items, IEnumerable<string> select)
        //{

        //    int index = 0;
        //    foreach (var item in items)
        //    {
        //        var line = item;
        //        if (index > 10 && index < 20)
        //        {
        //            Console.WriteLine(index);
        //            select.Append(line);
        //        }
        //        yield return item;
        //        index++;
        //    }
        //    select.Display();
        //}
        //public static IEnumerable<string> ten2(IEnumerable<string> items, IEnumerable<string> select)
        //{

        //    int index = 0;
        //    foreach (var item in items)
        //    {
        //        var line = item;
        //        if (index > 10 && index < 20)
        //        {
        //            Console.WriteLine(index);
        //            select.Append(line);
        //        }
        //        yield return item;
        //        index++;
        //    }
        //    select.Display();
        //}
        static void Main(string[] args)
        {
            //var Selected = NewIEnumerable();
            //fill(Read.text("log.txt"), Selected).Do();
            //Selected.Display();
            //csvSimpleExample();
            //textAdvancedExample();
            RgxsUsageExample();


        }





        static void RgxsUsageExample()
        {

            StreamReader textFile = new List<string>() { "123    abc", ",,,", "Hello, world!", "456 def" }.fakeFile();


            string pattern = OneOf(NUMS.As("numgroup"), ALPHAS.As("wordgroup"));
            Console.WriteLine(pattern);  //"(?<numgroup>\\d+)|(?<wordgroup>[a-zA-Z]+)"

            // Add transformation requests for specific group names
            // Regxs transformations = new Regxs(new Regex()));

            Data.Read.text(textFile)
                .Map(OneOf(NUMS.As("numgroup"), ALPHAS.As("wordgroup")))
                .SelectCase(
                    ("numgroup", s => $"Number: {s}"),
                    ("wordgroup", s => $"Word: {s}"),
                    (RegxsExt.UNMATCHED_LINE, s => $"---Line : {s.ToUpper()} -----"),
                    (RegxsExt.UNMATCHED_SLICE, s => $"<<{s.ToUpper()}>>")
                )
                .AllCases()
                .ForEach(x => x.Display())
                .Do();
                /*Display("Displaying :");*/

        //// Test case 1: Line matching the regex pattern

            //string result1 = transformations.Map(line1).SelectByGroup( ("numgroup", s => $"Number: {s}"), ("wordgroup", s => $"Word: {s}")); 
            //Console.WriteLine($"Original: {line1}");
            //Console.WriteLine($"Transformed: {result1}");
            //Console.WriteLine();

            //// Test case 2: Line not matching the regex pattern

            //string result2 = transformations.Build(line2).ToString();
            //Console.WriteLine($"Original: {line2}");
            //Console.WriteLine($"Transformed: {result2}");
            //Console.WriteLine();

            //// Test case 3: Line matching the regex pattern with missing group transformation

            //string result3 = transformations.Build(line3).ToString();
            //Console.WriteLine($"Original: {line3}");
            //Console.WriteLine($"Transformed: {result3}");
            //Console.WriteLine();

            //Console.ReadLine();
            /*
                Original: 123 abc
                Transformed: Number: 123 Word: abc


                Original: Hello, world!
                Transformed: HELLO, WORLD!


                Original: 456 def
                Transformed: Number: 456 def


                The output demonstrates the behavior of the RegrexTransformations class for different test cases:


                For the line "123 abc", which matches the regex pattern (\d+)\s+(\w+), the captured groups are transformed according to the specified transformations. Group "1" (the number) is prefixed with "Number: ", and group "2" (the word) is prefixed with "Word: ".

                For the line "Hello, world!", which does not match the regex pattern, the default transformation (converting the string to uppercase) is applied.

                For the line "456 def", which matches the regex pattern but has a missing group transformation (no transformation specified for group "2"), the captured group without a specified transformation is left unchanged. Only group "1" (the number) is transformed with the "Number: " prefix.
             */

            }
            //static void Usage2()
            //{
            //    // Define regular expressions and associated transformations
            //    var rgxs = new Regxs(new Regex(@"(\d+)"), "Number", num => $"Number: {num}")
            //                    .Add(new Regex(@"(\w+)"), "Word", word => $"Word: {word.ToUpper()}");

            //    // Apply regular expression matching and mapping
            //    var input = "Sample text with 123 and abc.";
            //    var output = rgxs.Build(input);

            //    Console.WriteLine($"Input: {input}");
            //    Console.WriteLine($"Output: {output}");
            //}







            //static void ReplaceExamples()
            //{
            //    const string group1 = "level";
            //    const string group2 = "instance";

            //    string pattern1 = @"\[" + Regx.ALPHNUMS.As(group1) + @"\]";
            //    string pattern2 = Regx.SPACES + @"\[" + "InstanceName".As(group2);
            //    string pattern = Regx.OneOf(pattern1, pattern2);

            //    string line1 = @"[   test ]e Info faefae Type2:";
            //    string line2 = @"[Info] in line2 test:";


            //    IEnumerable<string> lines = Data.Read.text(new List<string>() { line1, line2 }.fakeFile(), false);

            //    lines.Map(pattern)
            //    .SelectCase(
            //        (group1, x => $"GGGG1 : {x}"),
            //        (group2, x => $"GGGG2 : {x}"),
            //        (RegxsExt.UNMATCHED_SLICE, x => x.ToUpper())
            //     )
            //    .AllCases()
            //    .Display();
            //}


            //static void ReplaceByGroupExample()
            //{

            //    const string group1 = "level";
            //    const string group2 = "InstanceName";

            //    string path = @"C:\Users\Bilel_Alstom\Desktop\InDAb";
            //    string pattern1 = @"\[" + Regx.ALPHNUMS.As(group1) + @"\]";
            //    string pattern2 = Regx.SPACES + @"\[" + "".As(group2);
            //    string pattern = Regx.OneOf(pattern1, pattern2);

            //    var reg = new Regex(pattern);



            //    Directory.EnumerateFiles(path, "*.txt").
            //                Where(x => FilePath.GetName(x) != "log.txt")
            //                .ForEach(x => File.Delete(x))
            //                .Do();



            //    Data.Read.text("log.txt")
            //            .Map(reg, (group1, v => v.ToUpper()), (group2, _ => "++++++"))
            //            .Enumerate()
            //            .WriteInFile(path + "\\log_test_oussama.txt");
            //}

            //static void syntaxiUsageExample()
            //{

            //    // Define the grammar rules
            //    var rules = new Rule[]
            //    {
            //        new Rule("E", "T", "E'"),
            //        new Rule("E'", "+", "T", "E'"),
            //        new Rule("E'", ""),
            //        new Rule("T", "F", "T'"),
            //        new Rule("T'", "*", "F", "T'"),
            //        new Rule("T'", ""),
            //        new Rule("F", "(", "E", ")"),
            //        new Rule("F", "id")
            //    };

            //    // Build the grammar
            //    var grammar = GrammarElem.Builder.Build(rules);

            //    // Input tokens
            //    var tokens = new string[] { "id", "+", "id", "*", "id" };

            //    // Parse the tokens
            //    foreach (var token in tokens)
            //    {
            //        var result = grammar.AcceptToken(token);
            //        Console.WriteLine($"Token: {token}, Digestion: {result}");
            //    }
            // }

        struct Person
        {

            public string FirstName;
            public string Name;
            public int Age;
        }
        static void csvSimpleExample()
        {
            // Read lines from a CSV file, extract data, and fill into an Enumerable of Person
            Read.csv<Person>("People.csv", ",")
                // Convert names to uppercase
                .Select(p =>
                {
                    p.Name = p.Name.ToUpper();
                    return p;
                })
                // Rewrite into a new CSV file
                .WriteCSV("People_UpperCase.csv", true);

            // Note: The file People.csv is processed without being fully loaded into memory.
        }

        static void textAdvancedExample()
        {
            StreamWriter errorLogs = new("errors.txt");
            StreamWriter warningLogs = new("warnings.txt");
            StreamWriter InfoLogs = new("others.txt");

            // Read lines and categorize by log level
            Read.text("log.txt")
                .Until(line => line.StartsWith("STOP:"))
                .Cases(
                    line => line.ToUpper().Contains("ERROR"),
                    line => line.ToUpper().Contains("WARNING"),
                    line => true
                )
                // Apply suitable transformations for each category,
                // => Add Log level information at the begining of each line
                .SelectCase(
                    line => line = $"ERROR : {line}",   // for lines containing "error" 
                    line => line = $"WARNING : {line}", // for lines containing  "warning"
                    line => line = $"INFO : {line}"     // for other lines
                    )
                // Write each log level in a different file
                .ForEachCase(
                    line => errorLogs.WriteLine(line),
                    line => warningLogs.WriteLine(line),
                    line => InfoLogs.WriteLine(line)
                )
                // Re-Write a new log file with the level information added for all processed log lines
                .AllCases()
                .WriteText("log_WithLevel.txt");

            errorLogs.Close();
            warningLogs.Close();
            InfoLogs.Close();

            // Note: The log file is processed in a single pass,
            // Transformations, chained actions and the different write operations,
            // are applied on the fly, line by line,
            // enhancing performance, with optimized ressource usage.
        }
    }
}
 