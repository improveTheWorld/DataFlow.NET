using DataFlow.Data;
using DataFlow.Data.Tests.Generators;
using Xunit;
using DataFlow.Data.Tests.Utilities;

namespace DataFlow.Data.Tests.Text;

public class TextReaderTests
{
    private readonly string _root;
    private readonly DataSetGenerator.GeneratedFiles _files;

    public TextReaderTests()
    {
        _root = TempFileHelper.CreateTempDirectory("Text");
        var cfg = new DataGenConfig { TextLines = 2000 };
        _files = DataSetGenerator.EnsureGenerated(_root, cfg);
    }

    [Fact]
    public async Task Reads_All_Lines_Async()
    {
        int count = 0;
        await foreach (var line in Read.Text(_files.TextPath))
            count++;
        Assert.True(count > 0);
    }

    [Fact]
    public void Reads_All_Lines_Sync()
    {
        int count = Read.TextSync(_files.TextPath).Count();
        Assert.True(count > 0);
    }
}