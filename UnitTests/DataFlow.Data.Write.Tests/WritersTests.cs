using DataFlow;
using System.Text;
using Xunit;


namespace DataFlow.Data.Write.Tests;

/// <summary>
/// Unit tests for Writers (CSV, JSON, YAML, Text) with options and stream support.
/// </summary>
public class WritersTests
{
    public record TestRecord(int Id, string Name, decimal Amount);

    private static async IAsyncEnumerable<T> ToAsync<T>(IEnumerable<T> items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }

    #region CSV Tests

    [Fact]
    public async Task WriteCsv_BasicRecords_ProducesValidCsv()
    {
        // Arrange
        var records = new[] { new TestRecord(1, "Alice", 100.50m), new TestRecord(2, "Bob", 200.75m) };
        var tempFile = Path.GetTempFileName();

        try
        {
            // Act
            await records.WriteCsv(tempFile);

            // Assert
            var content = await File.ReadAllTextAsync(tempFile);
            Assert.Contains("Id", content);
            Assert.Contains("Alice", content);
            Assert.Contains("Bob", content);
            // Note: decimal format is locale-specific, check record count instead
            var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(3, lines.Length); // Header + 2 records
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteCsv_WithOptions_UsesCustomSeparator()
    {
        // Arrange
        var records = new[] { new TestRecord(1, "Test", 50m) };
        using var stream = new MemoryStream();
        var options = new CsvWriteOptions { Separator = ";" };

        // Act
        await records.WriteCsv(stream, options);

        // Assert
        stream.Position = 0;
        var content = new StreamReader(stream).ReadToEnd();
        Assert.Contains(";", content);
        Assert.DoesNotContain(",", content);
    }

    [Fact]
    public async Task WriteCsv_NoHeader_OmitsHeaderRow()
    {
        // Arrange
        var records = new[] { new TestRecord(1, "Alice", 100m) };
        using var stream = new MemoryStream();
        var options = new CsvWriteOptions { WriteHeader = false };

        // Act
        await records.WriteCsv(stream, options);

        // Assert
        stream.Position = 0;
        var content = new StreamReader(stream).ReadToEnd();
        Assert.DoesNotContain("Id", content);
        Assert.Contains("Alice", content);
    }

    [Fact]
    public async Task WriteCsv_MetricsTracked_RecordsCount()
    {
        // Arrange
        var records = ToAsync(new[] { new TestRecord(1, "A", 1m), new TestRecord(2, "B", 2m), new TestRecord(3, "C", 3m) });
        using var stream = new MemoryStream();
        var options = new CsvWriteOptions();

        // Act
        await records.WriteCsv(stream, options);

        // Assert
        Assert.Equal(3, options.Metrics.RecordsWritten);
        Assert.NotNull(options.Metrics.StartedUtc);
        Assert.NotNull(options.Metrics.CompletedUtc);
    }

    [Fact]
    public async Task WriteCsv_SpecialCharacters_ProperlyQuoted()
    {
        // Arrange
        var records = new[] { new TestRecord(1, "Hello, World", 100m) };
        using var stream = new MemoryStream();

        // Act
        await records.WriteCsv(stream);

        // Assert
        stream.Position = 0;
        var content = new StreamReader(stream).ReadToEnd();
        Assert.Contains("\"Hello, World\"", content);
    }

    #endregion

    #region JSON Tests

    [Fact]
    public async Task WriteJson_BasicItems_ProducesValidJson()
    {
        // Arrange
        var items = ToAsync(new[] { new TestRecord(1, "Test", 99.99m) });
        using var stream = new MemoryStream();

        // Act
        await items.WriteJson(stream);

        // Assert
        stream.Position = 0;
        var content = new StreamReader(stream).ReadToEnd();
        Assert.StartsWith("[", content.Trim());
        Assert.EndsWith("]", content.Trim());
        Assert.Contains("\"Id\"", content);
    }

    [Fact]
    public async Task WriteJson_NotIndented_ProducesCompactJson()
    {
        // Arrange
        var items = new[] { new TestRecord(1, "Test", 50m) };
        using var stream = new MemoryStream();
        var options = new JsonWriteOptions { Indented = false };

        // Act
        await items.WriteJson(stream, options);

        // Assert
        stream.Position = 0;
        var content = new StreamReader(stream).ReadToEnd();
        Assert.DoesNotContain("\n  ", content); // No indentation
    }

    [Fact]
    public async Task WriteJson_MetricsTracked_RecordsCount()
    {
        // Arrange
        var items = new[] { new TestRecord(1, "A", 1m), new TestRecord(2, "B", 2m) };
        using var stream = new MemoryStream();
        var options = new JsonWriteOptions();

        // Act
        await items.WriteJson(stream, options);

        // Assert
        Assert.Equal(2, options.Metrics.RecordsWritten);
    }

    #endregion

    #region YAML Tests

    [Fact]
    public async Task WriteYaml_BasicItems_ProducesValidYaml()
    {
        // Arrange
        var items = ToAsync(new[] { new TestRecord(1, "YamlTest", 42m) });
        using var stream = new MemoryStream();

        // Act
        await items.WriteYaml(stream);

        // Assert
        stream.Position = 0;
        var content = new StreamReader(stream).ReadToEnd();
        Assert.Contains("-", content); // YAML list marker
        Assert.Contains("YamlTest", content);
    }

    [Fact]
    public async Task WriteYaml_EmptySequence_WritesEmptyMarker()
    {
        // Arrange
        var items = ToAsync(Array.Empty<TestRecord>());
        using var stream = new MemoryStream();
        var options = new YamlWriteOptions { WriteEmptySequence = true };

        // Act
        await items.WriteYaml(stream, options);

        // Assert
        stream.Position = 0;
        var content = new StreamReader(stream).ReadToEnd();
        Assert.Contains("[]", content);
    }

    [Fact]
    public async Task WriteYaml_WithBatching_CreatesMultipleDocuments()
    {
        // Arrange
        var items = ToAsync(Enumerable.Range(1, 5).Select(i => new TestRecord(i, $"Item{i}", i * 10m)));
        using var stream = new MemoryStream();
        var options = new YamlWriteOptions { BatchSize = 2 };

        // Act
        await items.WriteYaml(stream, options);

        // Assert
        stream.Position = 0;
        var content = new StreamReader(stream).ReadToEnd();
        Assert.Contains("---", content); // Document separator
    }

    #endregion

    #region Text Tests

    [Fact]
    public async Task WriteText_Lines_WritesAllLines()
    {
        // Arrange
        var lines = new[] { "Line 1", "Line 2", "Line 3" };
        using var stream = new MemoryStream();

        // Act
        await lines.WriteText(stream);

        // Assert
        stream.Position = 0;
        var content = new StreamReader(stream).ReadToEnd();
        Assert.Contains("Line 1", content);
        Assert.Contains("Line 3", content);
    }

    [Fact]
    public async Task WriteText_MetricsTracked_LinesCount()
    {
        // Arrange
        var lines = ToAsync(new[] { "A", "B", "C", "D" });
        using var stream = new MemoryStream();
        var options = new WriteOptions();

        // Act
        await lines.WriteText(stream, options);

        // Assert
        Assert.Equal(4, options.Metrics.RecordsWritten);
    }

    #endregion

    #region Cancellation Tests

    [Fact]
    public async Task WriteCsv_Cancellation_StopsWriting()
    {
        // Arrange
        var cts = new CancellationTokenSource();
        var records = ToAsync(Enumerable.Range(1, 1000).Select(i => new TestRecord(i, $"Item{i}", i)));
        using var stream = new MemoryStream();
        var options = new CsvWriteOptions { CancellationToken = cts.Token };

        // Act & Assert
        cts.CancelAfter(1); // Cancel almost immediately
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await records.WriteCsv(stream, options);
        });
    }

    #endregion

    #region CsvWriter Core Tests

    [Fact]
    public void ToCsvLine_NullRecord_ReturnsEmpty()
    {
        TestRecord? record = null;
        var result = record.ToCsvLine();
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ToCsvLine_CustomSeparator_UsesSeparator()
    {
        var record = new TestRecord(1, "Test", 50m);
        var result = record.ToCsvLine(";");
        Assert.Contains(";", result);
        Assert.DoesNotContain(",", result);
    }

    [Fact]
    public void ToCsvLine_EmbeddedQuotes_DoublesThem()
    {
        var record = new TestRecord(1, "Say \"Hello\"", 100m);
        var result = record.ToCsvLine();
        Assert.Contains("\"\"Hello\"\"", result);
    }

    [Fact]
    public void ToCsvLine_NewlineInField_QuotesField()
    {
        var record = new TestRecord(1, "Line1\nLine2", 100m);
        var result = record.ToCsvLine();
        Assert.Contains("\"Line1\nLine2\"", result);
    }

    [Fact]
    public void ToCsvLine_LeadingTrailingSpaces_QuotesField()
    {
        var record = new TestRecord(1, " Padded ", 100m);
        var result = record.ToCsvLine();
        Assert.Contains("\" Padded \"", result);
    }

    [Fact]
    public void CsvHeader_BasicType_ReturnsPropertyNames()
    {
        var header = CsvWriter.CsvHeader<TestRecord>();
        Assert.Contains("Id", header);
        Assert.Contains("Name", header);
        Assert.Contains("Amount", header);
    }

    [Fact]
    public void CsvHeader_CustomSeparator_UsesSeparator()
    {
        var header = CsvWriter.CsvHeader<TestRecord>(";");
        Assert.Contains(";", header);
        Assert.DoesNotContain(",", header);
    }

    #endregion

    #region Additional Edge Cases

    [Fact]
    public async Task WriteCsv_EmptyCollection_WritesOnlyHeader()
    {
        var records = Array.Empty<TestRecord>();
        using var stream = new MemoryStream();
        var options = new CsvWriteOptions();

        await records.WriteCsv(stream, options);

        stream.Position = 0;
        var content = new StreamReader(stream).ReadToEnd();
        Assert.Contains("Id", content);
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines); // Only header
    }

    [Fact]
    public async Task WriteJson_EmptyCollection_WritesEmptyArray()
    {
        var items = Array.Empty<TestRecord>();
        using var stream = new MemoryStream();

        await items.WriteJson(stream);

        stream.Position = 0;
        var content = new StreamReader(stream).ReadToEnd().Trim();
        Assert.Equal("[]", content);
    }

    [Fact]
    public async Task WriteYaml_NoEmptyMarker_WritesNothing()
    {
        var items = ToAsync(Array.Empty<TestRecord>());
        using var stream = new MemoryStream();
        var options = new YamlWriteOptions { WriteEmptySequence = false };

        await items.WriteYaml(stream, options);

        stream.Position = 0;
        var content = new StreamReader(stream).ReadToEnd();
        Assert.DoesNotContain("[]", content);
    }

    [Fact]
    public async Task WriteCsv_IEnumerableStream_WritesToStream()
    {
        var records = new[] { new TestRecord(1, "Sync", 99m) };
        using var stream = new MemoryStream();

        await records.WriteCsv(stream);

        stream.Position = 0;
        var content = new StreamReader(stream).ReadToEnd();
        Assert.Contains("Sync", content);
    }

    [Fact]
    public void WriterMetrics_InitialState_IsZero()
    {
        var metrics = new WriterMetrics();
        Assert.Equal(0, metrics.RecordsWritten);
        Assert.Null(metrics.StartedUtc);
        Assert.Null(metrics.CompletedUtc);
    }

    [Fact]
    public void WriteOptions_DefaultValues_AreCorrect()
    {
        var options = new WriteOptions();
        Assert.Equal(System.Text.Encoding.UTF8, options.Encoding);
        Assert.False(options.Append);
        Assert.Equal(default, options.CancellationToken);
    }

    [Fact]
    public void CsvWriteOptions_DefaultValues_AreCorrect()
    {
        var options = new CsvWriteOptions();
        Assert.Equal(",", options.Separator);
        Assert.True(options.WriteHeader);
    }

    [Fact]
    public void JsonWriteOptions_DefaultValues_AreCorrect()
    {
        var options = new JsonWriteOptions();
        Assert.True(options.Indented);
        Assert.Null(options.SerializerOptions);
    }

    [Fact]
    public void YamlWriteOptions_DefaultValues_AreCorrect()
    {
        var options = new YamlWriteOptions();
        Assert.True(options.WriteEmptySequence);
        Assert.Null(options.BatchSize);
    }

    #endregion

    #region Sync Method Tests

    [Fact]
    public void WriteCsvSync_BasicRecords_CreatesFile()
    {
        var records = new[] { new TestRecord(1, "Alice", 100m) };
        var tempFile = Path.GetTempFileName();
        try
        {
            records.WriteCsvSync(tempFile);
            Assert.True(File.Exists(tempFile));
            var content = File.ReadAllText(tempFile);
            Assert.Contains("Alice", content);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void WriteJsonSync_BasicItems_CreatesFile()
    {
        var items = new[] { new TestRecord(1, "JsonSync", 50m) };
        var tempFile = Path.GetTempFileName();
        try
        {
            items.WriteJsonSync(tempFile);
            Assert.True(File.Exists(tempFile));
            var content = File.ReadAllText(tempFile);
            Assert.Contains("JsonSync", content);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void WriteYamlSync_BasicItems_CreatesFile()
    {
        var items = new[] { new TestRecord(1, "YamlSync", 75m) };
        var tempFile = Path.GetTempFileName();
        try
        {
            items.WriteYamlSync(tempFile);
            Assert.True(File.Exists(tempFile));
            var content = File.ReadAllText(tempFile);
            Assert.Contains("YamlSync", content);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void WriteTextSync_Lines_CreatesFile()
    {
        var lines = new[] { "Sync Line 1", "Sync Line 2" };
        var tempFile = Path.GetTempFileName();
        try
        {
            lines.WriteTextSync(tempFile);
            Assert.True(File.Exists(tempFile));
            var content = File.ReadAllText(tempFile);
            Assert.Contains("Sync Line 1", content);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion

    #region Async File Path Tests

    [Fact]
    public async Task WriteText_FilePath_CreatesFile()
    {
        var lines = ToAsync(new[] { "Async Line 1", "Async Line 2" });
        var tempFile = Path.GetTempFileName();
        try
        {
            await lines.WriteText(tempFile);
            Assert.True(File.Exists(tempFile));
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteJson_FilePath_CreatesFile()
    {
        var items = ToAsync(new[] { new TestRecord(1, "JsonPath", 123m) });
        var tempFile = Path.GetTempFileName();
        try
        {
            await items.WriteJson(tempFile);
            var content = await File.ReadAllTextAsync(tempFile);
            Assert.Contains("[", content);
            Assert.Contains("JsonPath", content);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteYaml_FilePath_IEnumerable_CreatesFile()
    {
        var items = new[] { new TestRecord(1, "YamlPath", 999m) };
        var tempFile = Path.GetTempFileName();
        try
        {
            await items.WriteYaml(tempFile);
            var content = await File.ReadAllTextAsync(tempFile);
            Assert.Contains("YamlPath", content);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteYamlBatched_MultipleItems_CreatesBatches()
    {
        var items = ToAsync(Enumerable.Range(1, 10).Select(i => new TestRecord(i, $"Batch{i}", i * 10m)));
        var tempFile = Path.GetTempFileName();
        try
        {
            await items.WriteYamlBatched(tempFile, batchSize: 3);
            var content = await File.ReadAllTextAsync(tempFile);
            // Should have document separators
            var separatorCount = content.Split("---").Length - 1;
            Assert.True(separatorCount >= 2, $"Expected at least 2 separators, found {separatorCount}");
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteCsv_IEnumerable_FilePath_CreatesFile()
    {
        var records = new[] { new TestRecord(1, "FilePath", 42m), new TestRecord(2, "Test", 84m) };
        var tempFile = Path.GetTempFileName();
        try
        {
            await records.WriteCsv(tempFile);
            var content = await File.ReadAllTextAsync(tempFile);
            Assert.Contains("FilePath", content);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteCsv_AsyncEnumerable_FilePath_WithOptions()
    {
        var records = ToAsync(new[] { new TestRecord(1, "OptionsPath", 100m) });
        var tempFile = Path.GetTempFileName();
        var options = new CsvWriteOptions { Separator = "|", WriteHeader = false };
        try
        {
            await records.WriteCsv(tempFile, options);
            var content = await File.ReadAllTextAsync(tempFile);
            Assert.Contains("|", content);
            Assert.DoesNotContain("Id", content);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteText_IEnumerable_FilePath_CreatesFile()
    {
        IEnumerable<string> lines = new[] { "Enum Line 1", "Enum Line 2" };
        var tempFile = Path.GetTempFileName();
        try
        {
            await lines.WriteText(tempFile);
            var content = await File.ReadAllTextAsync(tempFile);
            Assert.Contains("Enum Line 1", content);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteJson_IEnumerable_FilePath_CreatesFile()
    {
        var items = new[] { new TestRecord(1, "EnumJson", 50m) };
        var tempFile = Path.GetTempFileName();
        try
        {
            await items.WriteJson(tempFile);
            var content = await File.ReadAllTextAsync(tempFile);
            Assert.Contains("EnumJson", content);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    #endregion

    #region JsonLinesFormat Tests

    // JsonLinesFormat — Doc: Data-Writing-Infrastructure.md L88
    // "One object per line, no array" when JsonLinesFormat = true

    [Fact]
    public async Task WriteJson_JsonLinesFormat_IEnumerable_FilePath_ProducesNDJSON()
    {
        // Arrange — exact audit report scenario
        var data = new[] { new { Id = 1, Name = "A" }, new { Id = 2, Name = "B" } };
        var tempFile = Path.GetTempFileName();
        var opts = new JsonWriteOptions { JsonLinesFormat = true };
        try
        {
            // Act
            await data.WriteJson(tempFile, opts);
            var content = await File.ReadAllTextAsync(tempFile);

            // Assert — each line should be a self-contained JSON object, no array wrapper
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(2, lines.Length);
            Assert.DoesNotContain("[", content);
            Assert.DoesNotContain("]", content);
            Assert.Contains("\"Id\"", lines[0]);
            Assert.Contains("\"Name\"", lines[0]);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteJson_JsonLinesFormat_IAsyncEnumerable_FilePath_ProducesNDJSON()
    {
        // Arrange
        var data = ToAsync(new[] { new TestRecord(1, "A", 10m), new TestRecord(2, "B", 20m) });
        var tempFile = Path.GetTempFileName();
        var opts = new JsonWriteOptions { JsonLinesFormat = true };
        try
        {
            // Act
            await data.WriteJson(tempFile, opts);
            var content = await File.ReadAllTextAsync(tempFile);

            // Assert
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(2, lines.Length);
            Assert.DoesNotContain("[", content);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void WriteJsonSync_JsonLinesFormat_FilePath_ProducesNDJSON()
    {
        // Arrange
        var data = new[] { new TestRecord(1, "A", 10m), new TestRecord(2, "B", 20m) };
        var tempFile = Path.GetTempFileName();
        var opts = new JsonWriteOptions { JsonLinesFormat = true };
        try
        {
            // Act
            data.WriteJsonSync(tempFile, opts);
            var content = File.ReadAllText(tempFile);

            // Assert
            var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(2, lines.Length);
            Assert.DoesNotContain("[", content);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task WriteJson_JsonLinesFormat_Stream_ProducesNDJSON()
    {
        // Arrange
        var data = new[] { new TestRecord(1, "A", 10m), new TestRecord(2, "B", 20m) };
        using var stream = new MemoryStream();
        var opts = new JsonWriteOptions { JsonLinesFormat = true };

        // Act
        await data.WriteJson(stream, opts);

        // Assert
        stream.Position = 0;
        var content = new StreamReader(stream).ReadToEnd();
        var lines = content.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length);
        Assert.DoesNotContain("[", content);
    }

    #endregion

    #region Null Argument Tests

    [Fact]
    public async Task WriteCsv_NullPath_ThrowsArgumentNull()
    {
        var records = new[] { new TestRecord(1, "Test", 1m) };
        await Assert.ThrowsAsync<ArgumentNullException>(() => records.WriteCsv((string)null!));
    }

    [Fact]
    public async Task WriteCsv_NullStream_ThrowsArgumentNull()
    {
        var records = ToAsync(new[] { new TestRecord(1, "Test", 1m) });
        await Assert.ThrowsAsync<ArgumentNullException>(() => records.WriteCsv((Stream)null!));
    }

    [Fact]
    public async Task WriteJson_NullPath_ThrowsArgumentNull()
    {
        var items = new[] { new TestRecord(1, "Test", 1m) };
        await Assert.ThrowsAsync<ArgumentNullException>(() => items.WriteJson((string)null!));
    }

    #endregion
}
