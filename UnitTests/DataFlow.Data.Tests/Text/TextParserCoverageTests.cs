using DataFlow;
using DataFlow.Data.Tests.Utilities;
using Xunit;
using System.Text;

namespace DataFlow.Data.Tests.Text;

/// <summary>
/// Additional tests for Read.Text to improve coverage of TextParser paths.
/// </summary>
public class TextParserCoverageTests
{
    #region Test Models

    public record LineItem(string Line);

    #endregion

    #region Stream-Based Tests

    [Fact]
    public void TextSync_Stream_BasicReading()
    {
        // Arrange
        var text = "line1\nline2\nline3\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var opts = new TextReadOptions();

        // Act
        var lines = Read.TextSync(stream, opts).ToList();

        // Assert
        Assert.Equal(3, lines.Count);
        Assert.Equal("line1", lines[0]);
        Assert.Equal("line2", lines[1]);
        Assert.Equal("line3", lines[2]);
    }

    [Fact]
    public void TextSync_Stream_CRLF()
    {
        // Arrange - Windows line endings
        var text = "line1\r\nline2\r\nline3\r\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var opts = new TextReadOptions();

        // Act
        var lines = Read.TextSync(stream, opts).ToList();

        // Assert
        Assert.Equal(3, lines.Count);
    }

    [Fact]
    public void TextSync_Stream_MixedLineEndings()
    {
        // Arrange - Mixed line endings
        var text = "unix\nwindows\r\nmac\r";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var opts = new TextReadOptions();

        // Act
        var lines = Read.TextSync(stream, opts).ToList();

        // Assert
        Assert.True(lines.Count >= 2);  // At least identifies separate lines
    }

    [Fact]
    public void TextSync_Stream_EmptyLines()
    {
        // Arrange - Has empty lines
        var text = "first\n\nsecond\n\n\nthird\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var opts = new TextReadOptions();

        // Act
        var lines = Read.TextSync(stream, opts).ToList();

        // Assert
        Assert.True(lines.Count >= 3);  // Should include empty lines
    }

    [Fact]
    public void TextSync_Stream_UnicodeContent()
    {
        // Arrange
        var text = "日本語\n한글\n中文\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var opts = new TextReadOptions();

        // Act
        var lines = Read.TextSync(stream, opts).ToList();

        // Assert
        Assert.Equal(3, lines.Count);
        Assert.Equal("日本語", lines[0]);
        Assert.Equal("한글", lines[1]);
        Assert.Equal("中文", lines[2]);
    }

    #endregion

    #region Metrics and Options

    [Fact]
    public void TextSync_Stream_MetricsPopulated()
    {
        // Arrange
        var text = "a\nb\nc\nd\ne\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var opts = new TextReadOptions();

        // Act
        var lines = Read.TextSync(stream, opts).ToList();

        // Assert
        Assert.Equal(5, lines.Count);
        Assert.NotNull(opts.Metrics);
        Assert.Equal(5, opts.Metrics.RecordsEmitted);
        Assert.NotNull(opts.Metrics.CompletedUtc);
    }

    [Fact]
    public void TextSync_Stream_WithFilePath()
    {
        // Arrange
        var text = "line1\nline2\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var opts = new TextReadOptions();

        // Act
        var lines = Read.TextSync(stream, opts, filePath: "test.txt").ToList();

        // Assert
        Assert.Equal(2, lines.Count);
    }

    #endregion

    #region Large File Simulation

    [Fact]
    public void TextSync_Stream_LargeFile()
    {
        // Arrange - Simulate large file (1000 lines)
        var sb = new StringBuilder();
        for (int i = 0; i < 1000; i++)
            sb.AppendLine($"line {i}");
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(sb.ToString()));
        var opts = new TextReadOptions();

        // Act
        var lines = Read.TextSync(stream, opts).ToList();

        // Assert
        Assert.Equal(1000, lines.Count);
        Assert.Equal("line 0", lines[0]);
        Assert.Equal("line 999", lines[999]);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void TextSync_Stream_EmptyFile()
    {
        // Arrange
        var text = "";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var opts = new TextReadOptions();

        // Act
        var lines = Read.TextSync(stream, opts).ToList();

        // Assert
        Assert.Empty(lines);
    }

    [Fact]
    public void TextSync_Stream_SingleLineNoNewline()
    {
        // Arrange - Single line without trailing newline
        var text = "just one line";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var opts = new TextReadOptions();

        // Act
        var lines = Read.TextSync(stream, opts).ToList();

        // Assert
        Assert.Single(lines);
        Assert.Equal("just one line", lines[0]);
    }

    [Fact]
    public void TextSync_SimpleOverload_Works()
    {
        // Arrange
        var text = "line1\nline2\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));

        // Act - Using simple overload (no options)
        var lines = Read.TextSync(stream).ToList();

        // Assert
        Assert.Equal(2, lines.Count);
    }

    #endregion

    #region Async Tests

    [Fact]
    public async Task TextAsync_Stream_Works()
    {
        // Arrange
        var text = "async1\nasync2\nasync3\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var opts = new TextReadOptions();

        // Act
        var lines = new List<string>();
        await foreach (var line in Read.Text(stream, opts))
        {
            lines.Add(line);
        }

        // Assert
        Assert.Equal(3, lines.Count);
    }

    [Fact]
    public async Task TextAsync_File_Works()
    {
        // Arrange
        var text = "file1\nfile2\nfile3\n";
        var tmpFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tmpFile, text);
            var opts = new TextReadOptions();

            // Act
            var lines = new List<string>();
            await foreach (var line in Read.Text(tmpFile, opts))
            {
                lines.Add(line);
            }

            // Assert
            Assert.Equal(3, lines.Count);
            Assert.Equal(3, opts.Metrics.RecordsEmitted);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    #endregion
}
