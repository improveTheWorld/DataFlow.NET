using DataFlow.Data;
using DataFlow.Data.Tests.Utilities;
using Xunit;
using System.Text;

namespace DataFlow.Data.Tests.Csv;

/// <summary>
/// Additional CSV coverage tests for stream-based reading, async paths, and options.
/// </summary>
public class CsvCoverageTests
{
    #region Stream-Based Tests

    [Fact]
    public void CsvSync_Stream_BasicReading()
    {
        // Arrange
        var csv = "Name,City\nAlice,Paris\nBob,London\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions { HasHeader = true };

        // Act
        var items = Read.CsvSync<dynamic>(stream, opts).ToList();

        // Assert
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void CsvSync_Stream_NoHeader()
    {
        // Arrange
        var csv = "Alice,Paris\nBob,London\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions
        {
            HasHeader = false,
            Schema = new[] { "Name", "City" }
        };

        // Act
        var items = Read.CsvSync<dynamic>(stream, opts).ToList();

        // Assert
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void CsvSync_Stream_Metrics()
    {
        // Arrange
        var csv = "A,B\n1,2\n3,4\n5,6\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions { HasHeader = true };

        // Act
        var items = Read.CsvSync<dynamic>(stream, opts).ToList();

        // Assert
        Assert.Equal(3, items.Count);
        Assert.NotNull(opts.Metrics);
        Assert.Equal(3, opts.Metrics.RecordsEmitted);
        Assert.NotNull(opts.Metrics.CompletedUtc);
    }

    #endregion

    #region Quote Mode Tests

    [Fact]
    public void CsvSync_QuotedFields()
    {
        // Arrange
        var csv = "Name,City\n\"Alice Smith\",Paris\n\"Bob Jones\",London\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions { HasHeader = true };

        // Act
        var items = Read.CsvSync<dynamic>(stream, opts).ToList();

        // Assert
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void CsvSync_QuoteModeLenient()
    {
        // Arrange - Lenient mode allows some non-RFC quotes
        var csv = "Name,City\nAlice,Paris\nBob,London\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions
        {
            HasHeader = true,
            QuoteMode = CsvQuoteMode.Lenient
        };

        // Act
        var items = Read.CsvSync<dynamic>(stream, opts).ToList();

        // Assert
        Assert.Equal(2, items.Count);
    }

    #endregion

    #region Error Handling

    [Fact]
    public void CsvSync_ErrorAction_Skip()
    {
        // Arrange
        var csv = "A,B,C\n1,2,3\nmalformed\n4,5,6\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var errorSink = new InMemoryErrorSink();
        var opts = new CsvReadOptions
        {
            HasHeader = true,
            ErrorAction = ReaderErrorAction.Skip,
            ErrorSink = errorSink
        };

        // Act
        var items = Read.CsvSync<dynamic>(stream, opts).ToList();

        // Assert  
        Assert.True(items.Count >= 2);  // Should skip malformed
    }

    [Fact]
    public void CsvSync_EmptyFile()
    {
        // Arrange
        var csv = "";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions { HasHeader = false };

        // Act
        var items = Read.CsvSync<dynamic>(stream, opts).ToList();

        // Assert
        Assert.Empty(items);
    }

    [Fact]
    public void CsvSync_HeaderOnly()
    {
        // Arrange
        var csv = "A,B,C\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions { HasHeader = true };

        // Act
        var items = Read.CsvSync<dynamic>(stream, opts).ToList();

        // Assert
        Assert.Empty(items);
    }

    #endregion

    #region Async Tests

    [Fact]
    public async Task CsvAsync_Stream_Works()
    {
        // Arrange
        var csv = "Name,Value\nA,1\nB,2\nC,3\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions { HasHeader = true };

        // Act
        var items = new List<dynamic>();
        await foreach (var item in Read.Csv<dynamic>(stream, opts))
        {
            items.Add(item);
        }

        // Assert
        Assert.Equal(3, items.Count);
    }

    [Fact]
    public async Task CsvAsync_File_Works()
    {
        // Arrange
        var csv = "Name,Value\nX,10\nY,20\n";
        var tmpFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tmpFile, csv);
            var opts = new CsvReadOptions { HasHeader = true };

            // Act
            var items = new List<dynamic>();
            await foreach (var item in Read.Csv<dynamic>(tmpFile, opts))
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
    public async Task CsvAsync_SimpleOverload_Works()
    {
        // Arrange
        var csv = "Name,City\nTest,City\n";
        var tmpFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tmpFile, csv);

            // Act
            var items = new List<dynamic>();
            await foreach (var item in Read.Csv<dynamic>(tmpFile))
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

    #region Large Data

    [Fact]
    public void CsvSync_LargeFile()
    {
        // Arrange - 500 rows
        var sb = new StringBuilder();
        sb.AppendLine("Id,Name,Value");
        for (int i = 0; i < 500; i++)
            sb.AppendLine($"{i},name{i},{i * 10}");
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
        var opts = new CsvReadOptions { HasHeader = true };

        // Act
        var items = Read.CsvSync<dynamic>(stream, opts).ToList();

        // Assert
        Assert.Equal(500, items.Count);
    }

    #endregion

    #region Unicode and Special Characters

    [Fact]
    public void CsvSync_Unicode()
    {
        // Arrange
        var csv = "Name,City\n日本語,東京\n한글,서울\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions { HasHeader = true };

        // Act
        var items = Read.CsvSync<dynamic>(stream, opts).ToList();

        // Assert
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public void CsvSync_EmbeddedNewline()
    {
        // Arrange - Quoted field with embedded newline
        var csv = "Name,Desc\n\"Alice\",\"Line1\nLine2\"\n\"Bob\",\"Simple\"\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        var opts = new CsvReadOptions { HasHeader = true };

        // Act
        var items = Read.CsvSync<dynamic>(stream, opts).ToList();

        // Assert
        Assert.Equal(2, items.Count);
    }

    #endregion
}
