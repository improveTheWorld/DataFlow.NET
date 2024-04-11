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
            lines.Map(x => "OTHERS").Add(reg, group1, x => x.ToUpper()).Enumerate().Out("Result");
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
    }
} 