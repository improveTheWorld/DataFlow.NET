using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DataFlow;
using Xunit;

namespace DataFlow.Data.Tests.Csv;
public sealed class CollectingErrorSink : IReaderErrorSink
{
    public List<ReaderError> Errors { get; } = new();
    public void Report(ReaderError error) => Errors.Add(error);
    public void Dispose() { }
}

public class CsvGuardRailTests
{
    private sealed class Row
    {
        public string A { get; set; } = "";
        public string B { get; set; } = "";
        public string C { get; set; } = "";
    }


    [Fact]
    public async Task MaxColumnsPerRow_SkipBehavior()
    {
        string path = Path.GetTempFileName();
        try
        {
            // Header + 3 valid rows + 1 violating row (4 columns) + 1 more valid row
            File.WriteAllText(path,
                "A,B,C\n" +  // header
                "1,2,3\n" +   // ok
                "4,5,6\n" +   // ok
                "x,y,z,w\n" + // 4 cols -> violation
                "7,8,9\n");   // ok

            var sink = new CollectingErrorSink();
            var opts = new CsvReadOptions
            {
                HasHeader = true,
                MaxColumnsPerRow = 3,
                ErrorAction = ReaderErrorAction.Skip,
                ErrorSink = sink
            };

            var rows = new List<Row>();
            await foreach (var r in Read.Csv<Row>(path, opts))
                rows.Add(r);

            // RawRecordsParsed includes all data rows (4), even skipped one
            Assert.Equal(4, opts.Metrics.RawRecordsParsed);
            // RecordsEmitted excludes the violating row
            Assert.Equal(3, opts.Metrics.RecordsEmitted);
            Assert.Equal(1, sink.Errors.Count);
            var err = sink.Errors.Single();
            Assert.Equal("CsvLimitExceeded", err.ErrorType);
            Assert.Contains("Row has 4 columns", err.Message);
            Assert.Equal(ReaderErrorAction.Skip.ToString(), err.ActionChosen.ToString());

            // Ensure the violating row's data not materialized
            Assert.DoesNotContain(rows, r => r.A == "x" && r.B == "y" && r.C == "z");
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public async Task MaxColumnsPerRow_Throw()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "A,B,C\n1,2,3\nX,Y,Z,W\n5,6,7\n");
            var sink = new CollectingErrorSink();
            var opts = new CsvReadOptions
            {
                HasHeader = true,
                MaxColumnsPerRow = 3,
                ErrorAction = ReaderErrorAction.Throw,
                ErrorSink = sink
            };

            var ex = await Assert.ThrowsAsync<InvalidDataException>(async () =>
            {
                await foreach (var _ in Read.Csv<Row>(path, opts)) { }
            });

            Assert.Contains("Row has 4 columns", ex.Message);
            Assert.Equal(1, sink.Errors.Count); // first (and only) error captured optionally
            Assert.True(opts.Metrics.RawRecordsParsed >= 1); // at least the row that triggered
            // First valid data row (1,2,3) is emitted before the violating row triggers the exception.
            Assert.Equal(1, opts.Metrics.RecordsEmitted);
            Assert.True(opts.Metrics.TerminatedEarly);
            Assert.Null(opts.Metrics.CompletedUtc);
        }
        finally { TryDelete(path); }
    }

    [Fact]
    public async Task MaxColumnsPerRow_Stop()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "A,B,C\n1,2,3\nX,Y,Z,W\n9,9,9\n");
            var sink = new CollectingErrorSink();
            var opts = new CsvReadOptions
            {
                HasHeader = true,
                MaxColumnsPerRow = 3,
                ErrorAction = ReaderErrorAction.Stop,
                ErrorSink = sink
            };

            var rows = new List<Row>();
            await foreach (var r in Read.Csv<Row>(path, opts))
                rows.Add(r);

            // Should have emitted only the first valid row (stop after violation)
            Assert.Equal(1, rows.Count);
            Assert.Equal(1, sink.Errors.Count);
            Assert.True(opts.Metrics.TerminatedEarly);
            Assert.Null(opts.Metrics.CompletedUtc);
        }
        finally { TryDelete(path); }
    }

    [Fact(DisplayName = "Row exactly at MaxColumnsPerRow passes")]
    public async Task MaxColumnsPerRow_ExactPass()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "A,B,C\n1,2,3\n4,5,6\n");
            var sink = new CollectingErrorSink();
            var opts = new CsvReadOptions
            {
                HasHeader = true,
                MaxColumnsPerRow = 3,
                ErrorAction = ReaderErrorAction.Skip,
                ErrorSink = sink
            };
            int count = 0;
            await foreach (var _ in Read.Csv<Row>(path, opts))
                count++;

            Assert.Equal(2, count);
            Assert.Equal(0, sink.Errors.Count);
        }
        finally { TryDelete(path); }
    }

    [Fact(DisplayName = "MaxRawRecordLength enforcement (Skip)")]
    public async Task MaxRawRecordLength_Skip()
    {
        string path = Path.GetTempFileName();
        try
        {
            // Make a long third row (quotes count, separators count)
            var longPayload = new string('A', 60);
            File.WriteAllText(path, $"A,B,C\n1,2,3\n\"{longPayload}\",X,Y\n9,9,9\n");

            var sink = new CollectingErrorSink();
            var opts = new CsvReadOptions
            {
                HasHeader = true,
                MaxRawRecordLength = 40, // smaller than long row
                ErrorAction = ReaderErrorAction.Skip,
                ErrorSink = sink
            };

            var rows = new List<Row>();
            await foreach (var r in Read.Csv<Row>(path, opts))
                rows.Add(r);

            // Expect rows 1 and 3 only
            Assert.Equal(2, rows.Count);
            Assert.Single(sink.Errors);
            var e = sink.Errors[0];
            Assert.Equal("CsvLimitExceeded", e.ErrorType);
            Assert.Contains("Raw record length", e.Message);
            Assert.Equal(ReaderErrorAction.Skip.ToString(), e.ActionChosen.ToString());
        }
        finally { TryDelete(path); }
    }

    [Fact(DisplayName = "MaxRawRecordLength exact boundary passes")]
    public async Task MaxRawRecordLength_ExactBoundary()
    {
        string path = Path.GetTempFileName();
        try
        {
            // Compute exact raw length: "A,B,C\n" header then row below.
            // We'll set limit to length of second row.
            string row = "1,22,333"; // length 9 (no newline)
            int limit = row.Length;
            File.WriteAllText(path, "A,B,C\n" + row);

            var sink = new CollectingErrorSink();
            var opts = new CsvReadOptions
            {
                HasHeader = true,
                MaxRawRecordLength = limit,
                ErrorAction = ReaderErrorAction.Skip,
                ErrorSink = sink
            };

            int count = 0;
            await foreach (var _ in Read.Csv<Row>(path, opts))
                count++;

            Assert.Equal(1, count);
            Assert.Empty(sink.Errors);
        }
        finally { TryDelete(path); }
    }

    [Fact(DisplayName = "Columns limit fires before schema width mismatch (no SchemaError)")]
    public async Task GuardRailPrecedesSchemaError()
    {
        string path = Path.GetTempFileName();
        try
        {
            // Schema has 5 fields, row will have 10. MaxColumnsPerRow set to 6 so limit triggers before potential SchemaError (assuming AllowExtraFields=false).
            File.WriteAllText(path, "A,B,C,D,E\n1,2,3,4,5,6,7,8,9,10\n2,3,4,5,6\n");

            var sink = new CollectingErrorSink();
            var opts = new CsvReadOptions
            {
                HasHeader = true,
                AllowExtraFields = false,
                MaxColumnsPerRow = 6,
                ErrorAction = ReaderErrorAction.Skip,
                ErrorSink = sink
            };

            var rows = new List<Row>();
            await foreach (var r in Read.Csv<Row>(path, opts))
                rows.Add(r);

            Assert.Single(sink.Errors);
            Assert.Equal("CsvLimitExceeded", sink.Errors[0].ErrorType);
            Assert.DoesNotContain(sink.Errors, e => e.ErrorType == "SchemaError");
        }
        finally { TryDelete(path); }
    }

    [Fact(DisplayName = "Only one CsvLimitExceeded per record when both limits breached")]
    public async Task SingleErrorWhenBothLimitsExceeded()
    {
        string path = Path.GetTempFileName();
        try
        {
            // Create a very wide and long row
            var wide = string.Join(",", Enumerable.Range(0, 20).Select(i => new string('X', 10)));
            File.WriteAllText(path, "A,B,C\n" + wide + "\n");

            var sink = new CollectingErrorSink();
            var opts = new CsvReadOptions
            {
                HasHeader = true,
                MaxColumnsPerRow = 5,
                MaxRawRecordLength = 30, // also smaller than the wide row raw length
                ErrorAction = ReaderErrorAction.Skip,
                ErrorSink = sink
            };

            await foreach (var _ in Read.Csv<Row>(path, opts)) { }

            Assert.Single(sink.Errors); // only one error
            Assert.Equal("CsvLimitExceeded", sink.Errors[0].ErrorType);
        }
        finally { TryDelete(path); }
    }

    [Fact(DisplayName = "RawRecordObserver not invoked for guard-rail skipped rows")]
    public async Task RawRecordObserver_SkippedRowsNotObserved()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path,
                "A,B,C\n" +
                "1,2,3\n" +      // record #1 (ok)
                "x,y,z,w\n" +    // record #2 (skip: 4 cols)
                "4,5,6\n");      // record #3 (ok)

            var observed = new List<long>();
            var sink = new CollectingErrorSink();
            var opts = new CsvReadOptions
            {
                HasHeader = true,
                MaxColumnsPerRow = 3,
                ErrorAction = ReaderErrorAction.Skip,
                ErrorSink = sink,
                RawRecordObserver = (n, raw) => observed.Add(n)
            };

            await foreach (var _ in Read.Csv<Row>(path, opts)) { }

            // RawRecordsParsed: 3 (including skipped)
            Assert.Equal(3, opts.Metrics.RawRecordsParsed);
            // Observed only 1 and 3 (skipped record #2 absent)
            Assert.Equal(new[] { 1L, 3L }, observed.ToArray());
            Assert.Single(sink.Errors);
        }
        finally { TryDelete(path); }
    }

    [Fact(DisplayName = "Excerpt truncation at configured 128 chars")]
    public async Task Excerpt_Truncated_At_128()
    {
        string path = Path.GetTempFileName();
        try
        {
            var longField = new string('A', 300);
            File.WriteAllText(path, "A,B\n" + longField + ",X,Y,Z\n"); // wide row -> columns > MaxColumnsPerRow triggers excerpt capture

            var sink = new CollectingErrorSink();
            var opts = new CsvReadOptions
            {
                HasHeader = true,
                MaxColumnsPerRow = 2,
                ErrorAction = ReaderErrorAction.Skip,
                ErrorSink = sink
            };

            await foreach (var _ in Read.Csv<dynamic>(path, opts)) { }

            var err = Assert.Single(sink.Errors);
            Assert.Equal("CsvLimitExceeded", err.ErrorType);
            Assert.True(err.RawExcerpt?.Length <= 128, $"Excerpt length={err.RawExcerpt?.Length}");
            Assert.True(err.RawExcerpt!.Length == 128 || err.RawExcerpt!.Length < 128);
        }
        finally { TryDelete(path); }
    }

    private static void TryDelete(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }
}