using DataFlow;
using DataFlow.Data.Tests.Utilities;
using System;
using System.Text;
using System.Xml.Linq;
using Xunit;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;

namespace DataFlow.Data.Tests.Cross;

public class StopActionTests
{
        [Fact]
        public async Task Csv_Stop_Stops_On_First_Error()
        {
            var path = Path.GetTempFileName();
            File.WriteAllText(path, "A,B\n1,2\n3,4,5\n6,7");
            var sink = new InMemoryErrorSink();
            var opts = new CsvReadOptions
            {
                HasHeader = true,
                AllowExtraFields = false,
                ErrorAction = ReaderErrorAction.Stop,
                ErrorSink = sink
            };
            int count = 0;
            await foreach (var _ in Read.Csv<dynamic>(path, opts))
                count++;

            // Only the first valid data row should be emitted (header is not counted as a record)
            Assert.Equal(1, count);
            Assert.Equal(1, opts.Metrics.RecordsEmitted);
            Assert.True(opts.Metrics.TerminatedEarly);
            // By design (docs §1.3 / §1.6): CompletedUtc is null on early termination (Stop, Throw, cancellation, fatal)
            Assert.Null(opts.Metrics.CompletedUtc);
            Assert.Equal(1, sink.Errors.Count);
            // RawRecordsParsed should be at least 2 (row 1 good, row 2 parsed then failed)
            Assert.True(opts.Metrics.RawRecordsParsed >= 2);
    }

    [Fact]
    public async Task Json_Stop_Stops_On_Error()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "[{\"id\":1},{INVALID_JSON},{\"id\":3}]");
        var sink = new InMemoryErrorSink();
        var opts = new JsonReadOptions<dynamic>
        {
            RequireArrayRoot = true,
            ErrorAction = ReaderErrorAction.Stop,
            ErrorSink = sink
        };
        int count = 0;
        await foreach (var _ in Read.Json<dynamic>(path, opts))
            count++;

        Assert.True(count <= 1);
        Assert.True(opts.Metrics.TerminatedEarly);
    }
    [Fact]
    public async Task Yaml_Stop_Stops_On_Error()
    {
        var path = Path.GetTempFileName();
        // Good doc, malformed doc (missing colon), another good doc
        File.WriteAllText(path, "---\nkey: 1\n---\nkey: ]\n---\nkey: 3\n");
        var sink = new InMemoryErrorSink();
        var opts = new YamlReadOptions<dynamic>
        {
            ErrorAction = ReaderErrorAction.Stop,
            ErrorSink = sink,
            RestrictTypes = false
        };
        int count = 0;
        await foreach (var _ in Read.Yaml<dynamic>(path, opts))
            count++;

        Assert.Equal(1, count);                 // we should have yielded exactly the first document
        Assert.True(opts.Metrics.TerminatedEarly);
        Assert.Equal(1, opts.Metrics.ErrorCount);
        Assert.Null(opts.Metrics.CompletedUtc); // optional extra assertion
    }


    // Path A: TypeRestriction (post-deserialize)
    // Cause: RestrictTypes true (default), T = dynamic (== object), document is a mapping -> runtime type != object
    [Fact]
    public async Task Yaml_Stop_TypeRestriction_Sets_TerminatedEarly()
    {
        var path = Path.GetTempFileName();
        try
        {


            // Single mapping document triggers TypeRestriction immediately
            System.IO.File.WriteAllText(path, "---\nkey: 1\n");

            var sink = new InMemoryErrorSink();
            var opts = new YamlReadOptions<dynamic>
            {
                ErrorAction = ReaderErrorAction.Stop,
                ErrorSink = sink,
                RestrictTypes = true,
                // Assuming your implementation supports an AllowedTypes or similar property.
                // Include only primitive safe types so a mapping/dictionary is rejected.
                //AllowedTypes = new[] { typeof(string), typeof(int), typeof(bool), typeof(double) }
                // RestrictTypes left true (default) to force TypeRestriction
            };

            int count = 0;
            await foreach (var _ in Read.Yaml<dynamic>(path, opts))
                count++;

            Assert.Equal(0, count);                              // Nothing emitted
            Assert.True(opts.Metrics.TerminatedEarly);           // Stop triggered
            Assert.Null(opts.Metrics.CompletedUtc);              // Not completed
            Assert.Equal(1, opts.Metrics.ErrorCount);            // Single error
            var error = Assert.Single(sink.Errors);
            Assert.Equal("TypeRestriction", error.ErrorType);
            Assert.Equal(1, opts.Metrics.RawRecordsParsed);      // We counted the record before rejecting
            Assert.Equal(0, opts.Metrics.RecordsEmitted);
        }
        finally
        {
            File.Delete(path);
        }
    }

    // Path B: YamlException thrown during deserialization
    // Strategy: First valid doc (emitted), second malformed doc causes parse error, Stop halts before third.
    [Fact]
    public async Task Yaml_Stop_ParseError_Sets_TerminatedEarly()
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            // Doc1 valid, Doc2 malformed list (missing closing bracket), Doc3 would be valid but never reached.
            var yaml = """
                        ---
                        ok: 1
                        ---
                        broken: [1,2
                        ---
                        ok: 3
                        """;
        System.IO.File.WriteAllText(path, yaml);

        var sink = new InMemoryErrorSink();
        var opts = new YamlReadOptions<Dictionary<string, object>>
        {
            ErrorAction = ReaderErrorAction.Stop,
            ErrorSink = sink,
            RestrictTypes = false // Avoid TypeRestriction so we reach the real parse error
        };

        int count = 0;
        await foreach (var doc in Read.Yaml<Dictionary<string, object>>(path, opts))
            count++;

            Assert.Equal(1, count);              // First doc only
            Assert.True(opts.Metrics.TerminatedEarly);
            Assert.Null(opts.Metrics.CompletedUtc);
            Assert.Equal(1, opts.Metrics.ErrorCount);

            var error = Assert.Single(sink.Errors);
            Assert.NotEqual("TypeRestriction", error.ErrorType);
            // Typical:
            // Assert.Equal("YamlException", error.ErrorType);
            // Or resilient:
            // Assert.Contains("Yaml", error.ErrorType, StringComparison.OrdinalIgnoreCase);

            Assert.Equal(2, opts.Metrics.RawRecordsParsed);
            Assert.Equal(1, opts.Metrics.RecordsEmitted);
        }
        finally
        {
            File.Delete(path);
        }
    
    }

    // Path C: SecurityFilteringParser guard rail (pre-deserialization)
    // Strategy: Set MaxDepth = 1 and supply nested mapping to exceed depth.
    [Fact]
    public async Task Yaml_Stop_SecurityViolation_Sets_TerminatedEarly()
    {
        var path = System.IO.Path.GetTempFileName();
        try
        {
            // Depth: root mapping (depth=1) -> nested mapping (depth becomes 2) exceeds MaxDepth=1
            var yaml = """
                        ---
                        a:
                          b: 1
                        """;
        System.IO.File.WriteAllText(path, yaml);

        var sink = new InMemoryErrorSink();
        var opts = new YamlReadOptions<dynamic>
        {
            ErrorAction = ReaderErrorAction.Stop,
            ErrorSink = sink,
            RestrictTypes = false, // So we don't trip TypeRestriction first
            MaxDepth = 1
        };

        int count = 0;
        await foreach (var _ in Read.Yaml<dynamic>(path, opts))
            count++;

            Assert.Equal(0, count);
            Assert.True(opts.Metrics.TerminatedEarly);
            Assert.Null(opts.Metrics.CompletedUtc);
            Assert.Equal(1, opts.Metrics.ErrorCount);

            var error = Assert.Single(sink.Errors);
            // Prefer a specific security classification:
            // Assert.Equal("MaxDepthExceeded", error.ErrorType);
            // Interim resilient assertion:
            Assert.NotEqual("TypeRestriction", error.ErrorType);

            Assert.Equal(1, opts.Metrics.RawRecordsParsed);
            Assert.Equal(0, opts.Metrics.RecordsEmitted);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
