
using System.Linq;
using DataFlow;
using DataFlow.Data.Tests.Utilities;
using DataFlow.Data.Tests.Generators;
using Xunit;

namespace DataFlow.Data.Tests.Csv;

public class CsvParserTests
{
    private readonly string _root;
    private readonly DataSetGenerator.GeneratedFiles _files;

    public CsvParserTests()
    {
        _root = TempFileHelper.CreateTempDirectory("CsvParser");
        var cfg = new DataGenConfig { CsvRows = 500, CsvColumns = 6 };
        _files = DataSetGenerator.EnsureGenerated(_root, cfg);
    }

    private async Task EnumerateAsync(bool asyncMode, string path, CsvReadOptions opts, Func<dynamic, bool>? perRow = null, int max = int.MaxValue)
    {
        int count = 0;
        if (asyncMode)
        {
            await foreach (var row in Read.Csv<dynamic>(path, opts))
            {
                count++;
                if (perRow != null && !perRow(row)) break;
                if (count >= max) break;
            }
        }
        else
        {
            foreach (var row in Read.CsvSync<dynamic>(path, opts))
            {
                count++;
                if (perRow != null && !perRow(row)) break;
                if (count >= max) break;
            }
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Reads_Header_Correctly(bool asyncMode)
    {
        var opts = new CsvReadOptions
        {
            HasHeader = true,
            ErrorAction = ReaderErrorAction.Skip,
            QuoteMode = CsvQuoteMode.Lenient
        };

        int count = 0;
        if (asyncMode)
        {
            await foreach (var row in Read.Csv<dynamic>(_files.CsvPath, opts))
            {
                count++;
                if (count >= 5) break;
            }
        }
        else
        {
            foreach (var row in Read.CsvSync<dynamic>(_files.CsvPath, opts))
            {
                count++;
                if (count >= 5) break;
            }
        }

        Assert.True(count > 0);
        Assert.NotNull(opts.Schema);
        Assert.Equal(6, opts.Schema!.Length);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Reads_NoHeader_WithSchema(bool asyncMode)
    {
        var opts = new CsvReadOptions
        {
            HasHeader = false,
            Schema = new[] { "A", "B", "C", "D", "E", "F" },
            ErrorAction = ReaderErrorAction.Skip,
            QuoteMode = CsvQuoteMode.Lenient
        };

        int count = 0;
        if (asyncMode)
        {
            await foreach (var row in Read.Csv<dynamic>(_files.CsvHeaderlessPath, opts))
            {
                count++;
                if (count >= 5) break;
            }
        }
        else
        {
            foreach (var row in Read.CsvSync<dynamic>(_files.CsvHeaderlessPath, opts))
            {
                count++;
                if (count >= 5) break;
            }
        }

        Assert.True(count > 0);
        Assert.Equal(6, opts.Schema!.Length);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task StrayQuote_Error_Reported(bool asyncMode)
    {
        var sink = new InMemoryErrorSink();
        var opts = new CsvReadOptions
        {
            HasHeader = true,
            QuoteMode = CsvQuoteMode.RfcStrict,
            ErrorAction = ReaderErrorAction.Skip,
            ErrorSink = sink
        };
        int processed = 0;
        if (asyncMode)
        {
            await foreach (var r in Read.Csv<dynamic>(_files.CsvPath, opts))
                processed++;
        }
        else
        {
            foreach (var r in Read.CsvSync<dynamic>(_files.CsvPath, opts))
                processed++;
        }

        Assert.True(sink.Errors.Any(e => e.ErrorType == "CsvQuoteError"));
        Assert.True(processed > 0);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TrailingGarbageAfterQuote_Error_When_Strict(bool asyncMode)
    {
        var sink = new InMemoryErrorSink();
        var opts = new CsvReadOptions
        {
            HasHeader = true,
            ErrorOnTrailingGarbageAfterClosingQuote = true,
            ErrorAction = ReaderErrorAction.Skip,
            ErrorSink = sink
        };
        if (asyncMode)
        {
            await foreach (var _ in Read.Csv<dynamic>(_files.CsvPath, opts)) { }
        }
        else
        {
            foreach (var _ in Read.CsvSync<dynamic>(_files.CsvPath, opts)) { }
        }
        Assert.Contains(sink.Errors, e => e.Message.Contains("Illegal character"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task UnterminatedQuote_AtEOF_Reported(bool asyncMode)
    {
        var path = Path.Combine(_root, $"unterminated_{(asyncMode ? "async" : "sync")}.csv");
        File.WriteAllText(path, "Col1\n\"Unfinished");
        var sink = new InMemoryErrorSink();
        var opts = new CsvReadOptions { ErrorAction = ReaderErrorAction.Skip, ErrorSink = sink };
        if (asyncMode)
        {
            await foreach (var _ in Read.Csv<dynamic>(path, opts)) { }
        }
        else
        {
            foreach (var _ in Read.CsvSync<dynamic>(path, opts)) { }
        }
        Assert.Contains(sink.Errors, e => e.ErrorType == "CsvQuoteError" && e.Message.Contains("Unterminated"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExtraFields_Error_When_NotAllowed(bool asyncMode)
    {
        var path = Path.Combine(_root, $"extra_{(asyncMode ? "async" : "sync")}.csv");
        File.WriteAllText(path, "A,B\n1,2,3");
        var sink = new InMemoryErrorSink();
        var opts = new CsvReadOptions
        {
            HasHeader = true,
            AllowExtraFields = false,
            ErrorAction = ReaderErrorAction.Skip,
            ErrorSink = sink
        };
        if (asyncMode)
        {
            await foreach (var _ in Read.Csv<dynamic>(path, opts)) { }
        }
        else
        {
            foreach (var _ in Read.CsvSync<dynamic>(path, opts)) { }
        }
        Assert.Contains(sink.Errors, e => e.ErrorType == "SchemaError" && e.Message.Contains("fields"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MissingTrailingFields_Error_When_NotAllowed(bool asyncMode)
    {
        var path = Path.Combine(_root, $"missing_{(asyncMode ? "async" : "sync")}.csv");
        File.WriteAllText(path, "A,B,C\n1,2");
        var sink = new InMemoryErrorSink();
        var opts = new CsvReadOptions
        {
            HasHeader = true,
            AllowMissingTrailingFields = false,
            ErrorAction = ReaderErrorAction.Skip,
            ErrorSink = sink
        };
        if (asyncMode)
        {
            await foreach (var _ in Read.Csv<dynamic>(path, opts)) { }
        }
        else
        {
            foreach (var _ in Read.CsvSync<dynamic>(path, opts)) { }
        }
        Assert.Contains(sink.Errors, e => e.ErrorType == "SchemaError" && e.Message.Contains("Missing field"));
    }


    // -----------------------
    // New behavior consolidation tests (full raw capture toggle + prefix excerpt)
    // -----------------------

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RawRecordObserver_Receives_Data_Rows_Not_Header(bool asyncMode)
    {
        var observed = new List<(long n, string raw)>();
        var opts = new CsvReadOptions
        {
            HasHeader = true,
            RawRecordObserver = (n, raw) => observed.Add((n, raw)),
            ErrorAction = ReaderErrorAction.Skip
        };

        if (asyncMode)
        {
            await EnumerateAsync(true, _files.CsvPath, opts, max: 5);
        }
        else
        {
            await EnumerateAsync(false, _files.CsvPath, opts, max: 5);
        }

        Assert.NotEmpty(observed);
        // Should not include header; first logical data record is 1
        Assert.Equal(1, observed.First().n);
        // Ensure we captured multiple rows and they look like CSV (contain separator)
        Assert.True(observed.All(t => t.n >= 1));
        Assert.True(observed.Any(t => t.raw.Contains(",")));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Excerpt_Uses_128_Prefix_And_Available_Without_Observer(bool asyncMode)
    {
        // Create a line that violates MaxRawRecordLength with long raw text
        var path = Path.Combine(_root, $"guard_prefix_{(asyncMode ? "async" : "sync")}.csv");
        var longCell = new string('X', 1024);
        // header + one very long data row
        File.WriteAllText(path, $"A,B\n\"{longCell}\",{longCell}");

        var sink = new InMemoryErrorSink();
        var opts = new CsvReadOptions
        {
            HasHeader = true,
            // No RawRecordObserver set: only prefix buffer should be used
            MaxRawRecordLength = 100, // small limit to trigger CsvLimitExceeded
            ErrorAction = ReaderErrorAction.Skip,
            ErrorSink = sink
        };

        if (asyncMode)
        {
            await foreach (var _ in Read.Csv<dynamic>(path, opts)) { }
        }
        else
        {
            foreach (var _ in Read.CsvSync<dynamic>(path, opts)) { }
        }

        var err = sink.Errors.FirstOrDefault(e => e.ErrorType == "CsvLimitExceeded");
        Assert.NotNull(err);
        Assert.False(string.IsNullOrEmpty(err!.RawExcerpt));
        // Excerpt must be at most 128 chars
        Assert.InRange(err.RawExcerpt.Length, 1, 128);
        // Should look like raw CSV starting with quote
        Assert.True(err.RawExcerpt[0] == '"' || err.RawExcerpt[0] == 'X');
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MaxRawRecordLength_Uses_RawLength_Counter(bool asyncMode)
    {
        // Produce a record that would exceed limit based on raw char count
        var path = Path.Combine(_root, $"rawlen_{(asyncMode ? "async" : "sync")}.csv");
        var s = new string('A', 300);
        File.WriteAllText(path, $"H1\n\"{s}\""); // quoted to ensure raw includes quotes

        var sink = new InMemoryErrorSink();
        var opts = new CsvReadOptions
        {
            HasHeader = true,
            MaxRawRecordLength = 100, // low limit
            ErrorAction = ReaderErrorAction.Skip,
            ErrorSink = sink
        };

        if (asyncMode)
        {
            await foreach (var _ in Read.Csv<dynamic>(path, opts)) { }
        }
        else
        {
            foreach (var _ in Read.CsvSync<dynamic>(path, opts)) { }
        }

        var limitErr = sink.Errors.FirstOrDefault(e => e.ErrorType == "CsvLimitExceeded");
        Assert.NotNull(limitErr);
        Assert.Contains("Raw record length", limitErr!.Message);
    }

}