using iCode.Extensions;
using iCode.TestTools.Fake;
using System.Text.RegularExpressions;
using iCode.Log;
using iCode.Framework;

namespace iCode.IEnumerableExtensions.UsageExamples
{  

   
    internal class Program
    {
        static void Main(string[] args)
        {
            ReplaceByGroupExample();
            ReplaceExamples();

        }

        static void ReplaceExamples() 
        {
            const string group1 = "mine";
            const string group2 = "others";

            string pattern1 = @"\[" + Rgx.ALPHNUMS.As("level") + @"\]";
            string pattern2 = Rgx.SPACES + @"\[" + "InstanceName".As("instance");
            string pattern = Rgx.OneOf(pattern1, pattern2);

            string line1 = @"[   test ]e Info faefae Type2:";
            string line2 = @"[Info] in line2 test:";


            var reg = new Regex(pattern);

            IEnumerable<string> lines = new List<string>() { line1, line2 }.AsStreamReader().AsLines(false);

            lines.Select(line => line.Find(reg).Replace((group1, v => v.ToUpper()), (group2, _ => "______")).Out("Result"));
            lines.Select(line => line.Find(reg).Replace((g, v) => g == group1 ? v.ToUpper() : "OTHERS")).Out("Result");
        }

        static void ReplaceByGroupExample()
        {

            const string group1 = "level";
            const string group2 = "InstanceName";

            string path = @"C:\Users\Bilel_Alstom\Desktop\InDAb";
            string pattern1 = @"\[" + Rgx.ALPHNUMS.As(group1) + @"\]";
            string pattern2 = Rgx.SPACES + @"\[" + "".As(group2);
            string pattern = Rgx.OneOf(pattern1, pattern2);

            var reg = new Regex(pattern);



            Directory.EnumerateFiles(path, "*.txt")
                       .Where(x => FilePath.GetName(x) != "log.txt")
                       .ForEach(x => File.Delete(x));

            

            (path + "\\log.txt")
                    .AsLines()
                    .Select(line => line.Find(reg).Replace((group1, v => v.ToUpper()), (group2, _ => "++++++")))
                    .WriteInFile(path + "\\log_test_oussama.txt");
        }
    }
} 