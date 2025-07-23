using DataFlow.Extensions;

namespace DataFlow.Framework;

public class FilePath
{
    public enum Status
    {
        MissedPath,
        MissedFile,
        File,
        Folder
    }
    Status _status = Status.MissedFile;
    public Status status
    {
        get
        {
            if (_status == Status.MissedFile)
            {
                if (File.Exists(FullName))
                {
                    _status = Status.File;
                }
                else if (Directory.Exists(FullName))
                {
                    _status = Status.Folder;
                }
                else if (!Directory.Exists(Up()))
                {
                    _status = Status.MissedPath;
                }
            }

            return _status;
        }
    }

    string[]? _Names;

    string[] Names
    {
        get => _Names != null ? _Names : _Names = explode(FullName);
    }

    static string[] explode(string fullName) =>
        fullName.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

    public string Name
    {
        get => Names.Last();
    }

    public string Up(int level = 1, bool computeFullPath = true)
    {
        int lastOne = Names.LastIdx() - level;
        return computeFullPath ? Path.Combine(Names.Until(lastOne).ToArray()) : Names[lastOne];
    }

    public static string Up(string fullPath, int level = 1, bool computeFullPath = true)
    {
        var tmp = fullPath.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        int lastOne = tmp.LastIdx() - level;
        return computeFullPath ? Path.Combine(tmp.Until(lastOne).ToArray()) : tmp[lastOne];
    }

    public readonly string FullName;
    public FilePath(string fullName)
    {
        FullName = fullName;
    }
    static public string GetName(string fullPath)
    {
        return Path.GetFileName(fullPath);
    }
    static public Status Check(string path)
    {
        if (File.Exists(path))
        {
            return Status.File;
        }
        else if (Directory.Exists(path))
        {
            return Status.Folder;
        }
        else if (!Directory.Exists(Path.GetDirectoryName(path)))
        {
            return Status.MissedPath;
        }
        else
        {
            return Status.MissedFile;
        }
    }

    static public bool IsFormatOk(string path)
    {
        if (String.IsNullOrWhiteSpace(path)
            || path.IndexOfAny(Path.GetInvalidPathChars()) >= 0
            || Path.GetFileName(path).IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            return false;
        }

        return true;
    }
  
    public static bool Rename(string path, string suffix, bool overwrite = false)
    {
        int index;
        return Rename(path, fileName => (index = fileName.LastIndexOf('.')) == -1 ? fileName + suffix : fileName.Insert(index, suffix), overwrite);
    }
    public static bool Rename(string path, Func<string, string> rename, bool overwrite = false)
    {
        bool exist = true;
        do
        {

            path = rename(path);

        } while (overwrite || !(exist = File.Exists(path)));

        File.Move(path.ToString(), path, overwrite);

        return overwrite || !exist; // return true if a file was created . equivalent to : return File.Exist(path)
    }
}
