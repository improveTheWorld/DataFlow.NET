using DataFlow;
using DataFlow.Data.Tests.Utilities;
using System.Text.Json;
using Xunit;

namespace DataFlow.Data.Tests.Json;

public class JsonValidationTests
{
    private record Rec(int id, double amount, bool ok);

    private static string WriteTemp(string json)
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, json);
        return path;
    }

    [Fact]
    public async Task Validator_False_Skips_Element_Increments_Error()
    {
        var path = WriteTemp("[{\"id\":1,\"amount\":1.1,\"ok\":true},{\"id\":2,\"ok\":false},{\"id\":3,\"amount\":3.3,\"ok\":true}]");
        var sink = new InMemoryErrorSink();
        var opts = new JsonReadOptions<Rec>
        {
            RequireArrayRoot = true,
            ValidateElements = true,
            ElementValidator = el => el.TryGetProperty("amount", out _), // reject id=2
            ErrorAction = ReaderErrorAction.Skip,
            ErrorSink = sink
        };

        var list = new List<Rec>();
        await foreach (var r in Read.Json<Rec>(path, opts))
            list.Add(r);

        Assert.Equal(2, list.Count);
        Assert.Equal(3, opts.Metrics.RawRecordsParsed);
        Assert.Equal(2, opts.Metrics.RecordsEmitted);
        Assert.Equal(1, opts.Metrics.ErrorCount);
        Assert.Contains(sink.Errors, e => e.ErrorType == "JsonValidationFailed");
    }

    [Fact]
    public async Task Validator_Throws_Treated_As_Error_And_Skips()
    {
        var path = WriteTemp("[{\"id\":1,\"amount\":1.1,\"ok\":true},{\"id\":2,\"amount\":2.2,\"ok\":false}]");
        var sink = new InMemoryErrorSink();
        var opts = new JsonReadOptions<Rec>
        {
            RequireArrayRoot = true,
            ValidateElements = true,
            ElementValidator = el => throw new InvalidOperationException("boom"),
            ErrorAction = ReaderErrorAction.Skip,
            ErrorSink = sink
        };

        var list = new List<Rec>();
        await foreach (var _ in Read.Json<Rec>(path, opts)) { }

        Assert.Empty(list);
        Assert.Equal(2, opts.Metrics.RawRecordsParsed);
        Assert.Equal(0, opts.Metrics.RecordsEmitted);
        Assert.Equal(2, opts.Metrics.ErrorCount);
        Assert.Contains(sink.Errors, e => e.ErrorType == "JsonValidationError" && e.Message.Contains("boom"));
    }

    [Fact]
    public async Task Validation_Stop_Aborts_Stream()
    {
        var path = WriteTemp("[{\"id\":1,\"amount\":1.1,\"ok\":true},{\"id\":2},{\"id\":3,\"amount\":3.3,\"ok\":true}]");
        var sink = new InMemoryErrorSink();
        var opts = new JsonReadOptions<Rec>
        {
            RequireArrayRoot = true,
            ValidateElements = true,
            ElementValidator = el => el.TryGetProperty("amount", out _),
            ErrorAction = ReaderErrorAction.Stop,
            ErrorSink = sink
        };

        int count = 0;
        await foreach (var _ in Read.Json<Rec>(path, opts))
            count++;

        // Should have processed the first element only, then encountered invalid second and stopped.
        Assert.Equal(2, opts.Metrics.RawRecordsParsed);
        Assert.Equal(1, opts.Metrics.RecordsEmitted);
        Assert.Equal(1, opts.Metrics.ErrorCount);
        Assert.True(opts.Metrics.TerminatedEarly);
        Assert.Contains(sink.Errors, e => e.ErrorType == "JsonValidationFailed");
    }

    [Fact]
    public async Task Validation_Throw_Raises_Exception()
    {
        var path = WriteTemp("[{\"id\":1,\"amount\":1.1,\"ok\":true},{\"id\":2}]");
        var opts = new JsonReadOptions<Rec>
        {
            RequireArrayRoot = true,
            ValidateElements = true,
            ElementValidator = el => el.HasProperty("amount"), // id=2 object missing 'amount'
            ErrorAction = ReaderErrorAction.Throw
        };

        // Collect using enumerator to assert exception
        var ex = await Assert.ThrowsAsync<InvalidDataException>(async () =>
        {
            await foreach (var _ in Read.Json<Rec>(path, opts)) { }
        });

        Assert.Contains("JsonValidationFailed", ex.Message);
        Assert.False(opts.Metrics.CompletedUtc.HasValue); // Did not complete gracefully
    }

    [Fact(DisplayName = "[BugGuard] SingleObject Validation Metrics == 1")]
    public async Task Single_Object_Validation_Metrics()
    {
        var path = WriteTemp("{\"id\":10,\"amount\":5.5,\"ok\":true}");
        var opts = new JsonReadOptions<Rec>
        {
            RequireArrayRoot = true,
            AllowSingleObject = true,
            ValidateElements = true,
            ElementValidator = _ => true
        };

        var list = new List<Rec>();
        await foreach (var r in Read.Json<Rec>(path, opts))
            list.Add(r);

        Assert.Single(list);
        Assert.Equal(1, opts.Metrics.RawRecordsParsed);
        Assert.Equal(1, opts.Metrics.RecordsEmitted);
        Assert.Equal(0, opts.Metrics.ErrorCount);
    }

    [Fact]
    public async Task Large_Element_Spans_Buffers_Validated()
    {
        // Create a large object ~ >64KB to exceed typical buffer (assuming 32–64KB internal)
        var sb = new System.Text.StringBuilder();
        sb.Append("{\"id\":1,\"amount\":1.0,\"ok\":true,\"blob\":\"");
        sb.Append(new string('A', 100_000));
        sb.Append("\"}");
        var path = WriteTemp(sb.ToString());

        var opts = new JsonReadOptions<Rec>
        {
            RequireArrayRoot = true,
            AllowSingleObject = true,
            ValidateElements = true,
            ElementValidator = e => e.TryGetProperty("blob", out _)
        };

        int count = 0;
        await foreach (var _ in Read.Json<Rec>(path, opts))
            count++;
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Primitive_Array_With_Validation_Downcasts()
    {
        var path = WriteTemp("[1,2,3]");
        // Use int target, no validator (fast path). Then force validation to ensure branch coverage.
        var opts = new JsonReadOptions<int>
        {
            RequireArrayRoot = true,
            ValidateElements = true,
            ElementValidator = e => e.ValueKind == JsonValueKind.Number
        };

        var list = new List<int>();
        await foreach (var v in Read.Json<int>(path, opts))
            list.Add(v);
        Assert.Equal(new[] { 1, 2, 3 }, list);
    }
}

internal static class JsonElementExtensions
{
    public static bool HasProperty(this JsonElement el, string name) => el.ValueKind == JsonValueKind.Object && el.TryGetProperty(name, out _);
}
