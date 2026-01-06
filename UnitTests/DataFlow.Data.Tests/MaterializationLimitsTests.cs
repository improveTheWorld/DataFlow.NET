using System.Text;
using DataFlow;
using Xunit;

namespace DataFlow.Data.Tests;

/// <summary>
/// Tests documenting the LIMITS and EDGE CASES of the ObjectMaterializer.
/// These tests verify what patterns work and which fail, serving as documentation.
/// </summary>
public class MaterializationLimitsTests
{
    #region Test Models - Working Patterns

    // ✅ WORKS: Mutable class with public setters
    public class MutableClass
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    // ✅ WORKS: Positional record (CSV/JSON only, NOT YAML)
    public record PositionalRecord(int Id, string Name);

    // ✅ WORKS: Record with mutable properties
    public record MutableRecord
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    #endregion

    #region Test Models - Edge Cases

    // ✅ WORKS: Private setter - ObjectMaterializer uses reflection
    public class PrivateSetterClass
    {
        public int Id { get; private set; }
        public string Name { get; private set; } = "";
    }

    // ❌ FAILS: Read-only properties (no setter at all)
    public class ReadOnlyPropertiesClass
    {
        public int Id { get; }
        public string Name { get; } = "";
        public ReadOnlyPropertiesClass() { }
    }

    // ✅ WORKS: Init-only properties - ObjectMaterializer can set them
    public class InitOnlyClass
    {
        public int Id { get; init; }
        public string Name { get; init; } = "";
    }

    // ⚠️ PARTIAL: Fields instead of properties - may work depending on visibility
    public class FieldsClass
    {
        public int Id;
        public string Name = "";
    }

    #endregion

    #region CSV - Working Patterns

    [Fact]
    public async Task Csv_MutableClass_Works()
    {
        var csv = "Id,Name\n1,Alice\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var items = new List<MutableClass>();
        await foreach (var item in Read.Csv<MutableClass>(stream))
            items.Add(item);

        Assert.Single(items);
        Assert.Equal(1, items[0].Id);
        Assert.Equal("Alice", items[0].Name);
    }

    [Fact]
    public async Task Csv_PositionalRecord_Works()
    {
        var csv = "Id,Name\n1,Alice\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var items = new List<PositionalRecord>();
        await foreach (var item in Read.Csv<PositionalRecord>(stream))
            items.Add(item);

        Assert.Single(items);
        Assert.Equal(1, items[0].Id);
        Assert.Equal("Alice", items[0].Name);
    }

    [Fact]
    public async Task Csv_MutableRecord_Works()
    {
        var csv = "Id,Name\n1,Alice\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var items = new List<MutableRecord>();
        await foreach (var item in Read.Csv<MutableRecord>(stream))
            items.Add(item);

        Assert.Single(items);
        Assert.Equal(1, items[0].Id);
        Assert.Equal("Alice", items[0].Name);
    }

    [Fact]
    public async Task Csv_PublicFields_Works()
    {
        var csv = "Id,Name\n1,Alice\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var items = new List<FieldsClass>();
        await foreach (var item in Read.Csv<FieldsClass>(stream))
            items.Add(item);

        Assert.Single(items);
        Assert.Equal(1, items[0].Id);
        Assert.Equal("Alice", items[0].Name);
    }

    #endregion

    #region CSV - Edge Cases that WORK

    [Fact]
    public async Task Csv_PrivateSetter_Works()
    {
        // Private setters ARE set by ObjectMaterializer (uses reflection)
        var csv = "Id,Name\n1,Alice\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var items = new List<PrivateSetterClass>();
        await foreach (var item in Read.Csv<PrivateSetterClass>(stream))
            items.Add(item);

        Assert.Single(items);
        // Properties ARE set even with private setter
        Assert.Equal(1, items[0].Id);
        Assert.Equal("Alice", items[0].Name);
    }

    #endregion

    #region CSV - Non-Working Patterns

    [Fact]
    public async Task Csv_ReadOnlyProperties_PropertiesRemainDefault()
    {
        // Read-only properties (no setter) cannot be set after construction
        var csv = "Id,Name\n1,Alice\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var items = new List<ReadOnlyPropertiesClass>();
        await foreach (var item in Read.Csv<ReadOnlyPropertiesClass>(stream))
            items.Add(item);

        Assert.Single(items);
        // Properties remain at default values (no setter exists)
        Assert.Equal(0, items[0].Id);
        Assert.Equal("", items[0].Name);
    }

    [Fact]
    public async Task Csv_InitOnly_Works()
    {
        // Init-only properties ARE set by ObjectMaterializer
        var csv = "Id,Name\n1,Alice\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var items = new List<InitOnlyClass>();
        await foreach (var item in Read.Csv<InitOnlyClass>(stream))
            items.Add(item);

        Assert.Single(items);
        // Init properties ARE set
        Assert.Equal(1, items[0].Id);
        Assert.Equal("Alice", items[0].Name);
    }

    #endregion

    #region JSON - Working Patterns

    [Fact]
    public async Task Json_MutableClass_Works()
    {
        var json = "[{\"Id\":1,\"Name\":\"Alice\"}]";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var items = new List<MutableClass>();
        await foreach (var item in Read.Json<MutableClass>(stream))
            items.Add(item);

        Assert.Single(items);
        Assert.Equal(1, items[0].Id);
        Assert.Equal("Alice", items[0].Name);
    }

    [Fact]
    public async Task Json_PositionalRecord_Works()
    {
        var json = "[{\"Id\":1,\"Name\":\"Alice\"}]";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var items = new List<PositionalRecord>();
        await foreach (var item in Read.Json<PositionalRecord>(stream))
            items.Add(item);

        Assert.Single(items);
        Assert.Equal(1, items[0].Id);
        Assert.Equal("Alice", items[0].Name);
    }

    [Fact]
    public async Task Json_PrivateSetter_PropertiesRemainDefault()
    {
        // JSON uses System.Text.Json which can't set private setters by default
        var json = "[{\"Id\":1,\"Name\":\"Alice\"}]";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var items = new List<PrivateSetterClass>();
        await foreach (var item in Read.Json<PrivateSetterClass>(stream))
            items.Add(item);

        Assert.Single(items);
        // Properties remain at default (System.Text.Json doesn't set private setters)
        Assert.Equal(0, items[0].Id);
        Assert.Equal("", items[0].Name);
    }

    [Fact]
    public async Task Json_InitOnly_Works()
    {
        var json = "[{\"Id\":1,\"Name\":\"Alice\"}]";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var items = new List<InitOnlyClass>();
        await foreach (var item in Read.Json<InitOnlyClass>(stream))
            items.Add(item);

        Assert.Single(items);
        Assert.Equal(1, items[0].Id);
        Assert.Equal("Alice", items[0].Name);
    }

    [Fact]
    public async Task Json_PublicFields_PropertiesRemainDefault()
    {
        // JSON (System.Text.Json) doesn't serialize/deserialize public fields by default
        var json = "[{\"Id\":1,\"Name\":\"Alice\"}]";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var items = new List<FieldsClass>();
        await foreach (var item in Read.Json<FieldsClass>(stream))
            items.Add(item);

        Assert.Single(items);
        // Fields remain at default (System.Text.Json only handles properties)
        Assert.Equal(0, items[0].Id);
        Assert.Equal("", items[0].Name);
    }

    #endregion

    #region YAML - Working Patterns

    // YAML record for mutable class test
    public class YamlMutableClass
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }

    [Fact]
    public async Task Yaml_MutableClass_Works()
    {
        var yaml = "Id: 1\nName: Alice\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(yaml));

        var items = new List<YamlMutableClass>();
        await foreach (var item in Read.Yaml<YamlMutableClass>(stream))
            items.Add(item);

        Assert.Single(items);
        Assert.Equal(1, items[0].Id);
        Assert.Equal("Alice", items[0].Name);
    }

    // YAML does NOT support positional records - YamlDotNet requires mutable properties
    // public record PositionalRecord(int Id, string Name); // Will fail with YAML

    #endregion


    #region Column Name Matching

    // ✅ Case-insensitive matching works
    [Fact]
    public async Task Csv_CaseInsensitiveMatching_Works()
    {
        // Lowercase headers match PascalCase properties
        var csv = "id,name\n1,Alice\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var items = new List<MutableClass>();
        await foreach (var item in Read.Csv<MutableClass>(stream))
            items.Add(item);

        Assert.Single(items);
        Assert.Equal(1, items[0].Id);
        Assert.Equal("Alice", items[0].Name);
    }

    // ✅ snake_case matching works
    [Fact]
    public async Task Csv_SnakeCaseMatching_Works()
    {
        // snake_case headers match PascalCase properties
        var csv = "my_id,user_name\n1,Alice\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        // Need a class with matching property names
        var items = new List<SnakeCaseTarget>();
        await foreach (var item in Read.Csv<SnakeCaseTarget>(stream))
            items.Add(item);

        Assert.Single(items);
        Assert.Equal(1, items[0].MyId);
        Assert.Equal("Alice", items[0].UserName);
    }

    public class SnakeCaseTarget
    {
        public int MyId { get; set; }
        public string UserName { get; set; } = "";
    }

    // ✅ Extra columns are ignored
    [Fact]
    public async Task Csv_ExtraColumns_Ignored()
    {
        var csv = "Id,Name,ExtraColumn\n1,Alice,ignored\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var items = new List<MutableClass>();
        await foreach (var item in Read.Csv<MutableClass>(stream))
            items.Add(item);

        Assert.Single(items);
        Assert.Equal(1, items[0].Id);
        Assert.Equal("Alice", items[0].Name);
    }

    // ✅ Missing columns get default values
    [Fact]
    public async Task Csv_MissingColumns_DefaultValue()
    {
        // Only Id column, Name is missing
        var csv = "Id\n1\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var items = new List<MutableClass>();
        await foreach (var item in Read.Csv<MutableClass>(stream))
            items.Add(item);

        Assert.Single(items);
        Assert.Equal(1, items[0].Id);
        Assert.Equal("", items[0].Name);  // Default value
    }

    #endregion
}
