using DataFlow.Data;
using DataFlow.Data.Tests.Utilities;
using Xunit;

namespace DataFlow.Data.Tests.Json;

/// <summary>
/// Tests for JSON parser buffer boundary handling.
/// These tests target the ProcessBuffer() and AdjustBufferForNextRead() paths
/// that are typically not covered by standard tests.
/// </summary>
public class JsonBufferBoundaryTests
{
    #region Test Models

    public record SimpleItem(int Id, string Name);
    public record NestedItem(int Id, Inner Data);
    public record Inner(string Value);

    #endregion

    #region Chunked Stream Tests

    [Theory]
    [InlineData(1)]   // Byte-by-byte (extreme case)
    [InlineData(4)]   // Typical small chunk
    [InlineData(16)]  // Medium chunk
    [InlineData(64)]  // Larger chunk
    public void JsonSync_WithChunkedStream_ParsesCorrectly(int chunkSize)
    {
        // Arrange
        var json = """[{"Id":1,"Name":"Alice"},{"Id":2,"Name":"Bob"}]""";
        using var stream = MockStreams.Chunked(json, chunkSize);
        var options = new JsonReadOptions<SimpleItem>();

        // Act
        var items = Read.JsonSync<SimpleItem>(stream, options).ToList();

        // Assert
        Assert.Equal(2, items.Count);
        Assert.Equal("Alice", items[0].Name);
        Assert.Equal("Bob", items[1].Name);
    }

    [Fact]
    public async Task JsonAsync_WithChunkedStream_ParsesCorrectly()
    {
        // Arrange - JSON split across many small reads
        var json = """[{"Id":1,"Name":"Alice"},{"Id":2,"Name":"Bob"}]""";
        using var stream = MockStreams.Chunked(json, 8);
        var options = new JsonReadOptions<SimpleItem>();

        // Act
        var items = new List<SimpleItem>();
        await foreach (var item in Read.Json<SimpleItem>(stream, options))
        {
            items.Add(item);
        }

        // Assert
        Assert.Equal(2, items.Count);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void JsonSync_NestedObject_WithChunkedStream(int chunkSize)
    {
        // Arrange - Nested JSON requires careful buffer handling
        var json = """[{"Id":1,"Data":{"Value":"test"}}]""";
        using var stream = MockStreams.Chunked(json, chunkSize);
        var options = new JsonReadOptions<NestedItem>();

        // Act
        var items = Read.JsonSync<NestedItem>(stream, options).ToList();

        // Assert
        Assert.Single(items);
        Assert.Equal("test", items[0].Data.Value);
    }

    #endregion

    #region Large Array Boundary Tests

    [Fact]
    public void JsonSync_LargeArray_WithSmallChunks_ParsesAllItems()
    {
        // Arrange - 100 items, read in 16-byte chunks
        var items = Enumerable.Range(1, 100)
            .Select(i => new { Id = i, Name = $"Item{i}" })
            .ToList();
        var json = System.Text.Json.JsonSerializer.Serialize(items);
        using var stream = MockStreams.Chunked(json, 16);
        var options = new JsonReadOptions<SimpleItem>();

        // Act
        var result = Read.JsonSync<SimpleItem>(stream, options).ToList();

        // Assert
        Assert.Equal(100, result.Count);
        Assert.Equal(1, result[0].Id);
        Assert.Equal(100, result[99].Id);
    }

    [Fact]
    public async Task JsonAsync_LargeArray_WithSmallChunks_ParsesAllItems()
    {
        // Arrange
        var items = Enumerable.Range(1, 50)
            .Select(i => new { Id = i, Name = $"Item{i}" })
            .ToList();
        var json = System.Text.Json.JsonSerializer.Serialize(items);
        using var stream = MockStreams.Chunked(json, 32);
        var options = new JsonReadOptions<SimpleItem>();

        // Act
        var result = new List<SimpleItem>();
        await foreach (var item in Read.Json<SimpleItem>(stream, options))
        {
            result.Add(item);
        }

        // Assert
        Assert.Equal(50, result.Count);
    }

    #endregion

    #region Whitespace Handling

    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    public void JsonSync_WithWhitespace_ChunkedAtWhitespace(int chunkSize)
    {
        // Arrange - Whitespace between elements
        var json = """
            [
              { "Id": 1, "Name": "Alice" },
              { "Id": 2, "Name": "Bob" }
            ]
            """;
        using var stream = MockStreams.Chunked(json, chunkSize);
        var options = new JsonReadOptions<SimpleItem>();

        // Act
        var items = Read.JsonSync<SimpleItem>(stream, options).ToList();

        // Assert
        Assert.Equal(2, items.Count);
    }

    #endregion

    #region Unicode Boundary Tests

    [Theory]
    [InlineData(1)]  // Split multi-byte chars
    [InlineData(3)]  // Typical UTF-8 boundary
    public void JsonSync_Unicode_ChunkedAcrossCharBoundary(int chunkSize)
    {
        // Arrange - Unicode characters that take multiple bytes
        var json = """[{"Id":1,"Name":"日本語"},{"Id":2,"Name":"한글"}]""";
        using var stream = MockStreams.Chunked(json, chunkSize);
        var options = new JsonReadOptions<SimpleItem>();

        // Act
        var items = Read.JsonSync<SimpleItem>(stream, options).ToList();

        // Assert
        Assert.Equal(2, items.Count);
        Assert.Equal("日本語", items[0].Name);
        Assert.Equal("한글", items[1].Name);
    }

    #endregion

    #region Metrics Tracking

    [Fact]
    public void JsonSync_Chunked_MetricsAreAccurate()
    {
        // Arrange
        var json = """[{"Id":1,"Name":"A"},{"Id":2,"Name":"B"},{"Id":3,"Name":"C"}]""";
        using var stream = MockStreams.Chunked(json, 10);
        var options = new JsonReadOptions<SimpleItem>();

        // Act
        var items = Read.JsonSync<SimpleItem>(stream, options).ToList();

        // Assert
        Assert.Equal(3, items.Count);
        Assert.NotNull(options.Metrics);
        Assert.Equal(3, options.Metrics.RecordsEmitted);
        Assert.NotNull(options.Metrics.CompletedUtc);
    }

    #endregion
}
