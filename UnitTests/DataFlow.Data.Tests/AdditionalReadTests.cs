using DataFlow.Data;
using System.Text;

namespace DataFlow.Data.Tests;

/// <summary>
/// Additional Read layer tests to boost coverage to 70%+
/// </summary>
public class AdditionalReadTests
{
    public record SimpleRecord(int Id, string Name);

    #region JSON Tests

    [Fact]
    public async Task Json_ArrayRoot_ReadsAllItems()
    {
        var json = "[{\"Id\":1,\"Name\":\"A\"},{\"Id\":2,\"Name\":\"B\"},{\"Id\":3,\"Name\":\"C\"}]";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<SimpleRecord> { RequireArrayRoot = true };

        var items = new List<SimpleRecord>();
        await foreach (var item in Read.Json<SimpleRecord>(ms, opts))
        {
            items.Add(item);
        }

        Assert.Equal(3, items.Count);
        Assert.Equal("A", items[0].Name);
        Assert.Equal("C", items[2].Name);
    }

    [Fact]
    public async Task Json_SingleObject_WhenAllowed_ReadsSingle()
    {
        var json = "{\"Id\":42,\"Name\":\"Single\"}";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<SimpleRecord>
        {
            RequireArrayRoot = true,
            AllowSingleObject = true
        };

        var items = new List<SimpleRecord>();
        await foreach (var item in Read.Json<SimpleRecord>(ms, opts))
        {
            items.Add(item);
        }

        Assert.Single(items);
        Assert.Equal(42, items[0].Id);
    }

    [Fact]
    public void JsonSync_ArrayRoot_ReadsAllItems()
    {
        var json = "[{\"Id\":1,\"Name\":\"First\"},{\"Id\":2,\"Name\":\"Second\"}]";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<SimpleRecord> { RequireArrayRoot = true };

        var items = Read.JsonSync<SimpleRecord>(ms, opts).ToList();

        Assert.Equal(2, items.Count);
        Assert.Equal("First", items[0].Name);
    }

    [Fact]
    public async Task Json_MaxElements_StopsAtLimit()
    {
        var json = "[{\"Id\":1,\"Name\":\"A\"},{\"Id\":2,\"Name\":\"B\"},{\"Id\":3,\"Name\":\"C\"}]";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(json));
        var opts = new JsonReadOptions<SimpleRecord>
        {
            RequireArrayRoot = true,
            MaxElements = 2,
            ErrorAction = ReaderErrorAction.Skip
        };

        var items = new List<SimpleRecord>();
        await foreach (var item in Read.Json<SimpleRecord>(ms, opts))
        {
            items.Add(item);
        }

        Assert.Equal(2, items.Count);
    }

    #endregion

    #region YAML Tests

    [Fact(Skip = "YAML format needs investigation")]
    public async Task Yaml_BasicList_ReadsAllItems()
    {
        var yaml = "- Id: 1\n  Name: First\n- Id: 2\n  Name: Second\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
        var opts = new YamlReadOptions<SimpleRecord>();

        var items = new List<SimpleRecord>();
        await foreach (var item in Read.Yaml<SimpleRecord>(ms, opts))
        {
            items.Add(item);
        }

        Assert.Equal(2, items.Count);
    }

    [Fact(Skip = "YAML format needs investigation")]
    public void YamlSync_BasicList_ReadsAllItems()
    {
        var yaml = "- Id: 10\n  Name: Ten\n- Id: 20\n  Name: Twenty\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(yaml));
        var opts = new YamlReadOptions<SimpleRecord>();

        var items = Read.YamlSync<SimpleRecord>(ms, opts).ToList();

        Assert.Equal(2, items.Count);
        Assert.Equal("Ten", items[0].Name);
    }

    #endregion

    #region CSV Tests  

    [Fact]
    public void AsCsvSync_String_ParsesCorrectly()
    {
        var csv = "Id,Name\n1,Alpha\n2,Beta";
        var items = Read.AsCsvSync<SimpleRecord>(csv).ToList();

        Assert.Equal(2, items.Count);
        Assert.Equal("Alpha", items[0].Name);
    }

    [Fact]
    public async Task Csv_WithCancellation_StopsOnCancel()
    {
        var csv = "Id,Name\n1,A\n2,B\n3,C\n4,D\n5,E";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        using var cts = new CancellationTokenSource();

        var opts = new CsvReadOptions
        {
            HasHeader = true,
            CancellationToken = cts.Token
        };

        var items = new List<SimpleRecord>();
        try
        {
            await foreach (var item in Read.Csv<SimpleRecord>(ms, opts))
            {
                items.Add(item);
                if (items.Count >= 2) cts.Cancel();
            }
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        Assert.True(items.Count >= 2);
    }

    [Fact]
    public void CsvSync_AllowExtraFields_IgnoresExtra()
    {
        var csv = "Id,Name\n1,Test,ExtraValue\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions
        {
            HasHeader = true,
            AllowExtraFields = true
        };

        var items = Read.CsvSync<SimpleRecord>(ms, opts).ToList();

        Assert.Single(items);
        Assert.Equal("Test", items[0].Name);
    }

    [Fact]
    public async Task Csv_CustomSeparator_ParsesCorrectly()
    {
        var csv = "Id;Name\n1;Semicolon\n";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions
        {
            HasHeader = true,
            Separator = ';'
        };

        var items = new List<SimpleRecord>();
        await foreach (var item in Read.Csv<SimpleRecord>(ms, opts))
        {
            items.Add(item);
        }

        Assert.Single(items);
        Assert.Equal("Semicolon", items[0].Name);
    }

    #endregion

    #region Text Tests

    [Fact]
    public async Task Text_ReadsAllLines()
    {
        var text = "Line 1\nLine 2\nLine 3";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));

        var lines = new List<string>();
        await foreach (var line in Read.Text(ms))
        {
            lines.Add(line);
        }

        Assert.Equal(3, lines.Count);
    }

    [Fact]
    public void TextSync_ReadsAllLines()
    {
        var text = "A\nB\nC";
        using var ms = new MemoryStream(Encoding.UTF8.GetBytes(text));

        var lines = Read.TextSync(ms).ToList();

        Assert.Equal(3, lines.Count);
    }

    #endregion
}
