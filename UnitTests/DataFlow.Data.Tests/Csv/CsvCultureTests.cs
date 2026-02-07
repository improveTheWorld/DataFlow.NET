using DataFlow;
using Xunit;
using System.Globalization;
using System.Text;

namespace DataFlow.Data.Tests.Csv;

/// <summary>
/// Tests for culture-aware CSV parsing via CsvReadOptions.FormatProvider.
/// Verifies that European decimal formats (comma as decimal separator)
/// are correctly parsed when the appropriate culture is specified.
/// </summary>
public class CsvCultureTests
{
    #region Test Models

    public class Invoice
    {
        public string Product { get; set; } = "";
        public decimal Amount { get; set; }
        public double Tax { get; set; }
        public int Quantity { get; set; }
    }

    public class SimpleAmount
    {
        public string Label { get; set; } = "";
        public decimal Value { get; set; }
    }

    #endregion

    #region French Culture (fr-FR) — comma decimal, space thousands

    [Fact]
    public void CsvSync_FrenchCulture_ParsesCommaDecimal()
    {
        // Arrange — French CSV: semicolon separator, comma decimal
        var csv = "Product;Amount;Tax;Quantity\nWidget;1234,50;19,60;10\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var opts = new CsvReadOptions
        {
            Separator = ";",
            HasHeader = true,
            FormatProvider = new CultureInfo("fr-FR")
        };

        // Act
        var items = Read.CsvSync<Invoice>(stream, opts).ToList();

        // Assert
        Assert.Single(items);
        Assert.Equal(1234.50m, items[0].Amount);
        Assert.Equal(19.60, items[0].Tax, precision: 2);
        Assert.Equal(10, items[0].Quantity);
        Assert.Equal("Widget", items[0].Product);
    }

    [Fact]
    public void CsvSync_FrenchCulture_MultipleRows()
    {
        // Arrange — Multiple rows with French decimal format
        var csv = "Label;Value\nAlpha;100,25\nBeta;200,75\nGamma;0,99\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var opts = new CsvReadOptions
        {
            Separator = ";",
            HasHeader = true,
            FormatProvider = new CultureInfo("fr-FR")
        };

        // Act
        var items = Read.CsvSync<SimpleAmount>(stream, opts).ToList();

        // Assert
        Assert.Equal(3, items.Count);
        Assert.Equal(100.25m, items[0].Value);
        Assert.Equal(200.75m, items[1].Value);
        Assert.Equal(0.99m, items[2].Value);
    }

    #endregion

    #region German Culture (de-DE) — comma decimal, dot thousands

    [Fact]
    public void CsvSync_GermanCulture_ParsesCommaDecimal()
    {
        // Arrange — German CSV: semicolon separator, comma decimal
        var csv = "Product;Amount;Tax;Quantity\nGadget;2500,99;475,19;5\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var opts = new CsvReadOptions
        {
            Separator = ";",
            HasHeader = true,
            FormatProvider = new CultureInfo("de-DE")
        };

        // Act
        var items = Read.CsvSync<Invoice>(stream, opts).ToList();

        // Assert
        Assert.Single(items);
        Assert.Equal(2500.99m, items[0].Amount);
        Assert.Equal(475.19, items[0].Tax, precision: 2);
    }

    #endregion

    #region InvariantCulture — backward compatibility

    [Fact]
    public void CsvSync_InvariantCulture_ParsesDotDecimal()
    {
        // Arrange — Standard US/Invariant CSV: comma separator, dot decimal
        var csv = "Product,Amount,Tax,Quantity\nWidget,1234.50,19.60,10\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var opts = new CsvReadOptions
        {
            HasHeader = true
            // FormatProvider defaults to InvariantCulture
        };

        // Act
        var items = Read.CsvSync<Invoice>(stream, opts).ToList();

        // Assert
        Assert.Single(items);
        Assert.Equal(1234.50m, items[0].Amount);
        Assert.Equal(19.60, items[0].Tax, precision: 2);
        Assert.Equal(10, items[0].Quantity);
    }

    [Fact]
    public void CsvSync_DefaultOptions_BackwardCompatible()
    {
        // Arrange — Ensure default behavior unchanged
        var csv = "Label,Value\nTest,42.5\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var opts = new CsvReadOptions { HasHeader = true };

        // Act
        var items = Read.CsvSync<SimpleAmount>(stream, opts).ToList();

        // Assert
        Assert.Single(items);
        Assert.Equal(42.5m, items[0].Value);
    }

    #endregion

    #region TextParser FormatProvider

    [Fact]
    public void TextParser_FrenchCulture_InfersCommaDecimal()
    {
        // Arrange
        var options = new TextParsingOptions
        {
            FormatProvider = new CultureInfo("fr-FR"),
            EnableDecimal = true
        };

        // Act
        var result = "1234,50".Parse(options);

        // Assert — Should parse as decimal 1234.50, not string
        Assert.IsType<decimal>(result);
        Assert.Equal(1234.50m, (decimal)result);
    }

    [Fact]
    public void TextParser_InvariantCulture_TreatsDotAsDecimal()
    {
        // Arrange — Default InvariantCulture
        var options = TextParsingOptions.Default;

        // Act
        var result = "1234.50".Parse(options);

        // Assert
        Assert.IsType<decimal>(result);
        Assert.Equal(1234.50m, (decimal)result);
    }

    [Fact]
    public void TextParser_GermanCulture_ParsesInteger()
    {
        // Arrange
        var options = new TextParsingOptions
        {
            FormatProvider = new CultureInfo("de-DE")
        };

        // Act
        var result = "42".Parse(options);

        // Assert — Integers should still work regardless of culture
        Assert.IsType<int>(result);
        Assert.Equal(42, (int)result);
    }

    [Fact]
    public void TextParser_TryParse_RespectsCulture()
    {
        // Arrange
        var frOptions = new TextParsingOptions
        {
            FormatProvider = new CultureInfo("fr-FR")
        };

        // Act
        var success = "99,99".TryParseAs<decimal>(out var value, frOptions);

        // Assert
        Assert.True(success);
        Assert.Equal(99.99m, value);
    }

    #endregion

    #region Async CSV with Culture

    [Fact]
    public async Task CsvAsync_FrenchCulture_Works()
    {
        // Arrange
        var csv = "Label;Value\nPrix;49,99\n";
        var tmpFile = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(tmpFile, csv);

            var opts = new CsvReadOptions
            {
                Separator = ";",
                HasHeader = true,
                FormatProvider = new CultureInfo("fr-FR")
            };

            // Act
            var items = new List<SimpleAmount>();
            await foreach (var item in Read.Csv<SimpleAmount>(tmpFile, opts))
            {
                items.Add(item);
            }

            // Assert
            Assert.Single(items);
            Assert.Equal(49.99m, items[0].Value);
            Assert.Equal("Prix", items[0].Label);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    #endregion

    #region String Extension with Culture

    [Fact]
    public void StringAsCsv_FrenchCulture_Works()
    {
        // Arrange
        var csv = "Label;Value\nArticle;15,75\n";

        var opts = new CsvReadOptions
        {
            Separator = ";",
            HasHeader = true,
            FormatProvider = new CultureInfo("fr-FR")
        };

        // Act
        var items = csv.AsCsv<SimpleAmount>(opts).ToList();

        // Assert
        Assert.Single(items);
        Assert.Equal(15.75m, items[0].Value);
    }

    #endregion
}
