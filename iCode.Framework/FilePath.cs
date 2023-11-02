using System.IO;

namespace iCode.Framework
{
    public class FilePath
    {
        public enum Status
        {
            
            MissedPath = 0,
            PathExist = 1,
            FileExist = 2
        } 

        public string? DirectoryPath { get; set; }

        public string Name { get; set; }

        string? _FullPath = null;
        public string FullPath => (_FullPath != null) ? _FullPath : (_FullPath = Path.Combine(DirectoryPath, Name));
        

        public FilePath(string path, string name)
        {
            DirectoryPath = path;
            Name = name;
        }

        public FilePath(string fullPath)
        {
            string? directory = Path.GetDirectoryName(fullPath);

            if (directory == null)
            {
                throw new ArgumentException(nameof(fullPath));
            }
            else
            {
                DirectoryPath = directory;
            }
                
            Name = Path.GetFileName(fullPath);
        }

       
        public Status Check()
        {
            return Check(DirectoryPath,Name);       
        }
        
        public StreamWriter CreatePathAndFile(string? suffixToRenameIfExistant = null)
        {
            return _createPathAndFile(DirectoryPath, FullPath);
        }
        public static StreamWriter CreatePathAndFile(string fullPath, string? suffixToRenameIfExistant = null)
        {
            return _createPathAndFile(Path.GetDirectoryName(fullPath), fullPath, suffixToRenameIfExistant);
        }

        public static StreamWriter CreatePathAndFile(string directoryPath, string name, string? suffixToRenameIfExistant = null)
        {
            return _createPathAndFile(directoryPath,Path.Combine(directoryPath, name), suffixToRenameIfExistant);
        }

        static StreamWriter _createPathAndFile(string directoryPath, string fullPath, string? suffixToRenameIfExistant = null)
        {
            Status status = _check(directoryPath,fullPath);

            if (status == Status.MissedPath)
            {
                Directory.CreateDirectory(directoryPath);
            }
            else if (status == Status.FileExist && suffixToRenameIfExistant != null)
            {
                File.Move(fullPath, Path.Combine(directoryPath, Path.GetFileNameWithoutExtension(fullPath) + suffixToRenameIfExistant + Path.GetExtension(fullPath)));
            }

            StreamWriter retValue = new StreamWriter(fullPath);
            retValue.Flush();
            return retValue;
        }


        StreamWriter? Ecrase()
        {
            if (File.Exists(FullPath))
            {
                return new StreamWriter(FullPath);
            }
            else
            {
                return null;
            }
        }

        public static Status Check(string fullPath)
        {
            return _check(Path.GetDirectoryName(fullPath), fullPath);
        }

        public static Status Check(string DirectoryPath, string name)
        {
            return _check(DirectoryPath, Path.Combine(DirectoryPath, name));
        }


        static Status _check(string directoryPath, string fullPath)
        {
            if (File.Exists(fullPath))
            {
                return Status.FileExist;
            }
            else if (Directory.Exists(directoryPath))
            {
                return Status.PathExist;
            }
            else
            {
                return Status.MissedPath;
            }
        }

        override public string? ToString()
        {
            return FullPath;
        }

    }
}
