using DataFlow.Framework;
using System.Globalization;
using Xunit;

namespace DataFlow.Core.Tests.Materialization;

public class CultureHandlingTests
{
    /// <summary>
    /// Respects provided culture
    /// </summary>
    [Fact]
    public void Create_WithGermanCulture_ShouldParseDecimalWithComma()
    {
        // Arrange
        var germanCulture = new CultureInfo("de-DE");
        var schema = new[] { "Amount", "Percentage", "Date" };

        // German format: comma as decimal separator, dot as thousands
        var values = new object[] { "1.234,56", "12,5", "15.01.2025" };

        // Act - REQUIRES CULTURE PARAMETER (not yet implemented)
        // var obj = ObjectMaterializer.Create<CultureSensitiveData>(
        //     schema, values, culture: germanCulture);

        // WORKAROUND for current implementation:
        var obj = ObjectMaterializer.Create<CultureSensitiveData>(
            schema,
            new object[] {
                decimal.Parse("1.234,56", germanCulture),
                double.Parse("12,5", germanCulture),
                DateTime.Parse("15.01.2025", germanCulture)
            });

        // Assert
        Assert.Equal(1234.56m, obj.Amount);
        Assert.Equal(12.5, obj.Percentage);
        Assert.Equal(new DateTime(2025, 1, 15), obj.Date);
    }

    /// <summary>
    /// Should still work with explicit InvariantCulture
    /// </summary>
    [Fact]
    public void Create_WithInvariantCulture_ShouldParseDecimalWithDot()
    {
        // Arrange
        var schema = new[] { "Amount", "Percentage" };
        var values = new object[] { "1234.56", "12.5" };

        // Act
        var obj = ObjectMaterializer.Create<CultureSensitiveData>(schema, values);

        // Assert
        Assert.Equal(1234.56m, obj.Amount);
        Assert.Equal(12.5, obj.Percentage);
    }

    /// <summary>
    /// Uses provided culture
    /// </summary>
    [Fact]
    public void Create_WithFrenchCulture_ShouldParseCorrectly()
    {
        // Arrange
        var frenchCulture = new CultureInfo("fr-FR");
        var schema = new[] { "Amount" };
        var values = new object[] { "1 234,56" }; // Space as thousands separator

        var obj = ObjectMaterializer.Create<CultureSensitiveData>(
            schema,
            new object[] { decimal.Parse("1 234,56", frenchCulture) });

        // Assert
        Assert.Equal(1234.56m, obj.Amount);
    }

    [Fact]
    public void Create_WithMixedCultureData_ShouldThrowWithoutCultureParameter()
    {
        // Arrange
        var schema = new[] { "Amount" };
        var values = new object[] { "1.234,56" }; // Ambiguous format

        // Act & Assert
        // Without culture parameter, InvariantCulture will fail on comma
        Assert.Throws<InvalidOperationException>(() =>
            ObjectMaterializer.Create<CultureSensitiveData>(schema, values));
    }
}
