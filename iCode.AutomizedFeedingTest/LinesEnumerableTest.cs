using iCode.Framework;
using iCode.Extentions.IEnumerableExtentions;
using Xunit;
using System.IO;
using System;

namespace iCode.Tests
{
    public class LinesEnumerableTest
    {
        [Fact]
        void CreateAndParseStreamReader()
        {
            MemoryStream  memoryStream = new MemoryStream();
            StreamWriter streamWriter = new StreamWriter(memoryStream);

            string line1 = "this is line 1";
            string line2 = "this is line 2";
            string line3 = "this is line 3";
            string line4 = "this is line 4";

            streamWriter.WriteLine(line1);
            streamWriter.WriteLine(line2);
            streamWriter.WriteLine(line3);
            streamWriter.WriteLine(line4);

            streamWriter.Flush();
            streamWriter.BaseStream.Position = 0;

            StreamReader reader = new StreamReader(memoryStream);  

           
            string allLines1 = "";
            string allLines2 = "";
            FileEnumerable Lines = new FileEnumerable(reader);
            foreach (string line in Lines)
            {
                allLines1 += line;
            }

            foreach (string line in Lines)
            {
                allLines2 += line;
            }
            
            Assert.Equal(line1+line2+line3+line4, allLines2);
            Assert.Equal(allLines1, allLines2);

            allLines1 = "";
            int count = 0;
            Lines.ApplyForeach(new Action<string>[]{ (x => allLines1 += x),(x=>count++)});
            Assert.Equal(line1 + line2 + line3 + line4, allLines1);
            Assert.Equal(4,count);
        }

        [Fact]
        void CreateAndParseEmptyStreamReader()
        {
            MemoryStream memoryStream = new MemoryStream();
            StreamWriter streamWriter = new StreamWriter(memoryStream);

           
            StreamReader reader = new StreamReader(memoryStream);


            string allLines1 = "";
            string allLines2 = "";
            FileEnumerable Lines = new FileEnumerable(reader);
            foreach (string line in Lines)
            {
                allLines1 += line;
            }

            foreach (string line in Lines)
            {
                allLines2 += line;
            }

            Assert.Equal("", allLines2);
            Assert.Equal("", allLines1);
        }

        [Fact]
        void CreateAndParseEmptyLinesStreamReader()
        {
            MemoryStream memoryStream = new MemoryStream();
            StreamWriter streamWriter = new StreamWriter(memoryStream);
            streamWriter.WriteLine("");
            streamWriter.WriteLine("");
            streamWriter.WriteLine("");
            streamWriter.WriteLine("");


            streamWriter.Flush();
            streamWriter.BaseStream.Position = 0;

            StreamReader reader = new StreamReader(memoryStream);
            string allLines1 = "";
            string allLines2 = "";
            FileEnumerable Lines = new FileEnumerable(reader);
            int count = 0;
            int count2 = 0;

            Lines.ApplyForeach(new Action<string>[] { x => allLines1 += x, x => count++ });
            Lines.ApplyForeach(new Action<string>[] { x => allLines2 += x, x => count2++ });
   
                                  
            Assert.Equal(4, count2);
            Assert.Equal(4, count);

            Assert.Equal("", allLines2);
            Assert.Equal("", allLines1);


           
        }
    }
}
