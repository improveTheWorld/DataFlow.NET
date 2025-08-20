# DataFlow.Data Reading Infrastructure

This document provides a deep-dive into the reading infrastructure of the `DataFlow.Data` layer, covering configuration, error handling, and format-specific options.

## 0. Fast Usage Overview

### Asynchrony Convention (IMPORTANT)

Default method names are **ASYNCHRONOUS**. Synchronous variants use the `Sync` suffix.

- **Async:** `Read.Csv<T>()` returns `IAsyncEnumerable<T>`
- **Sync:** `Read.CsvSync<T>()` returns `IEnumerable<T>`

### 0.1 Read Raw Text Lines

```csharp
// Async
IAsyncEnumerable<string> lines = Read.Text("file.txt");

// Sync
IEnumerable<string> linesSync = Read.TextSync("file.txt");
```

### 0.2 Simple CSV

*Auto-defaults: `HasHeader = true` if no schema is passed. Errors throw by default.*

```csharp
// Async
var rows = Read.Csv<MyRow>("data.csv", ",");

// Sync
var rowsSync = Read.CsvSync<MyRow>("data.csv", ",");

// Provide a schema for a header-less file
var rows2 = Read.Csv<MyRow>("data_no_header.csv", ",", null, "Id", "Name", "Price");

// Handle errors by skipping instead of throwing
// (onError triggers Skip behavior + DelegatingErrorSink)
var rows3 = Read.Csv<MyRow>("maybe_dirty.csv", ",", (raw, ex) => Console.WriteLine(raw + " :: " + ex.Message));
```

### 0.3 Full CSV with Options

```csharp
var options = new CsvReadOptions {
    HasHeader = true,
    Separator = ';',
    AllowMissingTrailingFields = false,
    AllowExtraFields = false,
    TrimWhitespace = true,
    ErrorAction = ReaderErrorAction.Skip,
    ErrorSink = new JsonLinesFileErrorSink("csv_errors.ndjson"),
    Progress = new Progress<ReaderProgress>(p => Console.WriteLine($"Read {p.RecordsRead} recs"))
};

await foreach (var rec in Read.Csv<MyRow>("data.csv", options))
{
    // process
}
```

### 0.4 Simple JSON

*Defaults: `RequireArrayRoot = true`, `AllowSingleObject = true`*

```csharp
// Async
await foreach (var item in Read.Json<MyDoc>("data.json")) { /* ... */ }

// Sync
foreach (var item in Read.JsonSync<MyDoc>("data.json")) { /* ... */ }
```

### 0.5 JSON with Validation / Progress / Single Object Handling

```csharp
var jsonOpts = new JsonReadOptions<MyDoc> {
    RequireArrayRoot = true,
    AllowSingleObject = true,
    ValidateElements = true,
    ElementValidator = e => e.TryGetProperty("id", out _),
    ErrorAction = ReaderErrorAction.Skip,
    ErrorSink = new JsonLinesFileErrorSink("json_errors.ndjson"),
    Progress = new Progress<ReaderProgress>(p => Console.WriteLine($"{p.Percentage:0.0}%"))
};

await foreach (var d in Read.Json<MyDoc>("data.json", jsonOpts)) { /* ... */ }
```

### 0.6 Simple YAML

*Auto mode: detects sequence root OR multi-document format.*

```csharp
await foreach (var obj in Read.Yaml<MyType>("file.yaml")) { /* ... */ }
```

### 0.7 YAML with Type Restrictions

```csharp
var yOpts = new YamlReadOptions<MyType> {
    RestrictTypes = true,
    AllowedTypes = new HashSet<Type> { typeof(MyType) },
    ErrorAction = ReaderErrorAction.Skip,
    ErrorSink = new JsonLinesFileErrorSink("yaml_errors.ndjson")
};

await foreach (var obj in Read.Yaml<MyType>("file.yaml", yOpts)) { /* ... */ }
```

---

## 1. ReadOptions & Error Strategy

### 1.1. Core Option Abstraction

All format-specific option records (`CsvReadOptions`, `JsonReadOptions<T>`, `YamlReadOptions<T>`) inherit from the abstract `ReadOptions`, which provides:

- **`ErrorAction`** (`ReaderErrorAction`): `Throw` | `Skip` | `Stop`
- **`ErrorSink`** (`IReaderErrorSink`)
- **`Progress`** (`IProgress<ReaderProgress>`)
- **`ProgressRecordInterval`**: Default `5000`
- **`ProgressTimeInterval`**: Default `5s`
- **`CancellationToken`**
- **`Metrics`** (`ReaderMetrics`)
- **Stack trace flags**: Unused in default sinks unless you build a custom one.
- **Internal progress gating**: Triggers on time OR record interval.

### 1.2. ReaderErrorAction Semantics

- **`Throw`**: The first error throws an `InvalidDataException` immediately. Enumeration stops.
- **`Skip`**: The error is logged to the sink, the offending record is ignored, and reading continues.
- **`Stop`**: The error is logged, `Metrics.TerminatedEarly` is set to `true`, and the iteration exits gracefully. `options.Complete()` is **NOT** called.

### 1.3. ReaderMetrics Fields

- **`LinesRead`**: Physical lines consumed. (Not always meaningful for JSON/YAML).
- **`RecordsRead`**: Number of successfully emitted logical records (rows, elements, documents).
- **`ErrorCount`**: Total errors encountered, including skipped ones.
- **`LastLineNumber`**: *Declared but not updated; may be deprecated.*
- **`TerminatedEarly`**: `true` if `ErrorAction.Stop` was triggered or the consumer stopped enumeration prematurely.
- **`TerminationErrorMessage`**: The message from the error that triggered the `Stop` action.
- **`StartedUtc` / `CompletedUtc`**: Timestamps. `CompletedUtc` is only set on normal completion, not on `Stop` or exceptions.

### 1.4. Progress Reporting

Progress is triggered when **EITHER** of the following is met:

- `(RecordsRead - lastProgressRecordMark) >= ProgressRecordInterval` (and interval > 0)
- Wall clock time since last emission >= `ProgressTimeInterval`

The `ReaderProgress` structure contains `LinesRead`, `RecordsRead`, `ErrorCount`, `Elapsed`, and `Percentage`.

- **`Percentage`** is only populated when the total size is known (currently only in the JSON reader).

### 1.5. HandleError Workflow

1. Increment `ErrorCount`.

2. Build a `ReaderError` object and send it to `ErrorSink.Report()`. Sink exceptions are swallowed.

3. Apply action logic:
   
   - **`Throw`**: `throw new InvalidDataException(message)`
   
   - **`Stop`**: Set `TerminatedEarly` and `TerminationErrorMessage`, then return `false` to break the loop.
   
   - **`Skip`**: Return `true` to continue.
     
     > **Note**: `RawExcerpt` is a best-effort snippet (e.g., truncated to ~128 chars for JSON).

### 1.6. Early Termination & Finalization

- **Normal Completion**: `options.Complete()` is called, setting `CompletedUtc` and emitting final progress.
- **Termination via `Stop`**: The iterator exits *before* `Complete()` is called, leaving `CompletedUtc` as `null`. This is intentional and can be used to detect abnormal termination.
- **Cancellation or Exception**: `CompletedUtc` also remains `null`.

---

## 2. Error Sinks

### 2.1. Interface

```csharp
public interface IReaderErrorSink : IDisposable
{
    void Report(ReaderError error);
}
```

### 2.2. Built-in Sinks

- **`NullErrorSink`**: A no-op singleton (the default).
- **`JsonLinesFileErrorSink`**: Writes one JSON object per line (NDJSON) to a file. It is thread-safe and flushes on each report.

### 2.3. ReaderError JSON Lines Example (`JsonLinesFileErrorSink`)

```json
{
  "ts": "2025-08-20T12:34:56.7890123Z",
  "reader": "CSV",
  "file": "data.csv",
  "line": 42,
  "record": 40,
  "errorType": "SchemaError",
  "message": "Row has 12 fields but schema has 10.",
  "excerpt": "col1,col2,col3,col4,col5,col6,col7,col8",
  "action": "Skip"
}
```

### 2.4. Custom Sink Pattern

Example of a custom sink that batches errors and forwards them to Serilog.

```csharp
public sealed class SerilogBatchErrorSink : IReaderErrorSink
{
    private readonly List<ReaderError> _buffer = new(256);
    private readonly object _gate = new();
    private readonly int _flushSize;

    public SerilogBatchErrorSink(int flushSize = 100) => _flushSize = flushSize;

    public void Report(ReaderError error)
    {
        lock (_gate)
        {
            _buffer.Add(error);
            if (_buffer.Count >= _flushSize) Flush();
        }
    }

    private void Flush()
    {
        foreach (var e in _buffer)
        {
            Log.Error("[{Reader}] {File}:{Line} rec#{Record} {Type} {Msg}",
                e.Reader, e.FilePath, e.LineNumber, e.RecordNumber, e.ErrorType, e.Message);
        }
        _buffer.Clear();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_buffer.Count > 0) Flush();
        }
    }
}

// Usage:
var opts = new CsvReadOptions {
    ErrorAction = ReaderErrorAction.Skip,
    ErrorSink = new SerilogBatchErrorSink(200)
};
```

### 2.5. Upgrading Legacy `onError` Delegates

Simple overloads wrap the callback inside an internal `DelegatingErrorSink`:

- If `onError == null` -> `ErrorAction = Throw`, `ErrorSink = NullErrorSink`
- If `onError` is provided -> `ErrorAction = Skip`, `ErrorSink = DelegatingErrorSink`

---

## 3. CSV (`CsvReadOptions`)

### 3.1. Fields & Defaults

- **`Separator`**: `,`
- **`Schema`**: `string[]?` (If `null` and `HasHeader` is true, the header row is used as the schema).
- **`HasHeader`**: `true`
- **`TrimWhitespace`**: `true` (Fields are trimmed before mapping).
- **`AllowMissingTrailingFields`**: `true` (Missing trailing columns become `default`; if `false`, an error is raised).
- **`AllowExtraFields`**: `false` (Extra incoming columns cause an error unless set to `true`).

### 3.2. RFC 4180 Compliance & Notes

- Supports double-quote escaping (`""`) and multiline fields.
- Line endings are normalized to `\n`.
- No comment line handling.
- An unterminated quoted field at EOF will result in a `CsvFormatError`.

### 3.3. Strict Mode Recommendation

For "fail fast" ingestion, use the following configuration:

```csharp
new CsvReadOptions {
  HasHeader = true,
  TrimWhitespace = false,
  AllowMissingTrailingFields = false,
  AllowExtraFields = false,
  ErrorAction = ReaderErrorAction.Throw
};
```

### 3.4. Schema Resolution Logic

- If `options.Schema` is `null` and `HasHeader` is `true`, the first record is consumed as the schema header.
- If the schema is still `null` at the first data row, a `SchemaError` is logged for each row.
- Schema mismatches (missing/extra columns with strict options) trigger a `SchemaError`.

### 3.5. Field Type Mapping

Raw string fields are mapped to object properties using reflection (`NEW.GetNew<T>`). The framework attempts to convert the string to the following types in order: `bool` -> `int` -> `long` -> `decimal` -> `double` -> `DateTime` -> `Guid` -> `string`.

### 3.6. Progress & Metrics Behavior

- `LinesRead` increments per physical line.
- `RecordsRead` increments per successfully attempted row.
- Progress emits only counts (no `Percentage`).

---

## 4. JSON (`JsonReadOptions<T>`)

### 4.1. Fields & Defaults

- **`SerializerOptions`**: `System.Text.Json` options (default `PropertyNameCaseInsensitive = true`).
- **`RequireArrayRoot`**: `true`.
- **`AllowSingleObject`**: `true` (Allows a single root object even if `RequireArrayRoot` is true).
- **`ValidateElements`**: `false`.
- **`ElementValidator`**: `Func<JsonElement, bool>?` (Required if `ValidateElements` is true).
- **`MaxDepth`**: `0` (Uses `JsonReader` default).

### 4.2. Root Handling Matrix

- **Root is `StartArray`**: Streams elements from the array.
- **Root is a single value/object**:
  - If `RequireArrayRoot` is `true` AND `AllowSingleObject` is `false` -> `JsonFormatError`.
  - Otherwise, the single object is processed as one logical record.

### 4.3. Fast Path vs. Validation Path

- **Fast Path** (default): Uses `JsonSerializer.Deserialize<T>(ref reader)` for direct, high-throughput streaming.
- **Validation Path** (if `ValidateElements` is true): Each element is parsed into a `JsonDocument` to be validated by `ElementValidator` before deserialization. This path has higher overhead.

### 4.4. Progress Percentage

The JSON reader is the only one that currently reports `Percentage` because it can access the total file size and current stream position.

### 4.5. ElementValidator Usage Example

```csharp
var opts = new JsonReadOptions<MyItem> {
    ValidateElements = true,
    ElementValidator = e => e.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number,
    ErrorAction = ReaderErrorAction.Skip
};
```

---

## 5. YAML (`YamlReadOptions<T>`)

### 5.1. Fields & Defaults

- **`RestrictTypes`**: `true` (Enforces a type whitelist).
- **`AllowedTypes`**: `null` (If `null` while `RestrictTypes` is true, only type `T` is allowed).
- **`DisallowAliases`**: `true` (*Note: Does not fully disable aliases at the parser level*).
- **`DisallowCustomTags`**: `true` (*Note: Not currently used in the reader pipeline*).
- **`UseSequenceStreamMode`**: `true` (*Note: Currently unused; detection is automatic*).
- **`MaxDepth`**: `64` (*Note: Not actively enforced*).

### 5.2. Structural Mode Detection

The reader automatically detects the YAML structure:

- If the root is a sequence (`[...]` or a multi-line list), it iterates each item.
- Otherwise, it falls back to multi-document mode, where each document (`--- ...`) is a record.

### 5.3. Type Restriction Logic

If `RestrictTypes` is `true`:

- If `AllowedTypes` is `null`, only objects of the exact type `T` are permitted (subclasses are rejected).
- If `AllowedTypes` is provided, only types in the set are permitted.
- A rejected type triggers a `TypeRestriction` error.

### 5.4. Aliases / Tags Security Note

**Warning**: The `DisallowAliases` and `DisallowCustomTags` flags do not provide full security guarantees in the current implementation. `DisallowAliases` only sets `IgnoreUnmatchedProperties`, and `DisallowCustomTags` is not referenced. For untrusted YAML, pre-scanning or external sandboxing is recommended.

### 5.5. Error Handling

Deserialization exceptions are handled per document/item. On `Skip`, the reader attempts to consume events until the next `DocumentEnd` to re-synchronize.

### 5.6. Progress & Metrics

- `RecordsRead` increments per successfully emitted item.
- `LinesRead` is not updated (remains `0`).
- `Percentage` is always `null`.

### 5.7. Example Hardened Configuration

```csharp
var yOpts = new YamlReadOptions<MyDto> {
    RestrictTypes = true,
    AllowedTypes = new HashSet<Type>{ typeof(MyDto) },
    ErrorAction = ReaderErrorAction.Stop,
    ErrorSink = new JsonLinesFileErrorSink("yaml_errors.ndjson")
};
// NOTE: DisallowAliases / DisallowCustomTags flags are currently advisory (not enforced).
```

---

## 6. Error Record & Excerpt Details

The `ReaderError` object emitted to sinks contains the following fields:

- **`reader`**: "CSV", "JSON", or "YAML".
- **`file`**: The file path.
- **`line`**: Line number (meaningful for CSV, `-1` otherwise).
- **`record`**: Logical record index.
- **`errorType`**: Classification like `SchemaError`, `JsonFormatError`, etc.
- **`message`**: The error message.
- **`excerpt`**: A truncated snippet of the raw data.
- **`action`**: The `ReaderErrorAction` that was taken (`Skip`, `Stop`, `Throw`).

---

## 7. Progress Usage Examples

### 7.1. Basic Count-Driven Progress

```csharp
var opts = new CsvReadOptions {
    Progress = new Progress<ReaderProgress>(p =>
        Console.WriteLine($"Records={p.RecordsRead} Errors={p.ErrorCount}")
    ),
    ProgressRecordInterval = 1000
};
```

### 7.2. Time-Driven Progress

```csharp
var opts = new JsonReadOptions<MyDoc> {
    Progress = new Progress<ReaderProgress>(p =>
        Console.WriteLine($"{p.Percentage?.ToString("0.00") ?? "?"}% ({p.RecordsRead} recs)")
    ),
    ProgressRecordInterval = 0, // Disable count-based trigger
    ProgressTimeInterval = TimeSpan.FromSeconds(2)
};
```

### 7.3. Dual Trigger (Default)

The default configuration triggers progress whichever comes first: every 5 seconds or every 5000 records.

---

## 8. Known Limitations

- **CSV**: Does not track column numbers in errors. `LastLineNumber` metric is not updated.
- **JSON**: The validation path incurs extra allocation overhead. `CompletedUtc` is not set on `Stop`.
- **YAML**: `DisallowAliases` and `DisallowCustomTags` flags are not fully enforced. `LinesRead` and `MaxDepth` are not used.
- **General**: Error sink failures are silently swallowed. `CompletedUtc` is `null` in all abnormal termination scenarios (`Stop`, exception, cancellation).

---

## 9. Side-by-Side Quick Reference

| Format   | Simple Overload                                    | Options Record       | Special Features                                                                                  |
|:-------- |:-------------------------------------------------- |:-------------------- |:------------------------------------------------------------------------------------------------- |
| **CSV**  | `Read.Csv<T>(path, sep, onError?, schema...)`      | `CsvReadOptions`     | RFC4180 multiline, schema inference, type inference.                                              |
| **JSON** | `Read.Json<T>(path, serializerOptions?, onError?)` | `JsonReadOptions<T>` | Streaming `Utf8JsonReader`, array or single object root, optional element validation, progress %. |
| **YAML** | `Read.Yaml<T>(path, deserializer?, onError?)`      | `YamlReadOptions<T>` | Auto sequence vs. multi-doc detection, type restriction whitelist.                                |

---

## 10. Full Integration Examples (Pipeline Style)

*Note: In DataFlow.NET, prefer streaming transformation pipelines (`Select` / `Cases` / `SelectCase` / `ForEachCase` / `AllCases` / `WriteX`) over manual loops to preserve laziness, enable zero-cost composition, and keep batch vs. streaming symmetry.*

Assume the following domain types:

```csharp
record RawOrder(string Id, decimal Amount, string Country, bool Priority);
record OrderEnriched(string Id, decimal Amount, string Country, string Tier, bool Priority, DateTime IngestedUtc);
record Alert(string OrderId, string Severity, string Reason);
record EventIn(string Type, string Source, DateTime Ts, int Severity);
record NormalizedEvent(string Type, string Source, DateTime Ts, int Severity, string Bucket);
record ConfigNode(string Key, string Value, string Environment);
record UnifiedMessage(string Source, string Kind, string Id, string Detail, DateTime AtUtc);
```

---

### Example 1: CSV → Enrichment → Categorization → Write JSON

This example reads CSV orders, enriches them with a calculated tier, categorizes them, creates alerts for specific categories, and writes the resulting alerts to a JSON file.

```csharp
var csvOpts = new CsvReadOptions {
    HasHeader = true,
    AllowMissingTrailingFields = false,
    AllowExtraFields = false,
    ErrorAction = ReaderErrorAction.Skip,
    ErrorSink = new JsonLinesFileErrorSink("orders_csv_errors.ndjson", append: true),
    Progress = new Progress<ReaderProgress>(p =>
        Console.WriteLine($"[CSV] {p.RecordsRead} rows ({p.ErrorCount} errors)"))
};

// Build the pipeline (lazy execution)
var alertPipeline =
    Read.Csv<RawOrder>("orders.csv", csvOpts)
        .Select(o => new OrderEnriched(
            o.Id,
            o.Amount,
            o.Country,
            Tier: o.Amount >= 5000 ? "Platinum" :
                  o.Amount >= 1000 ? "Gold" :
                  o.Amount >= 250  ? "Silver" : "Standard",
            Priority: o.Priority,
            IngestedUtc: DateTime.UtcNow))
        .Cases(
            o => o.Priority,      // Category 0: Priority
            o => o.Tier == "Gold" || o.Tier == "Platinum",  // Category 1: High tier
            o => o.Country != "US" // Category 2: Export
            // Supra category 3: everything else
        )
        .SelectCase(
            // For categories 0,1,2 produce Alerts; supra items become null
            pri => new Alert(pri.Id, "High", "Priority flag"),
            tier => new Alert(tier.Id, "Info", "High tier loyalty"),
            export => new Alert(export.Id, "Info", "Export shipment"),
            _ => null
        )
        .Where(x => x.newItem != null)  // Filter out the ignored supra items
        .AllCases(); // The result is a clean IAsyncEnumerable<Alert>

// Terminal write operation triggers the pipeline enumeration
await alertPipeline.WriteJson("alerts.json");

// After enumeration, metrics are available for inspection
Console.WriteLine($"CSV RecordsRead={csvOpts.Metrics.RecordsRead} Errors={csvOpts.Metrics.ErrorCount} Completed={csvOpts.Metrics.CompletedUtc != null}");
```

---

### Example 2: JSON Stream → Validation → Side-Effects → Write CSV

This pipeline validates incoming JSON events, normalizes them, performs console actions for high-priority events, and writes all normalized events to a CSV file.

```csharp
var jsonOpts = new JsonReadOptions<EventIn> {
    RequireArrayRoot = true,
    AllowSingleObject = true, // Allow a single event file
    ValidateElements = true,
    ElementValidator = e =>
        e.TryGetProperty("Type", out var t) && t.ValueKind == JsonValueKind.String &&
        e.TryGetProperty("Severity", out var s) && s.ValueKind == JsonValueKind.Number,
    ErrorAction = ReaderErrorAction.Skip,
    ErrorSink = new JsonLinesFileErrorSink("events_json_errors.ndjson"),
    Progress = new Progress<ReaderProgress>(p =>
        Console.WriteLine($"[JSON] {p.Percentage?.ToString("0.0") ?? "?"}% {p.RecordsRead} events"))
};

await Read.Json<EventIn>("events.json", jsonOpts)
    .Select(e => new NormalizedEvent(
        e.Type,
        e.Source,
        e.Ts,
        e.Severity,
        Bucket: e.Severity >= 8 ? "Critical" :
                e.Severity >= 5 ? "High" :
                e.Severity >= 3 ? "Medium" : "Low"))
    .Cases(
        n => n.Bucket == "Critical", // Category 0
        n => n.Bucket == "High"      // Category 1
        // Supra: Medium / Low
    )
    .ForEachCase(
        // Perform actions for specific categories
        critical => Console.WriteLine($"CRIT {critical.Source}:{critical.Type} Sev={critical.Severity}"),
        high => Console.WriteLine($"HIGH  {high.Source}:{high.Type} Sev={high.Severity}"),
        // Provide an explicit no-op for the supra category to align delegates
        n => { }
    )
    .AllCases()    // Extract the transformed items (NormalizedEvent)
    .WriteCsv("events_processed.csv"); // Terminal write

// Inspect metrics after completion
Console.WriteLine($"JSON Records={jsonOpts.Metrics.RecordsRead} Errors={jsonOpts.Metrics.ErrorCount}");
```

---

### Example 3: YAML → Type Restriction → Categorization → Write Text

This example reads YAML configuration documents, enforces type safety, categorizes them by environment, and writes only the important (prod/staging) key-value pairs to a text file.

```csharp
var yamlOpts = new YamlReadOptions<ConfigNode> {
    RestrictTypes = true,
    AllowedTypes = new HashSet<Type>{ typeof(ConfigNode) },
    ErrorAction = ReaderErrorAction.Skip,
    ErrorSink = new JsonLinesFileErrorSink("config_yaml_errors.ndjson"),
    Progress = new Progress<ReaderProgress>(p =>
        Console.WriteLine($"[YAML] {p.RecordsRead} docs, Errors={p.ErrorCount}"))
};

await Read.Yaml<ConfigNode>("configuration.yaml", yamlOpts)
    .Cases(
        c => c.Environment == "prod",    // Category 0
        c => c.Environment == "staging" // Category 1
        // Supra: dev / test / other
    )
    .SelectCase(
        prod => $"[PROD] {prod.Key}={prod.Value}",
        staging => $"[STAGING] {staging.Key}={staging.Value}",
        other => null  // Discard dev/test lines by returning null
    )
    .Where(x => x != null) // Filter out discarded items
    .WriteText("important_config.txt");

Console.WriteLine($"YAML Records={yamlOpts.Metrics.RecordsRead} Errors={yamlOpts.Metrics.ErrorCount}");
```

---

### Example 4: Merging Different Formats into One Unified Pipeline

This advanced example demonstrates fusing data from CSV, JSON, and YAML sources into a single, unified stream, which is then categorized and written to a final NDJSON file.

```csharp
// Use the same options records from previous examples (csvOpts, jsonOpts, yamlOpts)

var unifiedPipeline =
    Read.Csv<RawOrder>("orders.csv", csvOpts)
        .Select(o => new UnifiedMessage("orders", "order",
            o.Id, $"Amount={o.Amount} Country={o.Country}", DateTime.UtcNow))
    .Concat(
        Read.Json<EventIn>("events.json", jsonOpts)
            .Select(e => new UnifiedMessage("events", "event",
                e.Type, $"Severity={e.Severity} Src={e.Source}", e.Ts))
    )
    .Concat(
        Read.Yaml<ConfigNode>("configuration.yaml", yamlOpts)
            .Select(c => new UnifiedMessage("config", "kv",
                c.Key, $"{c.Environment}:{c.Value}", DateTime.UtcNow))
    )
    // Categorize the combined unified stream
    .Cases(
        m => m.Source == "orders" && m.Detail.Contains("Amount="),
        m => m.Source == "events" && m.Detail.Contains("Severity=8"),
        m => m.Source == "config" && m.Detail.Contains("prod")
    )
    .SelectCase(
        orderMsg => orderMsg with { Kind = "order-important" },
        severeEvent => severeEvent with { Kind = "event-critical" },
        prodCfg => prodCfg with { Kind = "config-prod" },
        other => other // Leave supra category items unchanged
    )
    .AllCases(); // Final result is IAsyncEnumerable<UnifiedMessage>

await unifiedPipeline.WriteJson("unified_messages.json");
```

---

### Example 5: Handling Metrics After Unified Pipeline Completion

After the unified pipeline from Example 4 has been enumerated, you can inspect the metrics from each individual reader.

```csharp
// After enumeration of the unified pipeline
Console.WriteLine("---- Metrics Summary ----");
Console.WriteLine($"Orders: {csvOpts.Metrics.RecordsRead} rows, errors={csvOpts.Metrics.ErrorCount}");
Console.WriteLine($"Events: {jsonOpts.Metrics.RecordsRead} events, errors={jsonOpts.Metrics.ErrorCount}");
Console.WriteLine($"Configs: {yamlOpts.Metrics.RecordsRead} docs, errors={yamlOpts.Metrics.ErrorCount}");
```