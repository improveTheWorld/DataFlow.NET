using System.Text;
using System.Text.Json;

namespace DataFlow.Data;


public sealed record CsvReadOptions : ReadOptions
{
    public char Separator { get; init; } = ',';
    public string[]? Schema { get; internal set; }
    public bool HasHeader { get; init; } = true;

    // BREAKING CHANGE: default false for strict RFC (spaces outside quotes are significant)
    public bool TrimWhitespace { get; init; } = false;

    public bool AllowMissingTrailingFields { get; init; } = true;
    public bool AllowExtraFields { get; init; } = false;

    // Quoting & line ending controls
    public CsvQuoteMode QuoteMode { get; init; } = CsvQuoteMode.RfcStrict;
    public bool ErrorOnTrailingGarbageAfterClosingQuote { get; init; } = true;
    public bool PreserveLineEndings { get; init; } = true;              // Preserve CRLF vs normalizing
    public bool NormalizeNewlinesInFields { get; init; } = false;       // If true (and PreserveLineEndings=false) CRLF -> \n inside quoted fields
    public bool CaptureRawRecord { get; init; } = false;                // Capture original raw record text (post-unescaping)
    public Action<long, string>? RawRecordObserver { get; init; }       // Callback(recordNumber, rawRecord)

    // Schema / type inference
    public bool InferSchema { get; init; } = false;
    public int SchemaInferenceSampleRows { get; init; } = 100;
    public SchemaInferenceMode SchemaInferenceMode { get; init; } = SchemaInferenceMode.ColumnNamesOnly;
    public Func<string, string, int, string>? GenerateColumnName { get; init; } // (rawHeaderCell,filePath,index,defaultName)=>name

    // Field type inference behavior
    public FieldTypeInferenceMode FieldTypeInference { get; init; } = FieldTypeInferenceMode.Primitive;
    public Func<string, object?>? FieldValueConverter { get; init; }    // Used when FieldTypeInference = Custom

    // Preservation flags to avoid data loss (leading zeros, very large numbers)
    public bool PreserveNumericStringsWithLeadingZeros { get; init; } = true;
    public bool PreserveLargeIntegerStrings { get; init; } = true;

    // Inferred types & internal state (populated when inference performed)
    public Type[]? InferredTypes { get; internal set; }
    internal bool[]? InferredTypeFinalized; // Column fallback tracking

    // Internal helper: Convert a raw field using configured inference rules + inferred schema types
    internal object? ConvertFieldValue(string raw, int columnIndex)
    {
        // Custom delegate
        if (FieldTypeInference == FieldTypeInferenceMode.Custom && FieldValueConverter != null)
            return FieldValueConverter(raw);

        if (FieldTypeInference == FieldTypeInferenceMode.None)
            return raw;

        // If we have inferred types and they are finalized, attempt strict casting
        if (InferredTypes != null && columnIndex < InferredTypes.Length && InferredTypes[columnIndex] != null)
        {
            var t = InferredTypes[columnIndex];
            if (t == typeof(string)) return raw;
            if (!TryConvertToType(raw, t, out var converted))
            {
                // Fallback to string if strict cast fails & mark column as string
                InferredTypes[columnIndex] = typeof(string);
                return raw;
            }
            return converted;
        }

        // Primitive inference path
        return StringMapper.StringMapper.GetObject(raw,
            preserveLeadingZeroNumeric: PreserveNumericStringsWithLeadingZeros,
            preserveLargeInteger: PreserveLargeIntegerStrings);
    }

    private static bool TryConvertToType(string s, Type t, out object? value)
    {
        value = null;
        if (t == typeof(string)) { value = s; return true; }
        if (t == typeof(bool) && bool.TryParse(s, out var b)) { value = b; return true; }
        if (t == typeof(int) && int.TryParse(s, out var i)) { value = i; return true; }
        if (t == typeof(long) && long.TryParse(s, out var l)) { value = l; return true; }
        if (t == typeof(decimal) && decimal.TryParse(s, out var dec)) { value = dec; return true; }
        if (t == typeof(double) && double.TryParse(s, out var dbl)) { value = dbl; return true; }
        if (t == typeof(DateTime) && DateTime.TryParse(s, out var dt)) { value = dt; return true; }
        if (t == typeof(Guid) && Guid.TryParse(s, out var g)) { value = g; return true; }
        return false;
    }
}

public enum ReaderErrorAction
{
    Throw,
    Skip,
    Stop
}

public record ReaderError(
    string Reader,
    string FilePath,
    long LineNumber,
    long RecordNumber,
    string ErrorType,
    string Message,
    string RawExcerpt,
    ReaderErrorAction ActionChosen,
    DateTimeOffset TimestampUtc);

public interface IReaderErrorSink : IDisposable
{
    void Report(ReaderError error);
}

public sealed class NullErrorSink : IReaderErrorSink
{
    public static readonly NullErrorSink Instance = new();
    private NullErrorSink() { }
    public void Report(ReaderError error) { /* no-op */ }
    public void Dispose() { }
}

public sealed class JsonLinesFileErrorSink : IReaderErrorSink
{
    private readonly StreamWriter _writer;
    private readonly bool _leaveOpen;
    private readonly object _lock = new();
    private readonly bool _includeStack;
    private readonly bool _fullStack;

    public JsonLinesFileErrorSink(
        string path,
        bool append = false,
        bool includeStackTrace = false,
        bool includeFullStackTrace = false,
        Encoding? encoding = null,
        bool leaveOpen = false)
    {
        _writer = new StreamWriter(File.Open(path, append ? FileMode.Append : FileMode.Create, FileAccess.Write, FileShare.Read),
            encoding ?? new UTF8Encoding(false));
        _leaveOpen = leaveOpen;
        _includeStack = includeStackTrace;
        _fullStack = includeFullStackTrace;
    }

    public void Report(ReaderError error)
    {
        var obj = new Dictionary<string, object?>
        {
            ["ts"] = error.TimestampUtc.ToString("O"),
            ["reader"] = error.Reader,
            ["file"] = error.FilePath,
            ["line"] = error.LineNumber >= 0 ? error.LineNumber : null,
            ["record"] = error.RecordNumber >= 0 ? error.RecordNumber : null,
            ["errorType"] = error.ErrorType,
            ["message"] = error.Message,
            ["excerpt"] = error.RawExcerpt,
            ["action"] = error.ActionChosen.ToString()
        };

        if (_includeStack)
        {
            // We assume a recent exception is stored ambiently if needed; left extensible.
            var st = Environment.StackTrace;
            obj["stack"] = _fullStack ? st : string.Join('\n', st.Split('\n').Take(10));
        }

        string json = JsonSerializer.Serialize(obj);
        lock (_lock)
        {
            _writer.WriteLine(json);
            _writer.Flush();
        }
    }

    public void Dispose()
    {
        if (!_leaveOpen)
        {
            _writer.Dispose();
        }
    }
}

public record ReaderMetrics
{
    public long LinesRead;
    public long RecordsRead;
    public long ErrorCount;
    public long LastLineNumber;
    public bool TerminatedEarly;
    public string? TerminationErrorMessage;
    public DateTimeOffset StartedUtc = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedUtc;
}

public record ReaderProgress(
    long LinesRead,
    long RecordsRead,
    long ErrorCount,
    double? Percentage,
    TimeSpan Elapsed);

public abstract record ReadOptions
{
    public ReaderErrorAction ErrorAction { get; init; } = ReaderErrorAction.Throw;
    public IReaderErrorSink ErrorSink { get; init; } = NullErrorSink.Instance;
    public IProgress<ReaderProgress>? Progress { get; init; }

    // Fire progress either when count interval reached OR time interval reached.
    public int ProgressRecordInterval { get; init; } = 5000;
    public TimeSpan ProgressTimeInterval { get; init; } = TimeSpan.FromSeconds(5);

    public bool IncludeStackTraceInErrors { get; init; } = false;
    public bool IncludeFullStackTrace { get; init; } = false;

    public ReaderMetrics Metrics { get; } = new();

    public CancellationToken CancellationToken { get; init; } = default;
    public string? FilePath { get; internal set; }

    internal DateTime _lastProgressWall = DateTime.UtcNow;
    internal long _lastProgressRecordMark = 0;

    internal bool ShouldEmitProgress()
    {
        if (Progress == null) return false;
        var now = DateTime.UtcNow;
        if (Metrics.RecordsRead - _lastProgressRecordMark >= ProgressRecordInterval && ProgressRecordInterval > 0)
            return true;
        if (now - _lastProgressWall >= ProgressTimeInterval)
            return true;
        return false;
    }

    internal void EmitProgress(long? totalBytes = null, long? bytesRead = null)
    {
        if (Progress == null) return;
        var elapsed = DateTimeOffset.UtcNow - Metrics.StartedUtc;
        double? percent = null;
        if (totalBytes.HasValue && totalBytes.Value > 0 && bytesRead.HasValue)
        {
            percent = Math.Min(100.0, (bytesRead.Value / (double)totalBytes.Value) * 100.0);
        }
        Progress.Report(new ReaderProgress(
            Metrics.LinesRead,
            Metrics.RecordsRead,
            Metrics.ErrorCount,
            percent,
            elapsed));
        _lastProgressWall = DateTime.UtcNow;
        _lastProgressRecordMark = Metrics.RecordsRead;
    }

    internal void Complete()
    {
        Metrics.CompletedUtc = DateTimeOffset.UtcNow;
        EmitProgress();
    }

    internal bool HandleError(
        string reader,
        long line,
        long record,
        string filePath,
        string errorType,
        string message,
        string excerpt)
    {
        Metrics.ErrorCount++;
        var err = new ReaderError(reader, filePath, line, record, errorType, message, excerpt, ErrorAction, DateTimeOffset.UtcNow);
        try
        {
            ErrorSink.Report(err);
        }
        catch
        {
            // swallow sink errors to avoid cascading failure
        }
        if (ErrorAction == ReaderErrorAction.Throw)
        {
            throw new InvalidDataException(message);
        }
        if (ErrorAction == ReaderErrorAction.Stop)
        {
            Metrics.TerminatedEarly = true;
            Metrics.TerminationErrorMessage = message;
            return false;
        }
        return true; // Skip and continue
    }
}



public sealed record JsonReadOptions<T> : ReadOptions
{
    public JsonSerializerOptions SerializerOptions { get; init; } = new()
    {
        PropertyNameCaseInsensitive = true
    };
    public bool RequireArrayRoot { get; init; } = true;
    public bool AllowSingleObject { get; init; } = true;
    public bool ValidateElements { get; init; } = false;
    public Func<JsonElement, bool>? ElementValidator { get; init; }
    public int MaxDepth { get; init; } = 0; // 0 means default
}

public sealed record YamlReadOptions<T> : ReadOptions
{
    public bool RestrictTypes { get; init; } = true;
    public IReadOnlySet<Type>? AllowedTypes { get; init; }
    public bool DisallowAliases { get; init; } = true;
    public bool DisallowCustomTags { get; init; } = true;
    public int MaxDepth { get; init; } = 64;
}