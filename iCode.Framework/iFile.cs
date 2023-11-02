namespace iCode.Framework
{   
    public class iFile : StreamReader
    {
        public FilePath Path;

        public iFile(string path) : base(path)
        {
            Path = new FilePath(path);
        }


        public iFile(string directory, string fileName) : base(System.IO.Path.Combine(directory, fileName))
        {

            Path = new FilePath(directory, fileName);
        }
    }
}
