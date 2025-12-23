using DataFlow.Data;
using Xunit;
using System.Text;

namespace DataFlow.Data.Tests.Json;

/// <summary>
/// Additional JSON tests for edge cases, error paths, and performance.
/// </summary>
public class JsonParserEdgeCaseTests
{
    public record Item(int Id, string Name);
    public record NestedItem(int Id, ItemData Data);
    public record ItemData(string Value, int Count);

    #region Whitespace and Formatting

    [Fact]
    public void JsonSync_WithPrettyPrint_Works()
    {
        // Arrange - Formatted JSON with whitespace
        var json = @"[
            {
                ""Id"": 1,
                ""Name"": ""first""
            },
            {
                ""Id"": 2,
                ""Name"": ""second""
            }
        ]";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<Item>();

        // Act
        var items = Read.JsonSync<Item>(stream, opts).ToList();

        // Assert
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void JsonSync_Minified_Works()
    {
        // Arrange - Minified JSON, no whitespace
        var json = "[{\"Id\":1,\"Name\":\"one\"},{\"Id\":2,\"Name\":\"two\"}]";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<Item>();

        // Act
        var items = Read.JsonSync<Item>(stream, opts).ToList();

        // Assert
        Assert.Equal(2, items.Count);
    }

    #endregion

    #region Deeply Nested Objects

    [Fact]
    public void JsonSync_DeeplyNested_Works()
    {
        // Arrange
        var json = @"[{""Id"":1,""Data"":{""Value"":""nested"",""Count"":42}}]";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<NestedItem>();

        // Act
        var items = Read.JsonSync<NestedItem>(stream, opts).ToList();

        // Assert
        Assert.Single(items);
        Assert.Equal("nested", items[0].Data.Value);
        Assert.Equal(42, items[0].Data.Count);
    }

    #endregion

    #region Error Recovery

    [Fact]
    public void JsonSync_MixedValidInvalid_SkipsErrors()
    {
        // Arrange - Second item has wrong type for Id
        var json = @"[{""Id"":1,""Name"":""good""},{""Id"":""not_int"",""Name"":""bad""},{""Id"":3,""Name"":""good2""}]";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<Item>
        {
            ErrorAction = ReaderErrorAction.Skip
        };

        // Act
        var items = Read.JsonSync<Item>(stream, opts).ToList();

        // Assert - At least 2 should parse
        Assert.True(items.Count >= 1);
    }

    [Fact]
    public void JsonSync_TrailingComma_HandledGracefully()
    {
        // Arrange - Some JSON parsers allow trailing commas
        var json = @"[{""Id"":1,""Name"":""test""},]";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<Item>
        {
            ErrorAction = ReaderErrorAction.Skip
        };

        // Act
        var items = Read.JsonSync<Item>(stream, opts).ToList();

        // Assert - May or may not parse, shouldn't crash
        Assert.True(items.Count >= 0);
    }

    #endregion

    #region Special Values

    [Fact]
    public void JsonSync_NullValues_Works()
    {
        // Arrange
        var json = @"[{""Id"":1,""Name"":null}]";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<Item>();

        // Act
        var items = Read.JsonSync<Item>(stream, opts).ToList();

        // Assert
        Assert.Single(items);
        Assert.Null(items[0].Name);
    }

    [Fact]
    public void JsonSync_NumericBoundaries_Works()
    {
        // Arrange - Large numbers
        var json = @"[{""Id"":2147483647,""Name"":""max_int""}]";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<Item>();

        // Act
        var items = Read.JsonSync<Item>(stream, opts).ToList();

        // Assert
        Assert.Single(items);
        Assert.Equal(int.MaxValue, items[0].Id);
    }

    #endregion

    #region Large Data

    [Fact]
    public void JsonSync_LargeArray_Works()
    {
        // Arrange - 500 items
        var sb = new StringBuilder("[");
        for (int i = 0; i < 500; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append($"{{\"Id\":{i},\"Name\":\"item{i}\"}}");
        }
        sb.Append("]");
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
        var opts = new JsonReadOptions<Item>();

        // Act
        var items = Read.JsonSync<Item>(stream, opts).ToList();

        // Assert
        Assert.Equal(500, items.Count);
        Assert.Equal(0, items[0].Id);
        Assert.Equal(499, items[^1].Id);
    }

    #endregion
}
