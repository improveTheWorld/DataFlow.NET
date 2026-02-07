using DataFlow.Framework;
using Xunit;

namespace DataFlow.Core.Tests.Materialization;

/// <summary>
/// NET-008: snake_case normalization (Pass 3) doesn't work.
/// The 5-pass schema resolution claims first_name → FirstName, but it silently fails.
/// Audit report v3 confirmed this is a real bug affecting Snowflake/Spark users.
/// </summary>
public class SnakeCaseNormalizationTests
{
    #region Models

    public class SnakePerson
    {
        public string FirstName { get; set; } = "";
        public int BirthYear { get; set; }
    }

    public class SnakeOrder
    {
        public int OrderId { get; set; }
        public decimal TotalAmount { get; set; }
        public string CustomerName { get; set; } = "";
    }

    public record SnakePersonRecord(string FirstName, int BirthYear);

    #endregion

    #region NET-008: snake_case → PascalCase resolution

    [Fact]
    public void SnakeCase_FirstName_ShouldMapToPascalCase()
    {
        // Audit report v3: this is the exact failing case
        // Doc: Materialization-Quick-Reference.md L79: first_name → FirstName
        var person = ObjectMaterializer.Create<SnakePerson>(
            schema: new[] { "first_name", "birth_year" },
            parameters: new object[] { "Charlie", 1990 }
        );

        Assert.Equal("Charlie", person.FirstName);
        Assert.Equal(1990, person.BirthYear);
    }

    [Fact]
    public void SnakeCase_Snowflake_OrderColumns_ShouldMap()
    {
        // Doc: Materialization-Quick-Reference.md L125-127
        // Snowflake/Spark example: order_id → OrderId, total_amount → TotalAmount
        var order = ObjectMaterializer.Create<SnakeOrder>(
            schema: new[] { "order_id", "total_amount", "customer_name" },
            parameters: new object[] { 42, 199.99m, "Alice" }
        );

        Assert.Equal(42, order.OrderId);
        Assert.Equal(199.99m, order.TotalAmount);
        Assert.Equal("Alice", order.CustomerName);
    }

    [Fact]
    public void SnakeCase_WithRecord_ShouldMapConstructorParams()
    {
        // Records use constructor matching — snake_case should resolve to ctor params too
        var person = ObjectMaterializer.Create<SnakePersonRecord>(
            schema: new[] { "first_name", "birth_year" },
            parameters: new object[] { "Charlie", 1990 }
        );

        Assert.Equal("Charlie", person.FirstName);
        Assert.Equal(1990, person.BirthYear);
    }

    [Fact]
    public void SnakeCase_SingleWord_ShouldStillWork()
    {
        // Edge case: single-word snake_case (no underscores) should still match
        var person = ObjectMaterializer.Create<SnakePerson>(
            schema: new[] { "firstname", "birthyear" },
            parameters: new object[] { "Charlie", 1990 }
        );

        // "firstname" should match "FirstName" via case-insensitive (Pass 2)
        Assert.Equal("Charlie", person.FirstName);
        Assert.Equal(1990, person.BirthYear);
    }

    [Fact]
    public void SnakeCase_MixedWithExact_ShouldResolveAll()
    {
        // Mix of exact and snake_case columns
        var order = ObjectMaterializer.Create<SnakeOrder>(
            schema: new[] { "OrderId", "total_amount", "customer_name" },
            parameters: new object[] { 42, 199.99m, "Alice" }
        );

        Assert.Equal(42, order.OrderId);        // exact match (Pass 1)
        Assert.Equal(199.99m, order.TotalAmount); // snake_case (Pass 3)
        Assert.Equal("Alice", order.CustomerName); // snake_case (Pass 3)
    }

    #endregion
}
