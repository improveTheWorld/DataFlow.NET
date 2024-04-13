using iCode.Extensions;
using iCode.TestTools.Fake;
using System.Text.RegularExpressions;
using iCode.Log;
using iCode.Framework;
using iCode.Framework.Rgx;
using iCode.Data;

namespace iCode.IEnumerableExtensions.UsageExamples
{

    internal class Program
    {
        static void Main(string[] args)
        {
            csvSimpleExample();

        }
        static void textUsageExamples()
        {
            StreamWriter errorLogs = new("errors.txt");
            StreamWriter warningLogs = new("warnings.txt");
            StreamWriter InfoLogs = new("Infos.txt");

            // Read lines from a text file
            // Transform lines using IEnumerable extensions
            Read.text("textUsage.txt")
                .Select(line => line.ToUpper())
                .Until(line => line.StartsWith("STOP"))
                .Cases(
                    line => line.Contains("ERROR"),
                    line => line.Contains("WARNING"),
                    line => true
                )

                // Transform lines with different format depending on classification
                .SelectCase(
                    errorLine => $"Error: {errorLine}",
                    warningLine => $"Warning: {warningLine}",
                    infoLine =>$"Info: {infoLine}"
                )

                // Write errors, warnings and Infos lines in 3 different files 
                .ForEachCase(
                    line => errorLogs.WriteLine(line),
                    line => warningLogs.WriteLine(line),
                    line => InfoLogs.WriteLine(line)
                );

            // Note that the records are is looped just once:
            // Transformations and chained actions are applied on the fly, record by record

            errorLogs.Close();
            warningLogs.Close();
            InfoLogs.Close();          

        }
        static void csvSimpleExample()
        {
            // Test Read.csv method
            Read.csv<Person>("People.csv", ",")
            .Select(p =>
            {
                p.Name = p.Name.ToUpper();
                return p;
             })
            .Spy("CSV List :  ", x=>$"{ x.Name} {x.FirstName} : {x.Age}"," | ")
            .WriteCSV("People_UpperCasedName.csv", true);

            // Note that the records Enumerable is looped just once:
            // Transformations and chained actions are applied on the fly, record by record
        }

        static void csvAdvancedExamples()
        {
            // Test Read.csv method
            Read.csv<Person>("csvUsage.csv", ",")
                .Cases(
                p => p.Age < 18,
                p => p.Age >= 18 && p.Age < 65,
                p => p.Age >= 65
            )

            // Apply suitable transformation depending on classification
            .DoCase(
                p => p.Name = $"Minor: {p.Name}", 
                p => p.Name = $"Adult: {p.Name}",
                p => p.Name = $"Senior: {p.Name}"
            )
            // Write the new  transformed csv records
            .Select(x=>x.item).WriteCSV("Classified_persons.csv", true);

            // Note that the records Enumerable is looped just once:
            // Transformations and chained actions are applied on the fly, record by record

        }

        static void BasicManipulationExamples()
        {
            // Test Read.csv method with custom separator
            string csvPath = "BasicManipulation.csv";
            var records = Read.csv<Person>(csvPath);
                      
                     
            // Test Take first 3 with Take( startIdx, count)
           
            records.Take(0, 3)
                .Select(record => $"Name: '{record.Name}', Age: '{record.Age}'")
                .Display("Top 3");
            

            // Test Classify and SelectByClassification with index
            var classifiedRecords = records.Cases(
                p => p.Age < 18,
                p => p.Age >= 18 && p.Age < 65,
                p => p.Age >= 65
            );

            var selectedWithIndex = classifiedRecords.SelectCase(
                (p, idx) => $"Minor #{idx + 1}: {p.Name}",
                (p, idx) => $"Adult #{idx + 1}: {p.Name}",
                (p, idx) => $"Senior #{idx + 1}: {p.Name}"
            );
            
            selectedWithIndex.Select(x=>x.item).Display("Selected With index");

             // Test Cumul
             var cumulResult = records.Select(p => p.Age).Cumul((a, b) => a + b);
            Console.WriteLine("\nCumulative sum of ages: " + cumulResult);

            // Cumul all names 2 firsts letters in one word!
            var firstLetter = records.Select(p => p.Name).Cumul((a, b) => a.Substring(0,1) + b.Substring(0,1));
            Console.WriteLine("\nCumulative names initials : " + cumulResult);

        }


        static void RgxsUsageExample()
        {
            // Create an instance of RegrexTransformations with a default transformation
            Rgxs transformations = new Rgxs(s => s.ToUpper());

            // Define a regular expression pattern
            string pattern = @"(\d+)\s+(\w+)";
            Regex regex = new Regex(pattern);

            // Add transformation requests for specific group names
            transformations.Add(regex,
                ("1", s => $"Number: {s}"),
                ("2", s => $"Word: {s}")
            );

            // Test case 1: Line matching the regex pattern
            string line1 = "123 abc";
            string result1 = transformations.Map(line1);
            Console.WriteLine($"Original: {line1}");
            Console.WriteLine($"Transformed: {result1}");
            Console.WriteLine();

            // Test case 2: Line not matching the regex pattern
            string line2 = "Hello, world!";
            string result2 = transformations.Map(line2);
            Console.WriteLine($"Original: {line2}");
            Console.WriteLine($"Transformed: {result2}");
            Console.WriteLine();

            // Test case 3: Line matching the regex pattern with missing group transformation
            string line3 = "456 def";
            string result3 = transformations.Map(line3);
            Console.WriteLine($"Original: {line3}");
            Console.WriteLine($"Transformed: {result3}");
            Console.WriteLine();

            Console.ReadLine();
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
        static void Usage2()
        {
            // Define regular expressions and associated transformations
            var rgxs = new Rgxs()
                .Add(new Regex(@"(\d+)"), "Number", num => $"Number: {num}")
                .Add(new Regex(@"(\w+)"), "Word", word => $"Word: {word.ToUpper()}");

            // Apply regular expression matching and mapping
            var input = "Sample text with 123 and abc.";
            var output = rgxs.Map(input);

            Console.WriteLine($"Input: {input}");
            Console.WriteLine($"Output: {output}");
        }

  


        public struct Person
        {

            public string FirstName;
            public string Name;
            public int Age;
        }


        static void ReplaceExamples()
        {
            const string group1 = "level";
            const string group2 = "instance";

            string pattern1 = @"\[" + Regx.ALPHNUMS.As(group1) + @"\]";
            string pattern2 = Regx.SPACES + @"\[" + "InstanceName".As(group2);
            string pattern = Regx.OneOf(pattern1, pattern2);

            string line1 = @"[   test ]e Info faefae Type2:";
            string line2 = @"[Info] in line2 test:";


            var reg = new Regex(pattern);

            IEnumerable<string> lines = Data.Read.text(new List<string>() { line1, line2 }.AsStreamReader(), false);
            lines.Map(reg, (group1, x => x.ToUpper()), (group2, (x) => "____")).Enumerate();
            lines.Map(x => "OTHERS").Add(reg, group1, x => x.ToUpper())/*.Spy("Result")*/;
        }

        static void ReplaceByGroupExample()
        {

            const string group1 = "level";
            const string group2 = "InstanceName";

            string path = @"C:\Users\Bilel_Alstom\Desktop\InDAb";
            string pattern1 = @"\[" + Regx.ALPHNUMS.As(group1) + @"\]";
            string pattern2 = Regx.SPACES + @"\[" + "".As(group2);
            string pattern = Regx.OneOf(pattern1, pattern2);

            var reg = new Regex(pattern);



            Directory.EnumerateFiles(path, "*.txt")
                       .Where(x => FilePath.GetName(x) != "log.txt")
                       .ForEach(x => File.Delete(x));



            Data.Read.text(path + "\\log.txt")
                    .Map(reg, (group1, v => v.ToUpper()), (group2, _ => "++++++"))
                    .Enumerate()
                    .WriteInFile(path + "\\log_test_oussama.txt");
        }

        static void syntaxiUsageExample()
        {

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
        }
    }
}
 