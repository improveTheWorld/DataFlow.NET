using iCode.Extensions;
namespace iCode.TestTools.Fake
{
    public static  class InMemory
    {
        public static StreamReader fakeFile(this IEnumerable<string> lines)
        {
            MemoryStream stream = new MemoryStream();

            var writer = new StreamWriter(stream);
                
            lines.ForEach(line =>writer.WriteLine(line)).Do();
              
            writer.Flush(); // Ensures all data is written to the stream

            stream.Position = 0; // Reset the position to the beginning of the stream

            return new StreamReader(stream);                
            
        }
       
    }
}