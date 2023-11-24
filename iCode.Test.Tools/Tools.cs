using iCode.Extensions;
namespace iCode.TestTools.Fake
{
    public static  class Fake
    {
        public static StreamReader StreamReader(List<string> lines)
        {
            MemoryStream stream = new MemoryStream();

            var writer = new StreamWriter(stream);
                
            lines.ForEach(line =>writer.WriteLine(line));
              
            writer.Flush(); // Ensures all data is written to the stream

            stream.Position = 0; // Reset the position to the beginning of the stream

            return new StreamReader(stream);

                
            
        }
    }
}