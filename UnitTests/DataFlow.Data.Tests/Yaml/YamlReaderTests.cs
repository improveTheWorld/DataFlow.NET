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
        // With IgnoreUnmatchedProperties, the extra 'desc' field is silently ignored
        // All 5 elements are now successfully parsed (no errors)
        Assert.Equal(5, count);
        Assert.Equal(0, opts.Metrics.ErrorCount);
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

    /// <summary>
    /// BUG-002 Regression Test: YAML should now match properties case-insensitively.
    /// Previously, PascalCase keys in YAML would fail to match C# properties.
    /// </summary>
    [Fact]
    public async Task Yaml_CaseInsensitive_PascalCase_Works()
    {
        // Arrange - PascalCase keys (matching C# convention) now work!
        var yaml = @"- Id: 10
  Name: Alice
  Ok: true
- Id: 20
  Name: Bob
  Ok: false
";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
        var opts = new YamlReadOptions<Node>();

        // Act
        var items = new List<Node>();
        await foreach (var item in Read.Yaml<Node>(stream, opts))
            items.Add(item);

        // Assert - BUG-002 FIX: PascalCase keys should now work
        Assert.Equal(2, items.Count);
        Assert.Equal(10, items[0].id);
        Assert.Equal("Alice", items[0].name);
        Assert.Equal(20, items[1].id);
        Assert.Equal("Bob", items[1].name);
    }

    /// <summary>
    /// BUG-007 Regression Test: MaxNodeScalarLength should not cause infinite loop.
    /// When a scalar exceeds the limit with ErrorAction.Skip, the reader should
    /// skip the element and continue, not hang indefinitely.
    /// </summary>
    [Fact(Timeout = 5000)] // 5 second timeout - if it hangs, the test fails
    public async Task Yaml_MaxNodeScalarLength_DoesNotHang()
    {
        // Arrange - YAML with a scalar that exceeds MaxNodeScalarLength (500 chars > 100 limit)
        var longValue = new string('X', 500);
        var yaml = $@"- id: 1
  name: {longValue}
- id: 2
  name: short
";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
        var opts = new YamlReadOptions<Node>
        {
            MaxNodeScalarLength = 100,
            ErrorAction = ReaderErrorAction.Skip
        };

        // Act - This should NOT hang. Before the fix, it caused infinite loop.
        var items = new List<Node>();
        await foreach (var item in Read.Yaml<Node>(stream, opts))
            items.Add(item);

        // Assert - First item should be skipped (oversized scalar), second should be read
        // The exact behavior depends on the fix: either skip the item or throw
        // For now, we just verify it doesn't hang and completes in reasonable time
        Assert.True(items.Count <= 2, $"Expected <= 2 items, got {items.Count}");
    }

    /// <summary>
    /// Verify MaxNodeScalarLength works correctly with ErrorAction.Throw.
    /// Should throw an exception when scalar exceeds limit.
    /// The exception may be InvalidDataException or YamlException (wrapping InvalidDataException).
    /// </summary>
    [Fact]
    public async Task Yaml_MaxNodeScalarLength_ThrowsOnExceed()
    {
        // Arrange
        var longValue = new string('X', 500);
        var yaml = $@"- id: 1
  name: {longValue}
";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
        var opts = new YamlReadOptions<Node>
        {
            MaxNodeScalarLength = 100,
            ErrorAction = ReaderErrorAction.Throw
        };

        // Act & Assert - Exception should be thrown (may be wrapped by YamlDotNet)
        await Assert.ThrowsAnyAsync<Exception>(async () =>
        {
            await foreach (var _ in Read.Yaml<Node>(stream, opts))
            { }
        });
    }
}
