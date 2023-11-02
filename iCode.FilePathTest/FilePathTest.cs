using Xunit;
using iCode.Framework;
using System.IO;


namespace iCode.Tests
{
    public sealed class FilePathTest
    {
        [Fact]
        void FilePath_CreatePathAndFile_ReplaceExistantFile()
        {
            
            
            string fullExePath  =  System.Reflection.Assembly.GetExecutingAssembly().Location;
            string fullExeDirectory =  Path.GetDirectoryName(fullExePath);


            string testFileFullPath = Path.Combine(fullExeDirectory, "testFile.txt");
            string oldTestFileFullPath = Path.Combine(fullExeDirectory, "testFile.old.txt");

            if(File.Exists(testFileFullPath))
                File.Delete(testFileFullPath);

            if(File.Exists(oldTestFileFullPath))
                File.Delete(oldTestFileFullPath);            

            string phraseOld = "this is a test File, will be old";
            string phraseNew = "this is not the old one";
            StreamWriter testFile  = FilePath.CreatePathAndFile(testFileFullPath);

            testFile.WriteLine(phraseOld);
            testFile.Flush();
            testFile.Close();
            testFile.Dispose();           

            var newTestFile = FilePath.CreatePathAndFile(testFileFullPath, ".old");

            newTestFile.WriteLine(phraseNew);
            newTestFile.Flush();
            newTestFile.Close();
            newTestFile.Dispose();

            Assert.Equal(FilePath.Status.FileExist,FilePath.Check(testFileFullPath));
            Assert.Equal(FilePath.Status.FileExist, FilePath.Check(oldTestFileFullPath));

            StreamReader newTestF = new StreamReader(testFileFullPath);
            StreamReader oldTestF = new StreamReader(oldTestFileFullPath);

            Assert.Equal(phraseNew, newTestF.ReadLine());
            Assert.Equal(phraseOld, oldTestF.ReadLine());

            newTestF.Close();
            newTestF.Dispose();
            oldTestF.Close();
            oldTestF.Dispose();


            File.Delete(testFileFullPath);
            File.Delete(oldTestFileFullPath);
        }


        void FilePath_CreatePathAndFile_EcraseExistantFile()
        {

        }
        void FilePath_CreatePathAndFile_MissedPath()
        {

        }

    }
}
