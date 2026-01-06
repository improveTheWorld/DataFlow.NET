using System.Text;
using System.Text.Json;
using DataFlow;
using Xunit;

namespace DataFlow.Data.Tests;

/// <summary>
/// Tests for simple stream overloads (minimal API without options object).
/// These test the symmetric File/Stream API pattern.
/// </summary>
public class SimpleStreamOverloadsTests
{
    #region Test Models

    // Using records with primary constructors like existing tests
    public record CsvRecord(int Id, string Name);
    public record JsonRecord(int Id, string Name);
    public record YamlPositionalRecord(int Id, string Name);

    // YAML: Using simple class with lowercase names for YamlDotNet compatibility
    public class YamlRecord
    {
        public int id { get; set; }
        public string name { get; set; } = "";
    }

    // BUG-001 FIX: Mutable class should now work (previously returned 0 rows)
    public class CsvMutableRecord
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    // Mutable classes for JSON and YAML testing
    public class JsonMutableRecord
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    public class YamlMutableRecord
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    #endregion


    #region CSV Simple Stream Tests

    [Fact]
    public async Task Csv_Stream_Simple_ReadsAllRows()
    {
        // Arrange
        var csv = "Id,Name\n1,Alice\n2,Bob\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var rows = new List<CsvRecord>();
        await foreach (var row in Read.Csv<CsvRecord>(stream))
            rows.Add(row);

        // Assert
        Assert.Equal(2, rows.Count);
        Assert.Equal(1, rows[0].Id);
        Assert.Equal("Alice", rows[0].Name);
        Assert.Equal(2, rows[1].Id);
        Assert.Equal("Bob", rows[1].Name);
    }

    [Fact]
    public void CsvSync_Stream_Simple_ReadsAllRows()
    {
        // Arrange
        var csv = "Id,Name\n1,Alice\n2,Bob\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var rows = Read.CsvSync<CsvRecord>(stream).ToList();

        // Assert
        Assert.Equal(2, rows.Count);
        Assert.Equal(1, rows[0].Id);
        Assert.Equal("Alice", rows[0].Name);
    }

    [Fact]
    public async Task Csv_Stream_Simple_LeavesStreamOpen()
    {
        // Arrange
        var csv = "Id,Name\n1,Test\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        await foreach (var _ in Read.Csv<CsvRecord>(stream)) { }

        // Assert - stream should still be usable
        Assert.True(stream.CanRead);
        stream.Position = 0; // Should not throw
    }

    [Fact]
    public async Task Csv_Stream_Simple_WithCustomSeparator()
    {
        // Arrange
        var csv = "Id;Name\n1;Alice\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var rows = new List<CsvRecord>();
        await foreach (var row in Read.Csv<CsvRecord>(stream, separator: ";"))
            rows.Add(row);

        // Assert
        Assert.Single(rows);
        Assert.Equal("Alice", rows[0].Name);
    }

    /// <summary>
    /// BUG-001 Regression Test: Mutable class with property setters should work.
    /// Previously this would silently return 0 rows.
    /// </summary>
    [Fact]
    public async Task Csv_Stream_Simple_MutableClass_ReadsAllRows()
    {
        // Arrange - use mutable class (not record)
        var csv = "Id,Name\n1,Alice\n2,Bob\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Act
        var rows = new List<CsvMutableRecord>();
        await foreach (var row in Read.Csv<CsvMutableRecord>(stream))
            rows.Add(row);

        // Assert - should work now after BUG-001 fix
        Assert.Equal(2, rows.Count);
        Assert.Equal(1, rows[0].Id);
        Assert.Equal("Alice", rows[0].Name);
        Assert.Equal(2, rows[1].Id);
        Assert.Equal("Bob", rows[1].Name);
    }

    #endregion

    #region JSON Simple Stream Tests

    [Fact]
    public async Task Json_Stream_Simple_ReadsAllItems()
    {
        // Arrange
        var json = "[{\"Id\":1,\"Name\":\"Alice\"},{\"Id\":2,\"Name\":\"Bob\"}]";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act
        var items = new List<JsonRecord>();
        await foreach (var item in Read.Json<JsonRecord>(stream))
            items.Add(item);

        // Assert
        Assert.Equal(2, items.Count);
        Assert.Equal(1, items[0].Id);
        Assert.Equal("Alice", items[0].Name);
    }

    [Fact]
    public void JsonSync_Stream_Simple_ReadsAllItems()
    {
        // Arrange
        var json = "[{\"Id\":1,\"Name\":\"Alice\"}]";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act
        var items = Read.JsonSync<JsonRecord>(stream).ToList();

        // Assert
        Assert.Single(items);
        Assert.Equal("Alice", items[0].Name);
    }

    [Fact]
    public async Task Json_Stream_Simple_LeavesStreamOpen()
    {
        // Arrange
        var json = "[{\"Id\":1,\"Name\":\"Test\"}]";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act
        await foreach (var _ in Read.Json<JsonRecord>(stream)) { }

        // Assert
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task Json_Stream_Simple_DefaultsAreCaseInsensitive()
    {
        // Arrange - lowercase property names
        var json = "[{\"id\":1,\"name\":\"Test\"}]";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act - no options passed, should use case-insensitive default
        var items = new List<JsonRecord>();
        await foreach (var item in Read.Json<JsonRecord>(stream))
            items.Add(item);

        // Assert
        Assert.Single(items);
        Assert.Equal("Test", items[0].Name);
    }

    /// <summary>
    /// Verify mutable class works with JSON (both positional record and mutable class should work).
    /// </summary>
    [Fact]
    public async Task Json_Stream_Simple_MutableClass_ReadsAllRows()
    {
        // Arrange
        var json = "[{\"Id\":1,\"Name\":\"Alice\"},{\"Id\":2,\"Name\":\"Bob\"}]";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act
        var items = new List<JsonMutableRecord>();
        await foreach (var item in Read.Json<JsonMutableRecord>(stream))
            items.Add(item);

        // Assert
        Assert.Equal(2, items.Count);
        Assert.Equal(1, items[0].Id);
        Assert.Equal("Alice", items[0].Name);
    }

    #endregion

    #region YAML Simple Stream Tests


    [Fact]
    public async Task Yaml_Stream_Simple_ReadsDocument()
    {
        // Arrange - lowercase keys to match YamlDotNet default naming
        var yaml = "id: 1\nname: Alice\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));

        // Act
        var items = new List<YamlRecord>();
        await foreach (var item in Read.Yaml<YamlRecord>(stream))
            items.Add(item);

        // Assert
        Assert.Single(items);
        Assert.Equal(1, items[0].id);
        Assert.Equal("Alice", items[0].name);
    }

    [Fact]
    public void YamlSync_Stream_Simple_ReadsDocument()
    {
        // Arrange
        var yaml = "id: 1\nname: Test\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));

        // Act
        var items = Read.YamlSync<YamlRecord>(stream).ToList();

        // Assert
        Assert.Single(items);
        Assert.Equal("Test", items[0].name);
    }

    [Fact]
    public async Task Yaml_Stream_Simple_LeavesStreamOpen()
    {
        // Arrange
        var yaml = "id: 1\nname: Test\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));

        // Act
        await foreach (var _ in Read.Yaml<YamlRecord>(stream)) { }

        // Assert
        Assert.True(stream.CanRead);
    }

    [Fact]
    public async Task Yaml_Stream_Simple_ReadsMultipleDocuments()
    {
        // Arrange - multiple YAML documents (sequence format like existing tests)
        var yaml = "- id: 1\n  name: Alice\n- id: 2\n  name: Bob\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));

        // Act
        var items = new List<YamlRecord>();
        await foreach (var item in Read.Yaml<YamlRecord>(stream))
            items.Add(item);

        // Assert
        Assert.Equal(2, items.Count);
        Assert.Equal("Alice", items[0].name);
        Assert.Equal("Bob", items[1].name);
    }

    /// <summary>
    /// Verify mutable class with PascalCase properties works with YAML (case-insensitive matching).
    /// </summary>
    [Fact]
    public async Task Yaml_Stream_Simple_MutableClass_ReadsAllRows()
    {
        // Arrange - PascalCase keys should match PascalCase properties (case-insensitive)
        var yaml = "Id: 1\nName: Alice\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));

        // Act
        var items = new List<YamlMutableRecord>();
        await foreach (var item in Read.Yaml<YamlMutableRecord>(stream))
            items.Add(item);

        // Assert
        Assert.Single(items);
        Assert.Equal(1, items[0].Id);
        Assert.Equal("Alice", items[0].Name);
    }

    // NOTE: Positional records (e.g., public record Foo(int Id, string Name)) 
    // are NOT supported by YAML reader because YamlDotNet requires mutable properties.
    // Use mutable classes or records with { get; set; } properties for YAML.

    #endregion


    #region Text Simple Stream Tests


    [Fact]
    public async Task Text_Stream_Simple_ReadsAllLines()
    {
        // Arrange
        var text = "Line 1\nLine 2\nLine 3\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));

        // Act
        var lines = new List<string>();
        await foreach (var line in Read.Text(stream))
            lines.Add(line);

        // Assert
        Assert.Equal(3, lines.Count);
        Assert.Equal("Line 1", lines[0]);
        Assert.Equal("Line 2", lines[1]);
        Assert.Equal("Line 3", lines[2]);
    }

    [Fact]
    public void TextSync_Stream_Simple_ReadsAllLines()
    {
        // Arrange
        var text = "Line A\nLine B\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));

        // Act
        var lines = Read.TextSync(stream).ToList();

        // Assert
        Assert.Equal(2, lines.Count);
        Assert.Equal("Line A", lines[0]);
    }

    [Fact]
    public async Task Text_Stream_Simple_LeavesStreamOpen()
    {
        // Arrange
        var text = "Test line\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));

        // Act
        await foreach (var _ in Read.Text(stream)) { }

        // Assert
        Assert.True(stream.CanRead);
    }

    #endregion
}
