using DataFlow.Data;
using DataFlow.Data.Tests.Utilities;
using Xunit;

namespace DataFlow.Data.Tests.Csv;

/// <summary>
/// Tests for CSV error recovery and edge cases that improve coverage
/// of CsvRfc4180Parser guard rails and error state machine.
/// </summary>
public class CsvErrorRecoveryTests
{
    #region Chunked Stream Tests (Buffer Boundary Coverage)

    [Theory]
    [InlineData(1)]   // Byte-by-byte
    [InlineData(4)]   // Small chunks
    [InlineData(16)]  // Medium chunks
    public void CsvSync_WithChunkedStream_ParsesCorrectly(int chunkSize)
    {
        // Arrange
        var csv = "Name,City\nAlice,Paris\nBob,London";
        using var stream = MockStreams.Chunked(csv, chunkSize);
        var options = new CsvReadOptions { HasHeader = true };

        // Act
        var items = Read.CsvSync<dynamic>(stream, options).ToList();

        // Assert
        Assert.Equal(2, items.Count);
    }

    [Fact]
    public async Task CsvAsync_WithChunkedStream_ParsesCorrectly()
    {
        // Arrange
        var csv = "Name,City\nAlice,Paris\nBob,London\nCharlie,Berlin";
        using var stream = MockStreams.Chunked(csv, 8);
        var options = new CsvReadOptions { HasHeader = true };

        // Act
        var items = new List<dynamic>();
        await foreach (var item in Read.Csv<dynamic>(stream, options))
        {
            items.Add(item);
        }

        // Assert
        Assert.Equal(3, items.Count);
    }

    #endregion

    #region Quoted Fields Split Across Chunks

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(10)]
    public void CsvSync_QuotedFieldSplitAcrossChunks(int chunkSize)
    {
        // Arrange - Quoted field with embedded newline
        var csv = "Col1,Col2\n\"line1\nline2\",value2\nsimple,data";
        using var stream = MockStreams.Chunked(csv, chunkSize);
        var options = new CsvReadOptions { HasHeader = true };

        // Act
        var items = Read.CsvSync<dynamic>(stream, options).ToList();

        // Assert
        Assert.Equal(2, items.Count);
    }

    #endregion

    #region Unicode Boundary Tests

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public void CsvSync_Unicode_ChunkedAcrossCharBoundary(int chunkSize)
    {
        // Arrange - Multi-byte UTF-8 characters
        var csv = "Name,City\n日本語,Tokyo\n한글,Seoul";
        using var stream = MockStreams.Chunked(csv, chunkSize);
        var options = new CsvReadOptions { HasHeader = true };

        // Act
        var items = Read.CsvSync<dynamic>(stream, options).ToList();

        // Assert
        Assert.Equal(2, items.Count);
    }

    #endregion

    #region CRLF Boundary Tests

    [Theory]
    [InlineData(1)]
    [InlineData(2)]  // Exactly CR+LF size
    [InlineData(5)]
    public void CsvSync_CrLf_ChunkedAcrossBoundary(int chunkSize)
    {
        // Arrange - CRLF line endings
        var csv = "Col1,Col2\r\na,b\r\nc,d\r\n";
        using var stream = MockStreams.Chunked(csv, chunkSize);
        var options = new CsvReadOptions { HasHeader = true };

        // Act
        var items = Read.CsvSync<dynamic>(stream, options).ToList();

        // Assert
        Assert.Equal(2, items.Count);
    }

    #endregion

    #region Metrics Tracking with Chunks

    [Fact]
    public void CsvSync_Chunked_MetricsAreAccurate()
    {
        // Arrange
        var csv = "Name,Value\nA,1\nB,2\nC,3";
        using var stream = MockStreams.Chunked(csv, 5);
        var options = new CsvReadOptions { HasHeader = true };

        // Act
        var items = Read.CsvSync<dynamic>(stream, options).ToList();

        // Assert
        Assert.Equal(3, items.Count);
        Assert.NotNull(options.Metrics);
        Assert.Equal(3, options.Metrics.RecordsEmitted);
        Assert.NotNull(options.Metrics.CompletedUtc);
    }

    #endregion

    #region Empty and Edge Cases

    [Fact]
    public void CsvSync_EmptyFile_Chunked()
    {
        // Arrange
        var csv = "";
        using var stream = MockStreams.Chunked(csv, 4);
        var options = new CsvReadOptions { HasHeader = false };

        // Act
        var items = Read.CsvSync<dynamic>(stream, options).ToList();

        // Assert
        Assert.Empty(items);
    }

    [Fact]
    public void CsvSync_HeaderOnly_Chunked()
    {
        // Arrange
        var csv = "Col1,Col2,Col3\n";
        using var stream = MockStreams.Chunked(csv, 4);
        var options = new CsvReadOptions { HasHeader = true };

        // Act
        var items = Read.CsvSync<dynamic>(stream, options).ToList();

        // Assert
        Assert.Empty(items);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void CsvSync_TrailingEmptyFields_Chunked(int chunkSize)
    {
        // Arrange
        var csv = "Col1,Col2,Col3\na,,\nb,c,";
        using var stream = MockStreams.Chunked(csv, chunkSize);
        var options = new CsvReadOptions { HasHeader = true };

        // Act
        var items = Read.CsvSync<dynamic>(stream, options).ToList();

        // Assert
        Assert.Equal(2, items.Count);
    }

    #endregion
}
