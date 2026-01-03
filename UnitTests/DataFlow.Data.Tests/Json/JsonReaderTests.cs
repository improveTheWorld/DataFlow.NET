using DataFlow;
using DataFlow.Data.Tests.Utilities;
using System.Text;
using Xunit;

namespace DataFlow.Data.Tests.Json;

public class JsonReaderTests
{
    // Simple POCO/record matching the JSON we generate
    private record BigRec(int id, string blob);

    private static string WriteLargeArrayFile(int largeStringLength, bool includeSecond)
    {
        var sb = new StringBuilder();
        sb.Append("[{\"id\":1,\"blob\":\"");
        sb.Append(new string('A', largeStringLength));
        sb.Append("\"}");
        if (includeSecond)
        {
            sb.Append(",{\"id\":2,\"blob\":\"X\"}");
        }
        sb.Append("]");
        var path = Path.GetTempFileName();
        File.WriteAllText(path, sb.ToString());
        return path;
    }

    // This test exposes the double-count bug: before the fix RawRecordsParsed == 2, RecordsEmitted == 1
    [Fact]
    public async Task Large_Element_CrossBuffer_No_DoubleCount_FastPath()
    {
        // Length > 64K so the element necessarily spans the 64 * 1024 buffer used in JsonStreamCore
        string path = WriteLargeArrayFile(100_000, includeSecond: false);

        var opts = new JsonReadOptions<BigRec>
        {
            RequireArrayRoot = true,
            // No validation / guards so fastPath stays true
        };

        var list = new List<BigRec>();
        await foreach (var r in Read.Json<BigRec>(path, opts))
            list.Add(r);

        Assert.Single(list); // We truly only have one JSON element
        Assert.Equal(1, opts.Metrics.RecordsEmitted);

        // The bug (pre-fix): RawRecordsParsed becomes 2 here.
        Assert.Equal(1, opts.Metrics.RawRecordsParsed); // Fails before fix, passes after.
    }

    // Variant with a second small element to ensure the total count also aligns
    [Fact]
    public async Task Large_Element_Then_Small_No_Extra_Count()
    {
        string path = WriteLargeArrayFile(100_000, includeSecond: true);

        var opts = new JsonReadOptions<BigRec>
        {
            RequireArrayRoot = true
        };

        var list = new List<BigRec>();
        await foreach (var r in Read.Json<BigRec>(path, opts))
            list.Add(r);

        Assert.Equal(2, list.Count);
        Assert.Equal(2, opts.Metrics.RecordsEmitted);

        // Pre-fix bug: RawRecordsParsed == 3 (double count of first element).
        Assert.Equal(2, opts.Metrics.RawRecordsParsed); // Fails before fix, passes after.
    }
    record JRec(int id, double amount, bool ok);

    [Fact]
    public async Task Reads_Array_FastPath()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "[{\"id\":1,\"amount\":5.2,\"ok\":true},{\"id\":2,\"amount\":7.1,\"ok\":false}]");
        var opts = new JsonReadOptions<JRec> { RequireArrayRoot = true };
        var list = new List<JRec>();
        await foreach (var item in Read.Json<JRec>(path, opts))
            list.Add(item);
        Assert.Equal(2, list.Count);
        Assert.Equal(2, opts.Metrics.RawRecordsParsed);
        Assert.Equal(2, opts.Metrics.RecordsEmitted);
        Assert.Equal(0, opts.Metrics.ErrorCount);
    }

    [Fact]
    public async Task Single_Object_FastPath()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "{\"id\":1,\"amount\":7.7,\"ok\":true}");
        var opts = new JsonReadOptions<JRec> { RequireArrayRoot = true, AllowSingleObject = true };
        var list = new List<JRec>();
        await foreach (var item in Read.Json<JRec>(path, opts))
            list.Add(item);
        Assert.Single(list);
        Assert.Equal(1, opts.Metrics.RawRecordsParsed);
        Assert.Equal(1, opts.Metrics.RecordsEmitted);
        Assert.Equal(0, opts.Metrics.ErrorCount);
    }

    [Fact]
    public async Task Validator_Skips_Invalid_Element()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "[{\"id\":1,\"amount\":1.0,\"ok\":true},{\"id\":2},{\"id\":3,\"amount\":3.3,\"ok\":true}]");
        var opts = new JsonReadOptions<JRec>
        {
            RequireArrayRoot = true,
            ValidateElements = true,
            ElementValidator = e => e.TryGetProperty("amount", out var _),
            ErrorAction = ReaderErrorAction.Skip,
            ErrorSink = new InMemoryErrorSink()
        };
        var list = new List<JRec>();
        await foreach (var item in Read.Json<JRec>(path, opts))
            list.Add(item);
        Assert.Equal(2, list.Count);
        Assert.Equal(2, opts.Metrics.RecordsEmitted); // Only successful increments
        Assert.Equal(3, opts.Metrics.RawRecordsParsed);
        Assert.Equal(1, opts.Metrics.ErrorCount);
    }

    [Fact(DisplayName = "[KnownIssue] Validation single-object metrics discrepancy")]
    public async Task Single_Object_Validation_Path_Metrics()
    {
        var path = Path.GetTempFileName();
        File.WriteAllText(path, "{\"id\":1,\"amount\":2.0,\"ok\":true}");
        var opts = new JsonReadOptions<JRec>
        {
            RequireArrayRoot = true,
            AllowSingleObject = true,
            ValidateElements = true,
            ElementValidator = e => true
        };
        var list = new List<JRec>();
        await foreach (var item in Read.Json<JRec>(path, opts))
            list.Add(item);

        Assert.Single(list);
        Assert.Equal(1, opts.Metrics.RawRecordsParsed);
        Assert.Equal(1, opts.Metrics.RecordsEmitted);
        Assert.Equal(0, opts.Metrics.ErrorCount);
    }


}
