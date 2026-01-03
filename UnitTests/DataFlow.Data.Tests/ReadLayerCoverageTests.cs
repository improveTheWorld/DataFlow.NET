using DataFlow;
using System.Text;

namespace DataFlow.Data.Tests;

/// <summary>
/// More Read layer tests targeting uncovered CSV and JSON parsing paths.
/// </summary>
public class ReadLayerCoverageTests
{
    public record TestRecord(int Id, string Name);
    public record QuotedRecord(int Id, string Text);

    #region CSV Parser Edge Cases

    [Fact]
    public void CsvSync_QuotedFields_ParsesCorrectly()
    {
        var csv = "Id,Text\n1,\"Hello, World\"\n2,\"Say \"\"Hi\"\"\"\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions { HasHeader = true };

        var items = Read.CsvSync<QuotedRecord>(ms, opts).ToList();

        Assert.Equal(2, items.Count);
        Assert.Equal("Hello, World", items[0].Text);
        Assert.Equal("Say \"Hi\"", items[1].Text);
    }

    [Fact]
    public void CsvSync_MultilineQuotedField_ParsesAcrossLines()
    {
        var csv = "Id,Text\n1,\"Line1\nLine2\"\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions { HasHeader = true };

        var items = Read.CsvSync<QuotedRecord>(ms, opts).ToList();

        Assert.Single(items);
        Assert.Contains("Line1", items[0].Text);
        Assert.Contains("Line2", items[0].Text);
    }

    [Fact]
    public void CsvSync_TabSeparator_ParsesCorrectly()
    {
        var csv = "Id\tName\n1\tTabTest\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions { HasHeader = true, Separator = '\t' };

        var items = Read.CsvSync<TestRecord>(ms, opts).ToList();

        Assert.Single(items);
        Assert.Equal("TabTest", items[0].Name);
    }

    [Fact]
    public void CsvSync_EmptyFields_HandledCorrectly()
    {
        var csv = "Id,Name\n1,\n2,Test\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions { HasHeader = true };

        var items = Read.CsvSync<TestRecord>(ms, opts).ToList();

        Assert.Equal(2, items.Count);
        Assert.Equal("", items[0].Name);
        Assert.Equal("Test", items[1].Name);
    }

    [Fact]
    public void CsvSync_NoHeader_UsesSchema()
    {
        var csv = "1,NoHeader\n2,Test\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions { HasHeader = false, Schema = new[] { "Id", "Name" } };

        var items = Read.CsvSync<TestRecord>(ms, opts).ToList();

        Assert.Equal(2, items.Count);
        Assert.Equal("NoHeader", items[0].Name);
    }

    [Fact]
    public async Task Csv_ErrorActionSkip_ContinuesAfterError()
    {
        var csv = "Id,Name\nINVALID,Test\n2,Valid\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions { HasHeader = true, ErrorAction = ReaderErrorAction.Skip };

        var items = new List<TestRecord>();
        await foreach (var item in Read.Csv<TestRecord>(ms, opts))
        {
            items.Add(item);
        }

        Assert.Single(items);
        Assert.Equal("Valid", items[0].Name);
    }

    [Fact]
    public async Task Csv_ErrorActionStop_StopsOnFirstError()
    {
        var csv = "Id,Name\nINVALID,Test\n2,Valid\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions { HasHeader = true, ErrorAction = ReaderErrorAction.Stop };

        var items = new List<TestRecord>();
        await foreach (var item in Read.Csv<TestRecord>(ms, opts))
        {
            items.Add(item);
        }

        Assert.Empty(items);
    }

    [Fact]
    public void CsvSync_LargeFile_HandlesBuffering()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Id,Name");
        for (int i = 0; i < 1000; i++)
        {
            sb.AppendLine($"{i},Name{i}");
        }
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
        var opts = new CsvReadOptions { HasHeader = true };

        var items = Read.CsvSync<TestRecord>(ms, opts).ToList();

        Assert.Equal(1000, items.Count);
    }

    [Fact]
    public void CsvSync_CRLFLineEndings_ParsesCorrectly()
    {
        var csv = "Id,Name\r\n1,CRLF\r\n2,Test\r\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions { HasHeader = true };

        var items = Read.CsvSync<TestRecord>(ms, opts).ToList();

        Assert.Equal(2, items.Count);
        Assert.Equal("CRLF", items[0].Name);
    }

    [Fact]
    public async Task Csv_MetricsTracking_RecordsStats()
    {
        var csv = "Id,Name\n1,A\n2,B\n3,C\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions { HasHeader = true };

        var items = new List<TestRecord>();
        await foreach (var item in Read.Csv<TestRecord>(ms, opts))
        {
            items.Add(item);
        }

        Assert.Equal(3, items.Count);
        Assert.NotEqual(default, opts.Metrics.StartedUtc);
        Assert.NotNull(opts.Metrics.CompletedUtc);
    }

    #endregion

    #region JSON Streaming Tests

    [Fact]
    public async Task Json_IndentedFormat_ReadsCorrectly()
    {
        var json = @"[
  {
    ""Id"": 1,
    ""Name"": ""Indented""
  }
]";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<TestRecord> { RequireArrayRoot = true };

        var items = new List<TestRecord>();
        await foreach (var item in Read.Json<TestRecord>(ms, opts))
        {
            items.Add(item);
        }

        Assert.Single(items);
        Assert.Equal("Indented", items[0].Name);
    }

    [Fact]
    public async Task Json_EmptyArray_ReadsEmpty()
    {
        var json = "[]";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<TestRecord> { RequireArrayRoot = true };

        var items = new List<TestRecord>();
        await foreach (var item in Read.Json<TestRecord>(ms, opts))
        {
            items.Add(item);
        }

        Assert.Empty(items);
    }

    [Fact]
    public void JsonSync_LargeArray_HandlesManyItems()
    {
        var sb = new StringBuilder("[");
        for (int i = 0; i < 500; i++)
        {
            if (i > 0) sb.Append(",");
            sb.Append($"{{\"Id\":{i},\"Name\":\"Item{i}\"}}");
        }
        sb.Append("]");

        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
        var opts = new JsonReadOptions<TestRecord> { RequireArrayRoot = true };

        var items = Read.JsonSync<TestRecord>(ms, opts).ToList();

        Assert.Equal(500, items.Count);
    }

    [Fact]
    public async Task Json_ErrorActionSkip_ContinuesOnError()
    {
        // Array with mix of valid and invalid objects
        var json = "[{\"Id\":1,\"Name\":\"Valid\"},{\"Id\":\"notanint\",\"Name\":\"Invalid\"},{\"Id\":2,\"Name\":\"Valid2\"}]";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<TestRecord>
        {
            RequireArrayRoot = true,
            ErrorAction = ReaderErrorAction.Skip
        };

        var items = new List<TestRecord>();
        await foreach (var item in Read.Json<TestRecord>(ms, opts))
        {
            items.Add(item);
        }

        Assert.True(items.Count >= 1, "Should have at least one valid item");
    }

    #endregion

    #region Text Reader Additional Tests

    [Fact]
    public async Task Text_WithOptions_TracksMetrics()
    {
        var text = "Line1\nLine2\nLine3\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var opts = new TextReadOptions();

        var lines = new List<string>();
        await foreach (var line in Read.Text(ms, opts))
        {
            lines.Add(line);
        }

        Assert.Equal(3, lines.Count);
        Assert.Equal(3, opts.Metrics.RecordsEmitted);
        Assert.NotEqual(default, opts.Metrics.StartedUtc);
    }

    [Fact]
    public void TextSync_WithOptions_TracksMetrics()
    {
        var text = "A\nB\nC\nD\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var opts = new TextReadOptions();

        var lines = Read.TextSync(ms, opts).ToList();

        Assert.Equal(4, lines.Count);
        Assert.Equal(4, opts.Metrics.RecordsEmitted);
    }

    [Fact]
    public async Task Text_EmptyFile_ReadsEmpty()
    {
        var text = "";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));

        var lines = new List<string>();
        await foreach (var line in Read.Text(ms))
        {
            lines.Add(line);
        }

        Assert.Empty(lines);
    }

    [Fact]
    public async Task Text_SingleLine_NoNewline_ReadsSingle()
    {
        var text = "OnlyLine";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));

        var lines = new List<string>();
        await foreach (var line in Read.Text(ms))
        {
            lines.Add(line);
        }

        Assert.Single(lines);
        Assert.Equal("OnlyLine", lines[0]);
    }

    #endregion

    #region More CSV Edge Cases

    [Fact]
    public void CsvSync_WhitespaceOnlyField_PreservedAsIs()
    {
        var csv = "Id,Name\n1,   \n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions { HasHeader = true };

        var items = Read.CsvSync<TestRecord>(ms, opts).ToList();

        Assert.Single(items);
        Assert.Equal("   ", items[0].Name);
    }

    [Fact]
    public void CsvSync_QuotedFieldWithOnlyQuotes_ParsesCorrectly()
    {
        var csv = "Id,Text\n1,\"\"\"\"\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions { HasHeader = true };

        var items = Read.CsvSync<QuotedRecord>(ms, opts).ToList();

        Assert.Single(items);
        Assert.Equal("\"", items[0].Text);
    }

    [Fact]
    public async Task Csv_FileWithOnlyHeader_ReturnsEmpty()
    {
        var csv = "Id,Name\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions { HasHeader = true };

        var items = new List<TestRecord>();
        await foreach (var item in Read.Csv<TestRecord>(ms, opts))
        {
            items.Add(item);
        }

        Assert.Empty(items);
    }

    [Fact]
    public void CsvSync_MixedLineEndings_ParsesAll()
    {
        var csv = "Id,Name\r\n1,CRLF\n2,LF\r3,CR\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions { HasHeader = true };

        var items = Read.CsvSync<TestRecord>(ms, opts).ToList();

        Assert.True(items.Count >= 2, "Should parse at least CRLF and LF rows");
    }

    [Fact]
    public void CsvSync_EmptyFile_ReturnsEmpty()
    {
        var csv = "";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions { HasHeader = true };

        var items = Read.CsvSync<TestRecord>(ms, opts).ToList();

        Assert.Empty(items);
    }

    [Fact]
    public async Task Csv_VeryLongField_Handled()
    {
        var longName = new string('X', 10000);
        var csv = $"Id,Name\n1,{longName}\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions { HasHeader = true };

        var items = new List<TestRecord>();
        await foreach (var item in Read.Csv<TestRecord>(ms, opts))
        {
            items.Add(item);
        }

        Assert.Single(items);
        Assert.Equal(10000, items[0].Name.Length);
    }

    #endregion

    #region More JSON Edge Cases

    [Fact]
    public async Task Json_NestedObjects_Parsed()
    {
        var json = "[{\"Id\":1,\"Name\":\"Nested\",\"Extra\":{\"Ignored\":true}}]";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<TestRecord> { RequireArrayRoot = true };

        var items = new List<TestRecord>();
        await foreach (var item in Read.Json<TestRecord>(ms, opts))
        {
            items.Add(item);
        }

        Assert.Single(items);
        Assert.Equal("Nested", items[0].Name);
    }

    [Fact]
    public async Task Json_NullValues_Handled()
    {
        var json = "[{\"Id\":1,\"Name\":null}]";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<TestRecord> { RequireArrayRoot = true };

        var items = new List<TestRecord>();
        await foreach (var item in Read.Json<TestRecord>(ms, opts))
        {
            items.Add(item);
        }

        Assert.Single(items);
        Assert.Null(items[0].Name);
    }

    [Fact]
    public async Task Json_UnicodeStrings_Handled()
    {
        var json = "[{\"Id\":1,\"Name\":\"日本語\"}]";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<TestRecord> { RequireArrayRoot = true };

        var items = new List<TestRecord>();
        await foreach (var item in Read.Json<TestRecord>(ms, opts))
        {
            items.Add(item);
        }

        Assert.Single(items);
        Assert.Equal("日本語", items[0].Name);
    }

    [Fact]
    public async Task Json_EscapedStrings_Handled()
    {
        var json = "[{\"Id\":1,\"Name\":\"Line1\\nLine2\\tTab\"}]";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<TestRecord> { RequireArrayRoot = true };

        var items = new List<TestRecord>();
        await foreach (var item in Read.Json<TestRecord>(ms, opts))
        {
            items.Add(item);
        }

        Assert.Single(items);
        Assert.Contains("\n", items[0].Name);
        Assert.Contains("\t", items[0].Name);
    }

    [Fact]
    public void JsonSync_WithMetrics_TracksProgress()
    {
        var json = "[{\"Id\":1,\"Name\":\"A\"},{\"Id\":2,\"Name\":\"B\"}]";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<TestRecord> { RequireArrayRoot = true };

        var items = Read.JsonSync<TestRecord>(ms, opts).ToList();

        Assert.Equal(2, items.Count);
        Assert.NotEqual(default, opts.Metrics.StartedUtc);
    }

    #endregion

    #region Text Edge Cases

    [Fact]
    public async Task Text_BinaryNewlines_HandledCorrectly()
    {
        var text = "Line1\r\nLine2\rLine3\nLine4";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));

        var lines = new List<string>();
        await foreach (var line in Read.Text(ms))
        {
            lines.Add(line);
        }

        Assert.True(lines.Count >= 3, "Should handle mixed newlines");
    }

    [Fact]
    public void TextSync_WithEncoding_UsesCorrect()
    {
        var text = "Unicode: α β γ";
        var bytes = Encoding.UTF8.GetBytes(text);
        using var ms = new MemoryStream(bytes);
        var opts = new TextReadOptions { Encoding = Encoding.UTF8 };

        var lines = Read.TextSync(ms, opts).ToList();

        Assert.Single(lines);
        Assert.Contains("α", lines[0]);
    }

    #endregion
}
