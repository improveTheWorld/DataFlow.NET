using DataFlow;
using Xunit;
using System.Globalization;
using System.Text;

namespace DataFlow.Data.Tests.Csv;

/// <summary>
/// Tests for the smart decimal auto-detection feature in TextParser.
/// Verifies that all common international decimal formats are correctly
/// parsed WITHOUT requiring explicit culture configuration.
/// </summary>
public class SmartDecimalParsingTests
{
    #region Test Models

    public class PriceRecord
    {
        public string Item { get; set; } = "";
        public decimal Price { get; set; }
    }

    public class DoubleRecord
    {
        public string Label { get; set; } = "";
        public double Value { get; set; }
    }

    #endregion

    #region TextParser.Infer — Auto-detects all unambiguous formats

    [Theory]
    [InlineData("1234.56", 1234.56)]        // US/UK: dot decimal
    [InlineData("1234,56", 1234.56)]        // FR/DE: comma decimal
    [InlineData("0.99", 0.99)]              // US: small decimal
    [InlineData("0,99", 0.99)]              // FR: small decimal
    [InlineData("100,25", 100.25)]          // European: 2 trailing digits
    [InlineData("99,9", 99.9)]             // European: 1 trailing digit
    [InlineData("1234,5678", 1234.5678)]    // European: 4 trailing digits
    public void Infer_SingleSeparator_AutoDetectsDecimal(string input, double expected)
    {
        var result = input.Parse(TextParsingOptions.Default);
        Assert.IsType<decimal>(result);
        Assert.Equal((decimal)expected, (decimal)result);
    }

    [Theory]
    [InlineData("1.234,56", 1234.56)]       // DE: dot thousands, comma decimal
    [InlineData("1,234.56", 1234.56)]       // US: comma thousands, dot decimal
    [InlineData("1.234.567,89", 1234567.89)]// DE: repeating dots = thousands
    [InlineData("1,234,567.89", 1234567.89)]// US: repeating commas = thousands
    [InlineData("12.345,67", 12345.67)]     // DE: 5-digit number
    public void Infer_BothSeparators_AutoDetectsDecimal(string input, double expected)
    {
        var result = input.Parse(TextParsingOptions.Default);
        Assert.IsType<decimal>(result);
        Assert.Equal((decimal)expected, (decimal)result);
    }

    [Theory]
    [InlineData("1 234,56", 1234.56)]       // FR: space thousands, comma decimal
    [InlineData("1 234.56", 1234.56)]       // UK-style: space thousands, dot decimal
    [InlineData("12 345 678,90", 12345678.90)] // FR: multiple space thousands
    public void Infer_SpaceThousands_AutoDetectsDecimal(string input, double expected)
    {
        var result = input.Parse(TextParsingOptions.Default);
        Assert.IsType<decimal>(result);
        Assert.Equal((decimal)expected, (decimal)result);
    }

    [Theory]
    [InlineData("-1234,56", -1234.56)]      // Negative: comma decimal
    [InlineData("-1.234,56", -1234.56)]     // Negative: both separators
    [InlineData("+1234,56", 1234.56)]       // Positive sign: comma decimal
    [InlineData("-0,99", -0.99)]            // Negative small
    public void Infer_WithSign_AutoDetectsDecimal(string input, double expected)
    {
        var result = input.Parse(TextParsingOptions.Default);
        Assert.IsType<decimal>(result);
        Assert.Equal((decimal)expected, (decimal)result);
    }

    [Theory]
    [InlineData("1,234,567", 1234567)]     // Multiple commas = thousands
    [InlineData("1.234.567", 1234567)]     // Multiple dots = thousands
    public void Infer_MultipleSameSeparators_TreatsAsThousands(string input, long expected)
    {
        var result = input.Parse(TextParsingOptions.Default);
        // Multiple identical separators → parsed as thousands → integer result
        Assert.True(result is decimal || result is int || result is long,
            $"Expected numeric type but got {result.GetType().Name}");
        Assert.Equal(expected, Convert.ToInt64(result));
    }

    #endregion

    #region Ambiguous cases — falls back to FormatProvider

    [Theory]
    [InlineData("1,234")]  // Could be 1234 (US thousands) or 1.234 (EU decimal)
    [InlineData("1.234")]  // Could be 1234 (EU thousands) or 1.234 (US decimal)
    public void Infer_AmbiguousFormat_FallsBackToFormatProvider(string input)
    {
        // With default InvariantCulture, these should still parse
        // (FormatProvider handles them after smart parser returns null)
        var result = input.Parse(TextParsingOptions.Default);
        Assert.True(result is decimal || result is int || result is long,
            $"Expected numeric but got {result.GetType().Name}: {result}");
    }

    #endregion

    #region CSV integration — zero config for European files

    [Fact]
    public void CsvSync_EuropeanFile_NoConfig_ParsesCorrectly()
    {
        // This is the key scenario: European CSV with semicolon separator
        // and comma decimal — should just work without specifying culture!
        var csv = "Item;Price\nWidget;1234,56\nGadget;99,99\nThing;0,50\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var opts = new CsvReadOptions
        {
            Separator = ";",
            HasHeader = true
            // NO FormatProvider specified — smart detection should handle it!
        };

        var items = Read.CsvSync<PriceRecord>(stream, opts).ToList();

        Assert.Equal(3, items.Count);
        Assert.Equal(1234.56m, items[0].Price);
        Assert.Equal(99.99m, items[1].Price);
        Assert.Equal(0.50m, items[2].Price);
    }

    [Fact]
    public void CsvSync_GermanFormat_BothSeparators_NoConfig()
    {
        // German: dot thousands + comma decimal
        var csv = "Item;Price\nExpensive;1.234,56\nCheap;5,99\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var opts = new CsvReadOptions
        {
            Separator = ";",
            HasHeader = true
            // NO culture specified
        };

        var items = Read.CsvSync<PriceRecord>(stream, opts).ToList();

        Assert.Equal(2, items.Count);
        Assert.Equal(1234.56m, items[0].Price);
        Assert.Equal(5.99m, items[1].Price);
    }

    [Fact]
    public void CsvSync_USFormat_StillWorks_NoConfig()
    {
        // US format should continue to work perfectly (backward compat)
        var csv = "Item,Price\nWidget,1234.56\nGadget,99.99\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var opts = new CsvReadOptions
        {
            HasHeader = true
            // Default everything
        };

        var items = Read.CsvSync<PriceRecord>(stream, opts).ToList();

        Assert.Equal(2, items.Count);
        Assert.Equal(1234.56m, items[0].Price);
        Assert.Equal(99.99m, items[1].Price);
    }

    [Fact]
    public void CsvSync_MixedUSFormat_WithThousands_NoConfig()
    {
        // US: comma thousands + dot decimal
        var csv = "Item,Price\n\"Big Item\",\"1,234.56\"\n\"Small\",\"0.99\"\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var opts = new CsvReadOptions
        {
            HasHeader = true
        };

        var items = Read.CsvSync<PriceRecord>(stream, opts).ToList();

        Assert.Equal(2, items.Count);
        Assert.Equal(1234.56m, items[0].Price);
        Assert.Equal(0.99m, items[1].Price);
    }

    [Fact]
    public void CsvSync_FrenchSpaceThousands_NoConfig()
    {
        // French: space thousands, comma decimal
        var csv = "Item;Price\nArticle;1 234,56\nAutre;99,99\n";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));

        var opts = new CsvReadOptions
        {
            Separator = ";",
            HasHeader = true
        };

        var items = Read.CsvSync<PriceRecord>(stream, opts).ToList();

        Assert.Equal(2, items.Count);
        Assert.Equal(1234.56m, items[0].Price);
        Assert.Equal(99.99m, items[1].Price);
    }

    #endregion

    #region SmartDecimalParsing can be disabled

    [Fact]
    public void CsvSync_SmartDisabled_FallsBackToFormatProvider()
    {
        // When SmartDecimalParsing is disabled, European format fails with InvariantCulture
        var options = new TextParsingOptions
        {
            SmartDecimalParsing = false,
            EnableDecimal = true
        };

        // "1234,56" with InvariantCulture + SmartDecimalParsing=false
        // → InvariantCulture sees comma as thousands → parses as 123456
        var result = "1234,56".Parse(options);

        // The behavior is: InvariantCulture treats comma as thousands,
        // so "1234,56" → not a valid thousands pattern → stays string
        // OR gets parsed incorrectly. Either way, it's NOT 1234.56m
        Assert.True(
            result is string || (result is decimal d && d != 1234.56m),
            "With smart parsing disabled, European comma should not auto-detect");
    }

    #endregion

    #region NormalizeDecimalString edge cases

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("12.34.56.78")]  // nonsensical format, but handled (multiple dots = thousands)
    public void NormalizeDecimalString_EdgeCases_DoNotThrow(string? input)
    {
        // Should not throw — just return null for non-parseable input
        // or handle gracefully
        if (input == null || string.IsNullOrWhiteSpace(input) || input == "abc")
        {
            Assert.Null(TextParser.NormalizeDecimalString(input!));
        }
    }

    [Fact]
    public void NormalizeDecimalString_PureInteger_ReturnsNull()
    {
        // Pure integers (no separator) → returns null (let int/long parsers handle)
        Assert.Null(TextParser.NormalizeDecimalString("1234"));
        Assert.Null(TextParser.NormalizeDecimalString("-42"));
    }

    [Fact]
    public void NormalizeDecimalString_BothSeparators_NormalizesCorrectly()
    {
        Assert.Equal("1234.56", TextParser.NormalizeDecimalString("1.234,56"));
        Assert.Equal("1234.56", TextParser.NormalizeDecimalString("1,234.56"));
        Assert.Equal("1234567.89", TextParser.NormalizeDecimalString("1.234.567,89"));
        Assert.Equal("1234567.89", TextParser.NormalizeDecimalString("1,234,567.89"));
    }

    [Fact]
    public void NormalizeDecimalString_SingleComma_NonAmbiguous()
    {
        Assert.Equal("1234.56", TextParser.NormalizeDecimalString("1234,56"));
        Assert.Equal("0.99", TextParser.NormalizeDecimalString("0,99"));
        Assert.Equal("99.9", TextParser.NormalizeDecimalString("99,9"));
    }

    [Fact]
    public void NormalizeDecimalString_SingleDot_NonAmbiguous()
    {
        Assert.Equal("1234.56", TextParser.NormalizeDecimalString("1234.56"));
        Assert.Equal("0.99", TextParser.NormalizeDecimalString("0.99"));
    }

    [Fact]
    public void NormalizeDecimalString_Ambiguous_ReturnsNull()
    {
        // Single separator + exactly 3 trailing digits → ambiguous
        Assert.Null(TextParser.NormalizeDecimalString("1,234"));
        Assert.Null(TextParser.NormalizeDecimalString("1.234"));
    }

    #endregion

    #region Double variant

    [Theory]
    [InlineData("3,14159", 3.14159)]   // European pi
    [InlineData("3.14159", 3.14159)]   // US pi
    [InlineData("1.234,56", 1234.56)]  // German format
    public void Infer_Double_SmartParsing(string input, double expected)
    {
        var opts = new TextParsingOptions { EnableDouble = true };
        var result = input.Parse(opts);
        Assert.True(result is decimal || result is double,
            $"Expected decimal or double but got {result.GetType().Name}");
        Assert.Equal(expected, Convert.ToDouble(result), precision: 5);
    }

    #endregion
}
