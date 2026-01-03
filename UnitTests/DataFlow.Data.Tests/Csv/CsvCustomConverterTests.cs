using DataFlow;
using System.Globalization;
using Xunit;

namespace DataFlow.Data.Tests.Csv;

public class CsvCustomConverterTests
{
  
    record MyRow    {
          public int Id { get; set; }
          public string Upper { get; set; } 
          public decimal Amount { get; set; }
    }

    [Fact]
    public async Task FieldValueConverter_Applies()
    {
        var path = Path.GetTempFileName();
        // Header: Id,Upper,Amount
        File.WriteAllText(path, "Id,Upper,Amount\n1,foo,12.34\n2,Bar,56.78");

        // Because CsvReadOptions.FieldValueConverter is Func<string, object?> (no column index / name),
        // we implement a generic converter that:
        // 1. Parses ints
        // 2. Parses decimals (InvariantCulture)
        // 3. Uppercases everything else (so the 'Upper' column gets transformed)
        var opts = new CsvReadOptions
        {
            HasHeader = true,
            FieldTypeInference = FieldTypeInferenceMode.Custom,
            FieldValueConverter = raw =>
            {
                if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var i))
                    return i;
                if (decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
                    return d;
                return raw.ToUpperInvariant();
            }
        };

        var rows = new List<MyRow>();
        await foreach (var r in Read.Csv<MyRow>(path, opts))
            rows.Add(r);

        Assert.Equal(2, rows.Count);

        Assert.Equal(1, rows[0].Id);
        Assert.Equal("FOO", rows[0].Upper);
        Assert.Equal(12.34m, rows[0].Amount);

        Assert.Equal(2, rows[1].Id);
        Assert.Equal("BAR", rows[1].Upper);
        Assert.Equal(56.78m, rows[1].Amount);
    }
}
