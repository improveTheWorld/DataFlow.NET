using System.Security.Cryptography;
namespace DataFlow.Data.Tests.Utilities;

public static class TempFileHelper
{
    public static string CreateTempDirectory(string name)
    {
        var root = Path.Combine(Path.GetTempPath(), "DataFlowTests", name);
        Directory.CreateDirectory(root);
        return root;
    }

    public static string Sha256(string path)
    {
        using var sha = SHA256.Create();
        using var fs = File.OpenRead(path);
        var hash = sha.ComputeHash(fs);
        return string.Concat(hash.Select(b => b.ToString("x2")));
    }
}