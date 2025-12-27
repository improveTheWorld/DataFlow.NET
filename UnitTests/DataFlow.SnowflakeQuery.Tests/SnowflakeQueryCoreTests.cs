using DataFlow.Data;
using Xunit;

namespace DataFlow.SnowflakeQuery.Tests;

/// <summary>
/// Unit tests for SnowflakeQuery SQL generation.
/// These tests validate SQL translation without requiring a real Snowflake connection.
/// They use the ToSql() method to verify correct SQL generation.
/// </summary>
public class SnowflakeQueryCoreTests
{
    private readonly SnowflakeOptions _mockOptions;

    public SnowflakeQueryCoreTests()
    {
        _mockOptions = new SnowflakeOptions
        {
            Account = "test_account",
            Database = "test_db",
            Schema = "test_schema",
            User = "test_user",
            Password = "test_pass",
            Warehouse = "test_warehouse"
        };
    }

    #region Basic Query Tests

    [Fact]
    public void ToSql_BasicQuery_GeneratesSelectStar()
    {
        // Arrange
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders");

        // Act
        var sql = query.ToSql();

        // Assert
        Assert.Contains("SELECT *", sql);
        Assert.Contains("FROM orders", sql);
    }

    [Fact]
    public void ToSql_Where_GeneratesWhereClause()
    {
        // Arrange
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .Where(o => o.Amount > 1000);

        // Act
        var sql = query.ToSql();

        // Assert
        Assert.Contains("WHERE", sql);
        Assert.Contains("amount", sql);
        Assert.Contains("> :p0", sql);  // Now uses parameter placeholder
    }

    [Fact]
    public void ToSql_MultipleWheres_GeneratesAndConditions()
    {
        // Arrange
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .Where(o => o.Amount > 1000)
            .Where(o => o.Status == "Active");

        // Act
        var sql = query.ToSql();

        // Assert
        Assert.Contains("AND", sql);
        Assert.Contains("amount", sql);
        Assert.Contains("status", sql);
    }

    [Fact]
    public void ToSql_OrderBy_GeneratesOrderByClause()
    {
        // Arrange
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .OrderBy(o => o.Amount);

        // Act
        var sql = query.ToSql();

        // Assert
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("amount", sql);
    }

    [Fact]
    public void ToSql_OrderByDescending_GeneratesDescClause()
    {
        // Arrange
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .OrderByDescending(o => o.Amount);

        // Act
        var sql = query.ToSql();

        // Assert
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("DESC", sql);
    }

    [Fact]
    public void ToSql_Take_GeneratesLimitClause()
    {
        // Arrange
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .Take(10);

        // Act
        var sql = query.ToSql();

        // Assert
        Assert.Contains("LIMIT 10", sql);
    }

    [Fact]
    public void ToSql_Skip_GeneratesOffsetClause()
    {
        // Arrange - Skip requires OrderBy for deterministic results
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .OrderBy(o => o.Id)
            .Skip(5);

        // Act
        var sql = query.ToSql();

        // Assert
        Assert.Contains("OFFSET 5", sql);
    }

    #endregion

    #region DateTime Property Tests

    [Fact]
    public void ToSql_DateTimeYear_GeneratesYearFunction()
    {
        // Arrange
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .Where(o => o.OrderDate.Year == 2024);

        // Act
        var sql = query.ToSql();

        // Assert
        Assert.Contains("YEAR(order_date)", sql);
        Assert.Contains("= :p0", sql);  // Parameter placeholder
    }

    [Fact]
    public void ToSql_DateTimeMonth_GeneratesMonthFunction()
    {
        // Arrange
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .Where(o => o.OrderDate.Month >= 6);

        // Act
        var sql = query.ToSql();

        // Assert
        Assert.Contains("MONTH(order_date)", sql);
        Assert.Contains(">= :p0", sql);  // Parameter placeholder
    }

    [Fact]
    public void ToSql_DateTimeDay_GeneratesDayFunction()
    {
        // Arrange
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .Where(o => o.OrderDate.Day == 15);

        // Act
        var sql = query.ToSql();

        // Assert
        Assert.Contains("DAY(order_date)", sql);
    }

    [Fact]
    public void ToSql_DateTimeHour_GeneratesHourFunction()
    {
        // Arrange
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .Where(o => o.OrderDate.Hour > 12);

        // Act
        var sql = query.ToSql();

        // Assert
        Assert.Contains("HOUR(order_date)", sql);
    }

    #endregion

    #region String Property Tests

    [Fact]
    public void ToSql_StringLength_GeneratesLengthFunction()
    {
        // Arrange
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .Where(o => o.CustomerName.Length > 10);

        // Act
        var sql = query.ToSql();

        // Assert
        Assert.Contains("LENGTH(customer_name)", sql);
        Assert.Contains("> :p0", sql);  // Parameter placeholder
    }

    [Fact]
    public void ToSql_StringIndexOf_GeneratesPositionFunction()
    {
        // Arrange
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .Where(o => o.CustomerName.IndexOf("Smith") >= 0);

        // Act
        var sql = query.ToSql();

        // Assert
        Assert.Contains("POSITION", sql);
        Assert.Contains(":p0", sql);  // Parameter placeholder for "Smith"
        Assert.Contains("- 1", sql); // 0-based adjustment
    }

    [Fact]
    public void ToSql_StringContains_GeneratesLikePattern()
    {
        // Arrange
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .Where(o => o.CustomerName.Contains("test"));

        // Act
        var sql = query.ToSql();

        // Assert
        Assert.Contains("LIKE CONCAT('%', :p0, '%')", sql);  // Parameterized LIKE pattern
    }

    #endregion

    #region Math Function Tests

    [Fact]
    public void ToSql_MathAbs_GeneratesAbsFunction()
    {
        // Arrange
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .Where(o => Math.Abs(o.Amount) > 100);

        // Act
        var sql = query.ToSql();

        // Assert
        Assert.Contains("ABS(amount)", sql);
        Assert.Contains("> :p0", sql);  // Parameter placeholder
    }

    [Fact]
    public void ToSql_MathRound_GeneratesRoundFunction()
    {
        // Arrange
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .Where(o => Math.Round(o.Amount) > 50);

        // Act
        var sql = query.ToSql();

        // Assert
        Assert.Contains("ROUND(amount)", sql);
    }

    [Fact]
    public void ToSql_MathRoundWithPrecision_GeneratesRoundWithDecimals()
    {
        // Arrange
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .Where(o => Math.Round(o.Amount, 2) > 50);

        // Act
        var sql = query.ToSql();

        // Assert
        Assert.Contains("ROUND(amount, 2)", sql);
    }

    [Fact]
    public void ToSql_MathCeiling_GeneratesCeilFunction()
    {
        // Arrange
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .Where(o => Math.Ceiling(o.Amount) > 100);

        // Act
        var sql = query.ToSql();

        // Assert
        Assert.Contains("CEIL(amount)", sql);
    }

    [Fact]
    public void ToSql_MathFloor_GeneratesFloorFunction()
    {
        // Arrange
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .Where(o => Math.Floor(o.Amount) > 100);

        // Act
        var sql = query.ToSql();

        // Assert
        Assert.Contains("FLOOR(amount)", sql);
    }

    [Fact]
    public void ToSql_MathSqrt_GeneratesSqrtFunction()
    {
        // Arrange
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .Where(o => Math.Sqrt((double)o.Amount) > 10);

        // Act
        var sql = query.ToSql();

        // Assert
        Assert.Contains("SQRT(amount)", sql);
    }

    #endregion

    #region Variant Any Tests

    [Fact]
    public void ToSql_VariantAny_GeneratesArraySizeFilter()
    {
        // Arrange
        var query = Read.SnowflakeTable<TestOrderWithItems>(_mockOptions, "orders")
            .Where(o => o.Items.Any(i => i.Price > 100));

        // Act
        var sql = query.ToSql();

        // Assert
        Assert.Contains("ARRAY_SIZE", sql);
        Assert.Contains("FILTER", sql);
        Assert.Contains("i:price", sql); // VARIANT colon syntax
    }

    [Fact]
    public void ToSql_VariantAll_GeneratesFilterNotClause()
    {
        // Arrange
        var query = new SnowflakeQuery<TestOrderWithItems>(_mockOptions, "orders_with_items");

        // Act - All items must have price > 0
        var result = query.Where(o => o.Items.All(i => i.Price > 0));
        var sql = result.ToSql();

        // Assert - All = no elements fail the predicate
        Assert.Contains("ARRAY_SIZE", sql);
        Assert.Contains("FILTER", sql);
        Assert.Contains("NOT", sql);
        Assert.Contains("i:price", sql);
    }

    [Fact]
    public void ToSql_VariantWhere_GeneratesFilterClause()
    {
        // Arrange
        var query = new SnowflakeQuery<TestOrderWithItems>(_mockOptions, "orders_with_items");

        // Act - Filter items to only those > 100
        var result = query.Select(o => new { o.Id, ExpensiveItems = o.Items.Where(i => i.Price > 100) });
        var sql = result.ToSql();

        // Assert
        Assert.Contains("FILTER", sql);
        Assert.Contains("i:price", sql);
    }

    [Fact]
    public void ToSql_VariantSelect_GeneratesTransformClause()
    {
        // Arrange
        var query = new SnowflakeQuery<TestOrderWithItems>(_mockOptions, "orders_with_items");

        // Act - Transform items to get doubled prices
        var result = query.Select(o => new { o.Id, DoubledPrices = o.Items.Select(i => i.Price * 2) });
        var sql = result.ToSql();

        // Assert
        Assert.Contains("TRANSFORM", sql);
        Assert.Contains("i:price", sql);
    }

    #endregion

    #region Parameterization Tests

    [Fact]
    public void QueryParameters_WithSingleValue_CollectsParameterWithCorrectValue()
    {
        // Arrange & Act
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .Where(o => o.Amount > 1000);

        var sql = query.ToSql();
        var parameters = query.QueryParameters;

        // Assert
        Assert.Single(parameters);
        Assert.Equal("p0", parameters[0].Name);
        Assert.Equal(1000m, parameters[0].Value);
        Assert.Contains(":p0", sql);
    }

    [Fact]
    public void QueryParameters_WithMultipleValues_CreatesSequentialParameters()
    {
        // Arrange & Act
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .Where(o => o.Amount > 100)
            .Where(o => o.Status == "Active");

        var sql = query.ToSql();
        var parameters = query.QueryParameters;

        // Assert - Multiple where clauses with different values
        Assert.True(parameters.Count >= 2);
        Assert.Contains(parameters, p => p.Name == "p0");
        Assert.Contains(parameters, p => p.Name == "p1");
        Assert.Contains(":p0", sql);
        Assert.Contains(":p1", sql);
    }

    [Fact]
    public void QueryParameters_WithStringValue_CollectsStringParameter()
    {
        // Arrange & Act
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .Where(o => o.CustomerName.Contains("Smith"));

        var sql = query.ToSql();
        var parameters = query.QueryParameters;

        // Assert
        Assert.Single(parameters);
        Assert.Equal("p0", parameters[0].Name);
        Assert.Equal("Smith", parameters[0].Value);
        Assert.Contains("LIKE CONCAT('%', :p0, '%')", sql);
    }

    [Fact]
    public void QueryParameters_WithMaliciousInput_SafelyParameterizes()
    {
        // Arrange - SQL injection attempt
        var maliciousInput = "'; DROP TABLE orders; --";

        // Act
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .Where(o => o.Status == maliciousInput);

        var sql = query.ToSql();
        var parameters = query.QueryParameters;

        // Assert - Malicious input is parameterized, not embedded in SQL
        Assert.DoesNotContain("DROP TABLE", sql);
        Assert.DoesNotContain(maliciousInput, sql);
        Assert.Contains(":p0", sql);
        Assert.Single(parameters);
        Assert.Equal(maliciousInput, parameters[0].Value);  // Value stored safely
    }

    [Fact]
    public void QueryParameters_WithBooleanValue_CollectsBooleanParameter()
    {
        // Arrange & Act
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .Where(o => o.IsInternational == true);

        var sql = query.ToSql();
        var parameters = query.QueryParameters;

        // Assert
        Assert.Single(parameters);
        Assert.Equal("p0", parameters[0].Name);
        Assert.Equal(true, parameters[0].Value);
        Assert.Contains(":p0", sql);
    }

    [Fact]
    public void QueryParameters_WithDateTimeValue_CollectsDateTimeParameter()
    {
        // Arrange
        var testDate = new DateTime(2024, 6, 15, 10, 30, 0);

        // Act
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .Where(o => o.OrderDate > testDate);

        var sql = query.ToSql();
        var parameters = query.QueryParameters;

        // Assert
        Assert.Single(parameters);
        Assert.Equal("p0", parameters[0].Name);
        Assert.Equal(testDate, parameters[0].Value);
        Assert.Contains(":p0", sql);
    }

    [Fact]
    public void QueryParameters_WithStartsWithPattern_UsesConcat()
    {
        // Arrange & Act
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .Where(o => o.CustomerName.StartsWith("John"));

        var sql = query.ToSql();
        var parameters = query.QueryParameters;

        // Assert - StartsWith should use CONCAT(param, '%')
        Assert.Single(parameters);
        Assert.Equal("p0", parameters[0].Name);
        Assert.Equal("John", parameters[0].Value);
        Assert.Contains("LIKE CONCAT(:p0, '%')", sql);
    }

    [Fact]
    public void QueryParameters_WithEndsWithPattern_UsesConcat()
    {
        // Arrange & Act
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .Where(o => o.CustomerName.EndsWith("son"));

        var sql = query.ToSql();
        var parameters = query.QueryParameters;

        // Assert - EndsWith should use CONCAT('%', param)
        Assert.Single(parameters);
        Assert.Equal("p0", parameters[0].Name);
        Assert.Equal("son", parameters[0].Value);
        Assert.Contains("LIKE CONCAT('%', :p0)", sql);
    }

    #endregion

    #region NULL Handling Tests

    [Fact]
    public void ToSql_EqualNull_GeneratesIsNull()
    {
        // Arrange & Act
        string? nullValue = null;
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .Where(o => o.Status == nullValue);

        var sql = query.ToSql();

        // Assert - Should generate IS NULL, not = NULL
        Assert.Contains("IS NULL", sql);
        Assert.DoesNotContain("= NULL", sql);
    }

    [Fact]
    public void ToSql_NotEqualNull_GeneratesIsNotNull()
    {
        // Arrange & Act
        string? nullValue = null;
        var query = Read.SnowflakeTable<TestOrder>(_mockOptions, "orders")
            .Where(o => o.Status != nullValue);

        var sql = query.ToSql();

        // Assert - Should generate IS NOT NULL, not <> NULL
        Assert.Contains("IS NOT NULL", sql);
        Assert.DoesNotContain("<> NULL", sql);
    }

    #endregion

    #region Test Models

    private class TestOrder
    {
        public int Id { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public bool IsInternational { get; set; }
        public DateTime OrderDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
    }

    private class TestOrderWithItems
    {
        public int Id { get; set; }
        public string CustomerId { get; set; } = string.Empty;
        [Variant]
        public List<LineItem> Items { get; set; } = new();
    }

    private class LineItem
    {
        public decimal Price { get; set; }
        public int Quantity { get; set; }
    }

    #endregion
}
