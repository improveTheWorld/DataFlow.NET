namespace iCode.Framework
{
    public class EnumerableFiles
    {
        public static IEnumerable<iFile> Files(string path, string searchPattern, Func<string, bool> filterFileByName = null, Func<string, bool> goInDepth = null, Func<StreamReader, bool> filterFilecontent = null)
        {
            return Files(path, new string[] { searchPattern }, filterFileByName, goInDepth, filterFilecontent);
        }
        public static IEnumerable<iFile> Files(string path, string[]? allowedExtentions = null,Func<string,bool>? filterFileByName = null , Func<string ,bool>? goInDepth =null, Func<StreamReader,bool>? filterFilecontent= null)
        {

            string searchPattern;

            if(allowedExtentions!=null && allowedExtentions.Length == 0)
            {
                throw new ArgumentException(nameof(allowedExtentions));
            }
            
            
            if(allowedExtentions != null && allowedExtentions.Length == 1)
            {
                searchPattern = allowedExtentions[0];
            }
            else // searchPatterns.Length > 1
            {
                searchPattern = "*.*";
            }
            

            StreamReader tmp;
            foreach( var file in Directory.GetFiles(path, searchPattern ) )
            {
                if(allowedExtentions == null || allowedExtentions.Length==1 || (allowedExtentions.Length>1 && allowedExtentions.Any(allowedExtention=> file.EndsWith(allowedExtention)))    )
                {
                    if (filterFileByName == null || filterFileByName(file))
                    {
                        tmp = new iFile(file);
                        if (filterFilecontent == null || filterFilecontent(tmp))
                        {
                            yield return (iFile)tmp;
                        }
                    }
                }               
                           
            }

            foreach(var directory in Directory.GetDirectories(path))
            {
                if(goInDepth == null || goInDepth(directory))
                {
                    foreach( var file in Files(directory, allowedExtentions, filterFileByName, goInDepth, filterFilecontent))
                    {
                        yield return file;
                    }
                }
            }
        }
    }
}

