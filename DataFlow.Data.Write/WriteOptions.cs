using System.Text;

namespace DataFlow;

/// <summary>
/// Base options for all write operations.
/// </summary>
public record WriteOptions
{
    /// <summary>
    /// Character encoding for the output. Default: UTF-8.
    /// </summary>
    public Encoding Encoding { get; init; } = Encoding.UTF8;

    /// <summary>
    /// If true, appends to existing file. If false, creates/overwrites.
    /// </summary>
    public bool Append { get; init; } = false;

    /// <summary>
    /// Cancellation token for the write operation.
    /// </summary>
    public CancellationToken CancellationToken { get; init; } = default;

    /// <summary>
    /// Metrics collected during the write operation.
    /// </summary>
    public WriterMetrics Metrics { get; } = new();
}

/// <summary>
/// Metrics collected during a write operation.
/// </summary>
public class WriterMetrics
{
    /// <summary>Number of records written to the output.</summary>
    public long RecordsWritten { get; internal set; }

    /// <summary>Timestamp when writing started (UTC).</summary>
    public DateTime? StartedUtc { get; internal set; }

    /// <summary>Timestamp when writing completed successfully (UTC).</summary>
    public DateTime? CompletedUtc { get; internal set; }

    internal void Start() => StartedUtc = DateTime.UtcNow;
    internal void Complete() => CompletedUtc = DateTime.UtcNow;
    internal void IncrementRecords() => RecordsWritten++;
}

/// <summary>
/// Options for CSV write operations.
/// </summary>
public record CsvWriteOptions : WriteOptions
{
    /// <summary>
    /// Field separator character. Default: ","
    /// </summary>
    public string Separator { get; init; } = ",";

    /// <summary>
    /// Whether to write a header row. Default: true.
    /// </summary>
    public bool WriteHeader { get; init; } = true;

    /// <summary>
    /// Line ending for records. Default: null (uses Environment.NewLine).
    /// </summary>
    public string? NewLine { get; init; } = null;
}

/// <summary>
/// Options for JSON write operations.
/// </summary>
public record JsonWriteOptions : WriteOptions
{
    /// <summary>
    /// Whether to indent the JSON output. Default: true.
    /// Ignored when JsonLinesFormat is true.
    /// </summary>
    public bool Indented { get; init; } = true;

    /// <summary>
    /// Custom JsonSerializerOptions. If null, default options are used.
    /// </summary>
    public System.Text.Json.JsonSerializerOptions? SerializerOptions { get; init; }

    /// <summary>
    /// If true, writes JSON Lines format (one JSON object per line, no array wrapper).
    /// Compatible with tools like Elasticsearch, BigQuery, and streaming processors.
    /// Default: false (standard JSON array).
    /// </summary>
    public bool JsonLinesFormat { get; init; } = false;
}

/// <summary>
/// Options for YAML write operations.
/// </summary>
public record YamlWriteOptions : WriteOptions
{
    /// <summary>
    /// Whether to write "[]" when the sequence is empty. Default: true.
    /// </summary>
    public bool WriteEmptySequence { get; init; } = true;

    /// <summary>
    /// If set, items are written in batches with YAML document separators (---).
    /// If null, single-document mode is used.
    /// </summary>
    public int? BatchSize { get; init; } = null;
}
