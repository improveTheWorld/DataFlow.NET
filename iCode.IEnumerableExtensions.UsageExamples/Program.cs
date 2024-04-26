using iCode.Extensions;
using iCode.TestTools.Fake;
using System.Text.RegularExpressions;
using iCode.Log;
using iCode.Framework;
using iCode.Data;
using static iCode.Framework.Regx;
using static iCode.Framework.Regxs;
using System.Linq;
using System.Text;
using System.Security.Cryptography.X509Certificates;
using Confluent.Kafka;
using static iCode.Framework.FilePath;
using static System.Net.Mime.MediaTypeNames;
using System.Diagnostics;
using System;

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

            csvSimpleExample();
            textAdvancedExample();
            RgxsUsageExample();


        }




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
                    line => line.ToUpper().Contains("WARNING")
                )
                // Apply suitable transformations for each category,
                // => Add Log level information at the begining of each line
                .SelectCase(
                    line => line = $"ERROR : {line}",   // for lines containing "error" 
                    line => line = $"WARNING : {line}", // for lines containing  "warning"
                    line => line = $"INFO : {line}"  // for other lines, assume Info
                    )
                // Write each log level in a different file
                .ForEachCase(
                    line => errorLogs.WriteLine(line),
                    warningLogs.WriteLine,
                    InfoLogs.WriteLine //Assume default is Info
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


       
        public static void RgxsUsageExample()
        {

            var log = new List<string>() {
                                        "Low memory condition detected while running application, this is a warning",
                                        "Server Status OK,  Received Response : 500 serevr error",
                                        "Received Response : 200 Status ok" ,
                                        "Resource allocation exceeded for process ID 453." ,
                                        "Received Response : 404 not Found" }
                                        .PutInStream();


            int errorsCount = 0;


            // Read the log file and analyze each line
            Read.text(log)
                // Define regex patterns for different log entry types
                .Map($"Received Response : {NUMS.As("ErrorCode")} {WORDS.As("errorMessage")}")
                .Cases(
                    "ErrorCode",
                    "errorMessage"
                )
                .SelectCase(
                    code =>
                    {
                        if (code != "200") errorsCount++;
                        return code;
                    },
                    message => $"--{message.ToUpper()}--",
                    x => x
                )
                .AllCases()
                .ToLines(Environment.NewLine)
                .Display("New Log",Environment.NewLine);


            Console.WriteLine ();
            Console.WriteLine($"Number of errors : {errorsCount}");
           
        }

        //static void RgxsUsageExample()
        //{

        //    StreamReader textFile = new List<string>() { "123    abc", ",,,", "Hello, world!", "456 def" }.PutInStream();
        //    string pattern = OneOf(NUMS.As("numgroup"), ALPHAS.As("wordgroup"));


        //    Data.Read.text(textFile)
        //        .Map(OneOf(NUMS.As("numgroup"), ALPHAS.As("wordgroup")))
        //        .Cases(
        //            "numgroup",
        //            "wordgroup",
        //            UNMATCHED.EOF
        //        )
        //        .SelectCase(
        //            s => $"Number: {s}",
        //            s => $"Word: {s}",
        //            s => "-EOF-",
        //            x => x
        //        )
        //        .AllCases()/*.Spy("All Pieces")*/
        //        .ToLines("-EOF-")
        //        .Display();


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

    }

}
