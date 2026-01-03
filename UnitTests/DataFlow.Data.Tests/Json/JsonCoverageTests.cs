using DataFlow;
using DataFlow.Data.Tests.Utilities;
using Xunit;
using System.Text;

namespace DataFlow.Data.Tests.Json;

/// <summary>
/// Additional JSON coverage tests for async paths, single root handling, and error conditions.
/// </summary>
public class JsonCoverageTests
{
    #region Test Models

    public record SimpleItem(int Id, string Name);
    public record NestedItem(int Id, Inner Data);
    public record Inner(string Value);

    #endregion

    #region Single Root Tests

    [Fact]
    public void JsonSync_SingleObject_Works()
    {
        // Arrange - Single JSON object (not an array)
        var json = """{"Id":1,"Name":"single"}""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<SimpleItem> { AllowSingleObject = true };

        // Act
        var items = Read.JsonSync<SimpleItem>(stream, opts).ToList();

        // Assert
        Assert.Single(items);
        Assert.Equal("single", items[0].Name);
    }

    [Fact]
    public void JsonSync_SingleNumber_Works()
    {
        // Arrange - Single primitive value
        var json = "42";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<int> { AllowSingleObject = true };

        // Act
        var items = Read.JsonSync<int>(stream, opts).ToList();

        // Assert
        Assert.Single(items);
        Assert.Equal(42, items[0]);
    }

    [Fact]
    public void JsonSync_SingleString_Works()
    {
        // Arrange - Single string value
        var json = "\"hello world\"";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<string> { AllowSingleObject = true };

        // Act
        var items = Read.JsonSync<string>(stream, opts).ToList();

        // Assert
        Assert.Single(items);
        Assert.Equal("hello world", items[0]);
    }

    #endregion

    #region Nested Object Tests

    [Fact]
    public void JsonSync_DeeplyNested_Works()
    {
        // Arrange
        var json = """[{"Id":1,"Data":{"Value":"deep1"}},{"Id":2,"Data":{"Value":"deep2"}}]""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<NestedItem>();

        // Act
        var items = Read.JsonSync<NestedItem>(stream, opts).ToList();

        // Assert
        Assert.Equal(2, items.Count);
        Assert.Equal("deep1", items[0].Data.Value);
        Assert.Equal("deep2", items[1].Data.Value);
    }

    #endregion

    #region Error Handling

    [Fact]
    public void JsonSync_MalformedItem_SkipsOnError()
    {
        // Arrange - Second item has invalid JSON structure
        var json = """[{"Id":1,"Name":"good"},{"Id":"not_a_number","Name":"bad"},{"Id":3,"Name":"good2"}]""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var errorSink = new InMemoryErrorSink();
        var opts = new JsonReadOptions<SimpleItem>
        {
            ErrorAction = ReaderErrorAction.Skip,
            ErrorSink = errorSink
        };

        // Act
        var items = Read.JsonSync<SimpleItem>(stream, opts).ToList();

        // Assert
        Assert.True(items.Count >= 1);
    }

    [Fact]
    public void JsonSync_EmptyArray_ReturnsEmpty()
    {
        // Arrange
        var json = "[]";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<SimpleItem>();

        // Act
        var items = Read.JsonSync<SimpleItem>(stream, opts).ToList();

        // Assert
        Assert.Empty(items);
    }

    [Fact]
    public void JsonSync_EmptyStream_ReturnsEmpty()
    {
        // Arrange
        var json = "";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<SimpleItem>();

        // Act
        var items = Read.JsonSync<SimpleItem>(stream, opts).ToList();

        // Assert
        Assert.Empty(items);
    }

    #endregion

    #region Metrics Tests

    [Fact]
    public void JsonSync_Metrics_ArePopulated()
    {
        // Arrange
        var json = """[{"Id":1,"Name":"one"},{"Id":2,"Name":"two"},{"Id":3,"Name":"three"}]""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<SimpleItem>();

        // Act
        var items = Read.JsonSync<SimpleItem>(stream, opts).ToList();

        // Assert
        Assert.Equal(3, items.Count);
        Assert.NotNull(opts.Metrics);
        Assert.Equal(3, opts.Metrics.RecordsEmitted);
        Assert.NotNull(opts.Metrics.CompletedUtc);
    }

    [Fact]
    public void JsonSync_RawRecordsParsed_Tracked()
    {
        // Arrange
        var json = """[{"Id":1,"Name":"a"},{"Id":2,"Name":"b"}]""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<SimpleItem>();

        // Act
        var items = Read.JsonSync<SimpleItem>(stream, opts).ToList();

        // Assert
        Assert.Equal(2, opts.Metrics.RawRecordsParsed);
    }

    #endregion

    #region Async Tests

    [Fact]
    public async Task JsonAsync_Stream_Works()
    {
        // Arrange
        var json = """[{"Id":1,"Name":"async1"},{"Id":2,"Name":"async2"}]""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<SimpleItem>();

        // Act
        var items = new List<SimpleItem>();
        await foreach (var item in Read.Json<SimpleItem>(stream, opts))
        {
            items.Add(item);
        }

        // Assert
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task JsonAsync_File_Works()
    {
        // Arrange
        var json = """[{"Id":1,"Name":"file1"},{"Id":2,"Name":"file2"}]""";
        var tmpFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tmpFile, json);
            var opts = new JsonReadOptions<SimpleItem>();

            // Act
            var items = new List<SimpleItem>();
            await foreach (var item in Read.Json<SimpleItem>(tmpFile, opts))
            {
                items.Add(item);
            }

            // Assert
            Assert.Equal(2, items.Count);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Fact]
    public async Task JsonAsync_SimpleOverload_Works()
    {
        // Arrange
        var json = """[{"Id":1,"Name":"simple"}]""";
        var tmpFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tmpFile, json);

            // Act
            var items = new List<SimpleItem>();
            await foreach (var item in Read.Json<SimpleItem>(tmpFile))
            {
                items.Add(item);
            }

            // Assert
            Assert.Single(items);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    #endregion

    #region Special Characters

    [Fact]
    public void JsonSync_WithSpecialCharacters_Works()
    {
        // Arrange - JSON with escape sequences
        var json = """[{"Id":1,"Name":"hello\tworld"},{"Id":2,"Name":"line1\nline2"}]""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<SimpleItem>();

        // Act
        var items = Read.JsonSync<SimpleItem>(stream, opts).ToList();

        // Assert
        Assert.Equal(2, items.Count);
        Assert.Contains("\t", items[0].Name);
        Assert.Contains("\n", items[1].Name);
    }

    [Fact]
    public void JsonSync_WithUnicode_Works()
    {
        // Arrange
        var json = """[{"Id":1,"Name":"日本語"},{"Id":2,"Name":"한글"}]""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<SimpleItem>();

        // Act
        var items = Read.JsonSync<SimpleItem>(stream, opts).ToList();

        // Assert
        Assert.Equal(2, items.Count);
        Assert.Equal("日本語", items[0].Name);
        Assert.Equal("한글", items[1].Name);
    }

    #endregion
}
