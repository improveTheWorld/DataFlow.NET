using DataFlow;
using DataFlow.Data.Tests.Utilities;
using Xunit;
using System.Text;

namespace DataFlow.Data.Tests.Json;

/// <summary>
/// DX-focused tests for JSON null handling behavior.
/// Verifies that the framework behaves intuitively when JSON data contains
/// null values for non-nullable C# model properties.
/// </summary>
public class JsonNullHandlingTests
{
    #region Test Models

    /// <summary>Non-nullable decimal — the "gotcha" scenario.</summary>
    public class Product
    {
        public string Name { get; set; } = "";
        public decimal Price { get; set; }
        public int Quantity { get; set; }
    }

    /// <summary>Nullable decimal — the "safe" model.</summary>
    public class ProductNullable
    {
        public string Name { get; set; } = "";
        public decimal? Price { get; set; }
        public int? Quantity { get; set; }
    }

    #endregion

    #region Default behavior (ErrorAction.Throw) — should throw clearly, not silently corrupt

    [Fact]
    public void JsonSync_NullOnNonNullable_DefaultThrows()
    {
        // Arrange — Price is null, but model expects non-nullable decimal
        var json = """[{"Name":"Widget","Price":null,"Quantity":10}]""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act & Assert — Default ErrorAction is Throw, should not silently produce 0m
        var ex = Assert.ThrowsAny<Exception>(() =>
        {
            Read.JsonSync<Product>(stream).ToList();
        });

        // The error should be catchable — DX: user gets a clear signal, not silent 0
        Assert.NotNull(ex);
    }

    #endregion

    #region ErrorAction.Skip — intuitive: bad records are skipped, good ones pass through

    [Fact]
    public void JsonSync_NullOnNonNullable_SkipMode_SkipsBadRecords()
    {
        // Arrange — First record has null Price, second and third are fine
        var json = """[{"Name":"Null","Price":null,"Quantity":1},{"Name":"Good","Price":9.99,"Quantity":5},{"Name":"Also Good","Price":19.99,"Quantity":3}]""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var opts = new JsonReadOptions<Product>
        {
            ErrorAction = ReaderErrorAction.Skip
        };

        // Act
        var items = Read.JsonSync<Product>(stream, opts).ToList();

        // Assert — The null-price record should be skipped, good records preserved
        Assert.True(items.Count >= 1, "At least good records should come through");
        Assert.True(items.All(p => p.Price > 0), "No zero-price items from null corruption");
    }

    [Fact]
    public void JsonSync_NullOnNonNullable_SkipMode_ErrorSinkCapturesDetails()
    {
        // Arrange
        var json = """[{"Name":"Bad","Price":null,"Quantity":1},{"Name":"Good","Price":5.00,"Quantity":2}]""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        var errorSink = new InMemoryErrorSink();
        var opts = new JsonReadOptions<Product>
        {
            ErrorAction = ReaderErrorAction.Skip,
            ErrorSink = errorSink
        };

        // Act
        var items = Read.JsonSync<Product>(stream, opts).ToList();

        // Assert — DX: errors are captured, not lost
        Assert.NotEmpty(errorSink.Errors);  // The null→non-nullable error is captured
        Assert.True(items.Count >= 1);       // Good records still come through
    }

    #endregion

    #region Nullable model — the recommended pattern: just works

    [Fact]
    public void JsonSync_NullOnNullable_JustWorks()
    {
        // Arrange — Using nullable model: decimal? and int?
        var json = """[{"Name":"Widget","Price":null,"Quantity":null},{"Name":"Gadget","Price":29.99,"Quantity":10}]""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act — No special options needed!
        var items = Read.JsonSync<ProductNullable>(stream).ToList();

        // Assert — Nulls become null, populated values work fine
        Assert.Equal(2, items.Count);

        Assert.Equal("Widget", items[0].Name);
        Assert.Null(items[0].Price);       // null → null (not 0!)
        Assert.Null(items[0].Quantity);    // null → null (not 0!)

        Assert.Equal("Gadget", items[1].Name);
        Assert.Equal(29.99m, items[1].Price);
        Assert.Equal(10, items[1].Quantity);
    }

    [Fact]
    public void JsonSync_MixedNulls_NullableModel_AllRecordsPreserved()
    {
        // Arrange — Real-world scenario: some products have prices, some don't
        var json = """
        [
            {"Name":"Free Sample","Price":null,"Quantity":100},
            {"Name":"Budget Item","Price":0.99,"Quantity":50},
            {"Name":"Premium","Price":299.99,"Quantity":5},
            {"Name":"Coming Soon","Price":null,"Quantity":null}
        ]
        """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act
        var items = Read.JsonSync<ProductNullable>(stream).ToList();

        // Assert — ALL 4 records preserved, nulls intact
        Assert.Equal(4, items.Count);
        Assert.Equal(2, items.Count(p => p.Price == null));  // 2 null prices
        Assert.Equal(2, items.Count(p => p.Price != null));  // 2 populated prices
        Assert.Equal(0.99m, items[1].Price);
        Assert.Equal(299.99m, items[2].Price);
    }

    #endregion

    #region Missing fields (not null, just absent) — should also be intuitive

    [Fact]
    public void JsonSync_MissingField_NonNullable_GetsDefault()
    {
        // Arrange — Price field is completely absent from JSON
        var json = """[{"Name":"No Price"}]""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act — Missing fields should get C# defaults (0 for decimal)
        var items = Read.JsonSync<Product>(stream).ToList();

        // Assert — This should work: missing ≠ null, just uses default
        Assert.Single(items);
        Assert.Equal("No Price", items[0].Name);
        Assert.Equal(0m, items[0].Price);      // default(decimal) = 0
        Assert.Equal(0, items[0].Quantity);     // default(int) = 0
    }

    [Fact]
    public void JsonSync_MissingField_Nullable_GetsNull()
    {
        // Arrange
        var json = """[{"Name":"Sparse"}]""";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));

        // Act
        var items = Read.JsonSync<ProductNullable>(stream).ToList();

        // Assert — Missing fields become null (not 0)
        Assert.Single(items);
        Assert.Equal("Sparse", items[0].Name);
        Assert.Null(items[0].Price);
        Assert.Null(items[0].Quantity);
    }

    #endregion
}
