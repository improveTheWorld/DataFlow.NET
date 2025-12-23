using DataFlow.Data;
using Xunit;
using System.Text;

namespace DataFlow.Data.Tests.Yaml;

/// <summary>
/// YAML reader tests using in-memory streams (no file I/O) to avoid hanging issues.
/// </summary>
public class YamlReaderTests
{
    public record Node
    {
        public int id { get; set; }
        public string name { get; set; } = "";
        public bool ok { get; set; }
    }

    // Inline YAML data instead of file generation
    private const string SampleSequenceYaml = @"- id: 0
  name: item_0
  ok: true
- id: 1
  name: item_1
  ok: false
- id: 2
  name: item_2
  ok: true
";

    private const string MalformedSequenceYaml = @"- id: 0
  name: item_0
  ok: true
- id: 1
  name: item_1
  ok: false
- id: 2
  name: item_2
  ok: true
- id: 3
  name: item_3
  ok: false
  desc: |
    extra line
    line
- id: 4
  name: item_4
  ok: true
";

    [Fact]
    public async Task Yaml_Read_Skip_Continues()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(MalformedSequenceYaml));
        var opts = new YamlReadOptions<Node>
        {
            ErrorAction = ReaderErrorAction.Skip
        };

        // Act
        int count = 0;
        await foreach (var n in Read.Yaml<Node>(stream, opts))
            count++;

        // Assert
        Assert.Equal(4, count);  // 5 total elements, 1 skipped
        Assert.Equal(1, opts.Metrics.ErrorCount);
    }

    [Fact]
    public async Task Reads_Sequence()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(SampleSequenceYaml));
        var opts = new YamlReadOptions<Node>
        {
            ErrorAction = ReaderErrorAction.Skip,
        };

        // Act
        int count = 0;
        await foreach (var _ in Read.Yaml<Node>(stream, opts))
            count++;

        // Assert
        Assert.Equal(3, count);
        Assert.False(opts.Metrics.TerminatedEarly);
    }

    [Fact]
    public async Task Yaml_Sync_Reads_Sequence()
    {
        // Arrange  
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(SampleSequenceYaml));
        var opts = new YamlReadOptions<Node>();

        // Act
        var items = Read.YamlSync<Node>(stream, opts).ToList();

        // Assert
        Assert.Equal(3, items.Count);
        Assert.Equal("item_0", items[0].name);
        Assert.Equal("item_2", items[2].name);
    }

    [Fact]
    public void YamlSync_Metrics_ArePopulated()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(SampleSequenceYaml));
        var opts = new YamlReadOptions<Node>();

        // Act
        var items = Read.YamlSync<Node>(stream, opts).ToList();

        // Assert
        Assert.Equal(3, items.Count);
        Assert.NotNull(opts.Metrics);
        Assert.Equal(3, opts.Metrics.RecordsEmitted);
        Assert.NotNull(opts.Metrics.CompletedUtc);
    }
}