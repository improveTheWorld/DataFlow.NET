
using DataFlow.Framework;

namespace DataFlow.Extensions
{
    public static class FileSystemExtensions
    {
        public static StreamWriter CreateFileWithoutFailure(this FilePath path, string renameSuffix= ".old")
        {
            var status  = path.status;
            var fullName = path.FullName;
            if (status == FilePath.Status.File)
            {
                //Rename the odl one with the suffixe .old
                FilePath.Rename(fullName, renameSuffix);
            }
            else if(status == FilePath.Status.Folder)
            {
                throw new ArgumentException(path + "is a existant folder");
            }
            else if(status == FilePath.Status.MissedPath)
            {
                Directory.CreateDirectory(path.Up());
            }
            return new StreamWriter(fullName);
        }

        public static void WriteInFile(this IEnumerable<string> lines, string path, string renamesuffix = ".old", int flusheach = -1)
        {
            StreamWriter fileWriter = CreateFileWithoutFailure(new FilePath(path),renamesuffix);
            lines.ForEach((line, idx) => 
            {
                fileWriter.WriteLine(line);
                if(idx % flusheach == 0  ) fileWriter.Flush();
            }).Do();
            fileWriter.Flush();
            fileWriter.Close();
        }

        public static string DerivateFileName(this string name, Func<string, string> derivate, bool keepEntension = true, params Func<string, string>[] derivates)
        {
            var pathHirarchy = name.Split(Path.DirectorySeparatorChar); 

            if (derivates.Length > pathHirarchy.Length - 1) throw new ArgumentOutOfRangeException(nameof(derivates));

            pathHirarchy[pathHirarchy.LastIdx()] = pathHirarchy.Last().DerivateFileName(derivate, keepEntension);

            for (int i = 0; i < derivates.Length; i++)
            {
                pathHirarchy[pathHirarchy.LastIdx() - i - 1] = derivates[i](pathHirarchy[pathHirarchy.LastIdx() - i - 1]);
            }

            return Path.Combine(pathHirarchy);
        }

        public static string DerivateFileName(this string name, Func<string, string> derivate, bool keepEntension = true)
        {

            var derivatedName   =  keepEntension ?
                derivate(Path.GetFileNameWithoutExtension(name)) + Path.GetExtension(name) :
                derivate(Path.GetFileNameWithoutExtension(name));

            return Path.Combine(Path.GetDirectoryName(name), derivatedName);
        }
    }

}