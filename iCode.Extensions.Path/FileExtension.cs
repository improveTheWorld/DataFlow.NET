
namespace iCode.Extensions
{
    public static class iFile
    {

        public static StreamWriter SafeStreamWriter(string path, string renameSuffixIfExist = ".old")
        {
            if(File.Exists(path))
            {
                Rename(path, renameSuffixIfExist);
            }
            return new StreamWriter(path);
        }
        public static bool Rename(string path, string suffix, bool overwrite = false )
        {
            int index;
            return Rename(path, fileName => (index = fileName.LastIndexOf('.')) == -1 ? fileName + suffix : fileName.Insert(index,suffix) , overwrite);
        }
        public static bool Rename(string path, Func<string, string> rename, bool overwrite = false)
        {
            bool exist = true;
            do {

                path = rename(path);
               
            } while (overwrite || !(exist = File.Exists(path)));

            File.Move(path.ToString(), path, overwrite);
            // return File.Exist(newPath)
            return overwrite || !exist; // return true if a file was created . equivalent to : return File.Exist(path)
        }
    }
}