#nullable enable
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace DataFlow;


public sealed record CsvReadOptions : ReadOptions
{
    public string Separator { get; init; } = ",";
    public string[]? Schema { get; set; }
    public bool HasHeader { get; init; } = true;

    // Default false for strict RFC (spaces outside quotes are significant)
    public bool TrimWhitespace { get; init; } = false;

    public bool AllowMissingTrailingFields { get; init; } = true;
    public bool AllowExtraFields { get; init; } = false;

    // Quoting & line ending controls
    public CsvQuoteMode QuoteMode { get; init; } = CsvQuoteMode.RfcStrict;
    public bool ErrorOnTrailingGarbageAfterClosingQuote { get; init; } = true;
    public bool PreserveLineEndings { get; init; } = true;              // Preserve CRLF vs normalizing
    public bool NormalizeNewlinesInFields { get; init; } = false;       // If true (and PreserveLineEndings=false) CRLF -> \n inside quoted fields
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
    internal bool[]? InferredTypeFinalized; // Column fallback tracking (true => no further type attempts)

    // Guard rails (0 => disabled)
    public int MaxColumnsPerRow { get; init; } = 0;
    public int MaxRawRecordLength { get; init; } = 0;

    /// <summary>
    /// Culture used for numeric and DateTime parsing. Defaults to <see cref="CultureInfo.InvariantCulture"/>.
    /// Set to e.g. <c>new CultureInfo("fr-FR")</c> for European CSV files that use comma as decimal separator.
    /// </summary>
    public IFormatProvider FormatProvider { get; init; } = CultureInfo.InvariantCulture;


    /// <summary>
    /// Converts a raw CSV field string to a typed object based on column schema and inference settings.
    /// </summary>
    /// <param name="raw">The raw string value from the CSV field.</param>
    /// <param name="columnIndex">The zero-based column index.</param>
    /// <returns>
    /// A typed object if conversion succeeds; otherwise the raw string.
    /// Empty strings for value types return the raw string (materialized as default values).
    /// </returns>
    /// <remarks>
    /// <b>Lenient by default:</b> Failed conversions demote the column to string type.
    /// Preserves leading zeros and large integers based on <see cref="PreserveNumericStringsWithLeadingZeros"/> 
    /// and <see cref="PreserveLargeIntegerStrings"/> settings.
    /// </remarks>
    internal object? ConvertFieldValue(string raw, int columnIndex)
    {
        // Custom delegate
        if (FieldTypeInference == FieldTypeInferenceMode.Custom && FieldValueConverter != null)
            return FieldValueConverter(raw);

        if (FieldTypeInference == FieldTypeInferenceMode.None)
            return raw;

        // If we have inferred types, attempt strict casting unless column permanently demoted/finalized.
        if (InferredTypes != null &&
            columnIndex < InferredTypes.Length &&
            InferredTypes[columnIndex] != null)
        {
            var t = InferredTypes[columnIndex];

            // If column finalized as string, return raw directly.
            if (t == typeof(string) &&
                InferredTypeFinalized != null &&
                columnIndex < InferredTypeFinalized.Length &&
                InferredTypeFinalized[columnIndex])
            {
                return raw;
            }

            if (t == typeof(string))
                return raw;

            if (!raw.TryParseAs(t, out var converted, new TextParsingOptions { FormatProvider = FormatProvider }))
            {
                // Demote to string and finalize (no further parse attempts for this column).
                InferredTypes[columnIndex] = typeof(string);
                if (InferredTypeFinalized != null && columnIndex < InferredTypeFinalized.Length)
                    InferredTypeFinalized[columnIndex] = true;
                return raw;
            }
            return converted;
        }

        // Primitive inference (no schema types or not inferred yet)
        return raw.Parse(
             TextParsingOptions.Default with
             {
                 PreserveLeadingZeroNumeric = PreserveNumericStringsWithLeadingZeros,
                 PreserveLargeIntegerStrings = PreserveLargeIntegerStrings,
                 TrimWhitespace = TrimWhitespace,
                 EnableDouble = true,
                 FormatProvider = FormatProvider
             });
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
    public long RawRecordsParsed;
    public long RecordsEmitted;
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

    /// <summary>
    /// Convenience shorthand: when set, automatically configures ErrorAction = Skip
    /// and ErrorSink to delegate errors to the provided callback.
    /// Equivalent to using the inline <c>onError</c> parameter on Read methods.
    /// </summary>
    public Action<Exception>? OnError
    {
        init
        {
            if (value != null)
            {
                ErrorAction = ReaderErrorAction.Skip;
                ErrorSink = new Read.DelegatingErrorSink(value, "(options)");
            }
        }
    }
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
        if (Metrics.RawRecordsParsed - _lastProgressRecordMark >= ProgressRecordInterval && ProgressRecordInterval > 0)
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
            Metrics.RecordsEmitted,
            Metrics.ErrorCount,
            percent,
            elapsed));
        _lastProgressWall = DateTime.UtcNow;
        _lastProgressRecordMark = Metrics.RawRecordsParsed;
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
            // Mark early termination also for Throw (documentation: fatal errors set TerminatedEarly)
            Metrics.TerminatedEarly = true;
            Metrics.TerminationErrorMessage = message;
            // Include errorType and excerpt in the InvalidDataException message.
            throw new InvalidDataException($"{errorType}: {message}{(string.IsNullOrEmpty(excerpt) ? "" : " | excerpt: " + excerpt)}");
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

    // Guard rails (0 => disabled)
    public long MaxElementBytes { get; init; } = 0;
    public long MaxElements { get; init; } = 0;
    public int MaxStringLength { get; init; } = 0;

    internal bool GuardRailsEnabled =>
        MaxElementBytes > 0 || MaxElements > 0 || MaxStringLength > 0;
}

public sealed record YamlReadOptions<T> : ReadOptions
{
    public bool RestrictTypes { get; init; } = true;
    public IReadOnlySet<Type>? AllowedTypes { get; init; }
    public bool DisallowAliases { get; init; } = true;
    public bool DisallowCustomTags { get; init; } = true;
    public int MaxDepth { get; init; } = 64;

    // Size and count limits (0 => disabled)
    public int MaxTotalDocuments { get; init; } = 0;
    public int MaxNodeScalarLength { get; init; } = 0;

}
// ============ Text Options ============
public sealed record TextReadOptions : ReadOptions
{
    // Encoding for StreamReader. If null, use UTF8 (with BOM detection if DetectEncodingFromByteOrderMarks is true).
    public Encoding? Encoding { get; set; } = null;

    // If true, StreamReader will detect UTF BOMs (UTF-8/UTF-16/UTF-32). Defaults to true for parity with typical readers.
    public bool DetectEncodingFromByteOrderMarks { get; set; } = true;

    // Buffer size for the StreamReader. Defaults to 64 KiB.
    public int BufferSize { get; set; } = 64 * 1024;

    // Optional: include line numbers in progress events by mapping RecordsRead to logical line count.
    // This is implicit because each emitted line increments RecordsEmitted which feeds RecordsRead in ReaderProgress.
}
