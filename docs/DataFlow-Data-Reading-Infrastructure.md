# DataFlow.Data Reading Infrastructure

This document provides a deep-dive into the reading infrastructure of the `DataFlow.Data` layer, covering configuration, error handling, and format-specific options.

## Table of Contents

1. [Fast Usage Overview](#0-fast-usage-overview)
2. [Options Architecture](#1-options-architecture)
3. [Error Handling](#2-error-handling)
4. [CSV Reader](#3-csv-reader)
5. [JSON Reader](#4-json-reader)
6. [YAML Reader](#5-yaml-reader)
7. [Text Reader](#6-text-reader)

**Related:** [ObjectMaterializer](ObjectMaterializer.md) — Low-level object creation from structured data

---

## 0. Fast Usage Overview

### 0.1 Asynchrony Convention (IMPORTANT)

Default method names are ASYNCHRONOUS. Synchronous variants use the `Sync` suffix.

- Async: `Read.Csv<T>()` returns `IAsyncEnumerable<T>`
- Sync: `Read.CsvSync<T>()` returns `IEnumerable<T>`
- Async: `Read.Yaml<T>()` returns `IAsyncEnumerable<T>`
- Sync: `Read.YamlSync<T>()` returns `IEnumerable<T>`
- Async: `Read.Json<T>()` returns `IAsyncEnumerable<T>`
- Sync: `Read.JsonSync<T>()` returns `IEnumerable<T>`
- Async: `Read.Text()` returns `IAsyncEnumerable<string>`
- Sync: `Read.TextSync()` returns `IEnumerable<string>`

Note: From-string helpers are exposed as string extension methods. They are excluded from this naming convention ( see next Paragraph for details).

### 0.2 String-Based Sync APIs

Quick parsing directly from an in-memory string. These are synchronous and delegate to the stream-based cores; diagnostics use file="(string)".  These are exposed as string extension methods on string.
 

```csharp
// CSV (options-based and simple)
IEnumerable<T> rows = csvText.AsCsv<T>(csvOptions, ct);
IEnumerable<T> rows2 = csvText.AsCsv<T>(separator: ",", onError: (raw, ex) => { }, ct);

// JSON
IEnumerable<T> items = jsonText.AsJson<T>(jsonOptions, ct);
IEnumerable<T> items2 = jsonText.AsJson<T>(serializerOptions: null, onError: ex => { }, ct);

// YAML
IEnumerable<T> docs = yamlText.AsYaml<T>(yamlOptions, ct);
IEnumerable<T> docs2 = yamlText.AsYaml<T>(onError: ex => { }, ct);
```


| Format | From-string sync overload (string extensions) |
| ------ | --------------------------------------------- |
| CSV    | `string.AsCsv<T>(CsvReadOptions, CancellationToken)`; `string.AsCsv<T>(string separator=",", Action<string,Exception>? onError=null, CancellationToken ct=default, params string[] schema)` |
| JSON   | `string.AsJson<T>(JsonReadOptions<T>, CancellationToken)`; `string.AsJson<T>(JsonSerializerOptions? serializerOptions=null, Action<Exception>? onError=null, CancellationToken ct=default)` |
| YAML   | `string.AsYaml<T>(YamlReadOptions<T>, CancellationToken)`; `string.AsYaml<T>(Action<Exception>? onError=null, CancellationToken ct=default)` |

Diagnostics use `file="(string)"`. Error handling, guard rails, and metrics mirror the stream/file sync paths.

Note: 
- the from-string overloads allocate a byte[] roughly equal to UTF‑8 length of the string.
- UTF‑8 is only an internal transport for string inputs; file encodings remain controlled by the file/stream path.


### 0.3 Stream-Based APIs

Every file-based reader now has a stream-based counterpart that the file overload delegates to.  
Use these when you already have an open `Stream` (e.g., memory streams, network streams, zip entries) to avoid temporary files and to keep ownership / lifetime under your control.

#### Options-Based Stream API (Full Control)

Use these when you need fine-grained control over parsing behavior and error reporting:

```csharp
// CSV - filePath improves error diagnostics
await foreach (var row in Read.Csv<MyRow>(myStream, csvOptions, filePath: "orders.csv"))
{ /* ... */ }

// JSON
await foreach (var item in Read.Json<MyDoc>(myStream, jsonOptions, filePath: "data.json"))
{ /* ... */ }

// YAML
await foreach (var doc in Read.Yaml<MyType>(myStream, yamlOptions, filePath: "config.yaml"))
{ /* ... */ }

// Text lines
await foreach (var line in Read.Text(myStream, textOptions, filePath: "log.txt"))
{ /* ... */ }
```

#### Simple Stream API (Minimal)

Use these for quick parsing with sensible defaults. The simple API is symmetric with the file API - just swap `path` for `stream`:

```csharp
// CSV - matches Csv<T>(path, separator, onError, token, schema)
await foreach (var row in Read.Csv<MyRow>(myStream, ",", onError: (raw, ex) => Log(ex)))
{ /* ... */ }

// JSON - matches Json<T>(path, options, onError, token)
await foreach (var item in Read.Json<MyDoc>(myStream))
{ /* ... */ }

// YAML - matches Yaml<T>(path, onError, token)
await foreach (var doc in Read.Yaml<MyType>(myStream))
{ /* ... */ }

// Text - matches Text(path, token)
await foreach (var line in Read.Text(myStream))
{ /* ... */ }
```

Notes:
* **Options-based API**: `filePath` is optional; supplying it improves error diagnostics (`file` field in error records). If omitted, an internal placeholder `"(stream)"` is used.
* **Simple API**: Does not expose `filePath` - use the options-based API if you need custom error context.
* The passed `Stream` is NOT disposed by the reader; the caller retains lifecycle responsibility.
* Cancellation: both the per-call token and the options-level token are honored.
* Progress percentage for JSON is only computed when the stream is seekable (`CanSeek == true`). Otherwise `Percentage` is `null`.
* Guard rails, inference, error handling, and cancellation semantics are identical to file-based usage.

### 0.4 Read Raw Text Lines

```csharp
// Async
IAsyncEnumerable<string> lines = Read.Text("file.txt");

// Sync
IEnumerable<string> linesSync = Read.TextSync("file.txt");
```

### 0.5 Simple CSV (Default RFC-leaning behavior)

Behavior: If no schema is provided and HasHeader = true (default), the first row is treated as a header. Errors throw by default unless you change ErrorAction or use the simple overload with an onError delegate.

```csharp

// Simplest call (errors throw by default):
var rows = Read.Csv<MyRow>("data.csv");

var rowsSync = Read.CsvSync<MyRow>("data.csv");

// Provide a schema for a header-less file
var rows2 = Read.Csv<MyRow>(
    "data_no_header.csv",
    new CsvReadOptions {
    HasHeader = false,
    Schema = new[] { "Id", "Name", "Price" }
});

// Handle errors by skipping instead of throwing (options-based)
var rows3 = Read.Csv<MyRow>(
    "maybe_dirty.csv",
    new CsvReadOptions {
    ErrorAction = ReaderErrorAction.Skip,
    ErrorSink = new JsonLinesFileErrorSink("maybe_dirty_errors.ndjson")
});

// QUICK AD-HOC: use the simple overload with an onError delegate (cannot customize other options through this overload):
var quick = Read.Csv<MyRow>(
    "maybe_dirty.csv",
    onError: (rawLineExcerpt, ex) => Console.WriteLine($"Row skipped: {ex.Message}"));
```
Notes:

- In the simple overload above, passing onError automatically sets ErrorAction = Skip internally and wraps the delegate with an internal bridge (DelegatingErrorSink). You cannot directly construct DelegatingErrorSink in your own code.
- You can ONLY adjust separator, schema and onError via the simple CSV overload. All advanced behaviors (inference, quoting modes, auditing, custom sinks, progress) require the options-based overload.
- To print structured error info when using the options-based API, implement a small custom IReaderErrorSink (see Section 2.5).
 
### 0.6 CSV With Schema & Type Inference

```csharp
var infOpts = new CsvReadOptions {
    HasHeader = true,
    InferSchema = true,
    SchemaInferenceMode = SchemaInferenceMode.ColumnNamesAndTypes,
    SchemaInferenceSampleRows = 200,
    FieldTypeInference = FieldTypeInferenceMode.Primitive,
    Progress = new Progress<ReaderProgress>(p => Console.WriteLine($"Rows={p.RecordsRead}"))
};

await foreach (var rec in Read.Csv<MyRow>("typed_data.csv", infOpts))
{
    // Use rec
}

Console.WriteLine("Inferred column CLR types:");
for (int i = 0; i < infOpts.InferredTypes!.Length; i++)
    Console.WriteLine($"{infOpts.Schema![i]} -> {infOpts.InferredTypes[i].Name}");
 
```

### 0.7 CSV Capturing Raw Records (Auditing)

```csharp
var auditOpts = new CsvReadOptions {
    HasHeader = true,
    RawRecordObserver = (n, raw) => AuditLog.WriteLine($"{n}:{raw}")
};
await foreach (var r in Read.Csv<MyRow>("audited.csv", auditOpts)) { }
```

Notes:
- Full raw record capture is enabled automatically when `RawRecordObserver` is non-null.
- Without an observer, only a lightweight 0..128 char raw prefix is retained for error diagnostics.


### 0.8 Full CSV with Options (Strict ingestion)

```csharp
var options = new CsvReadOptions {
    HasHeader = true,
    Separator = ';',
    AllowMissingTrailingFields = false,
    AllowExtraFields = false,
    TrimWhitespace = false,
    QuoteMode = CsvQuoteMode.RfcStrict,
    // For true strict ingestion we fail fast (Throw). Change to Skip if you prefer lenient continuation.
    ErrorAction = ReaderErrorAction.Throw,
    ErrorSink = new JsonLinesFileErrorSink("csv_errors.ndjson"), // Optional when ErrorAction=Throw
    Progress = new Progress<ReaderProgress>(p => Console.WriteLine($"Read {p.RecordsRead} recs"))
};

await foreach (var rec in Read.Csv<MyRow>("data.csv", options))
{
 // process
}
```
Note: When ErrorAction = Throw, the first error will raise an InvalidDataException and terminate enumeration. In that fail-fast mode an ErrorSink is optional. Configure an ErrorSink only if you want a persisted record of the first (and only) failure or are switching to Skip/Stop later.

### 0.9 Simple JSON

Defaults: `RequireArrayRoot = true`, `AllowSingleObject = true`

```csharp
await foreach (var item in Read.Json<MyDoc>("data.json")) { /* ... */ }
```

### 0.10 JSON with Validation / Progress / Single Object Handling

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

// Ad-hoc quick delegate form — no direct options customization besides serializer & error style:
await foreach (var d in Read.Json<MyDoc>(
    "data.json",
    onError: ex => Console.WriteLine($"JSON error: {ex.Message}")))
{
    /* ... */
}
```

### 0.11 Simple YAML

```csharp
await foreach (var obj in Read.Yaml<MyType>("file.yaml")) { /* ... */ }
```

### 0.12 YAML with Type Restrictions

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

### 1.1 Core Option Abstraction

All format-specific option records (`CsvReadOptions`, `JsonReadOptions<T>`, `YamlReadOptions<T>`) inherit from `ReadOptions`, which provides:

- ErrorAction (`ReaderErrorAction`): Throw | Skip | Stop
- ErrorSink (`IReaderErrorSink`)
- Progress (`IProgress<ReaderProgress>`)
- ProgressRecordInterval (default 5000)
- ProgressTimeInterval (default 5s)
- CancellationToken
- Metrics (`ReaderMetrics`)
- Internal progress gating (record OR time driven)

### 1.2 ReaderErrorAction Semantics

- Throw: first error throws `InvalidDataException`
- Skip: log & continue
- Stop: log, set `TerminatedEarly`, exit enumeration (no `Complete()`)

### 1.3 ReaderMetrics Fields

The `Metrics` object on the options record tracks statistics during a read operation.

- **`LinesRead`**: The number of physical lines (based on newline characters) read from the source. Primarily used by the CSV reader.
- **`RawRecordsParsed`**: Count of logical records fully parsed (including those skipped due to per-record errors). For JSON single-root this is set to 1 only after the root value is fully processed. For JSON `MaxElements` guard rail violations the violating (excess) element is not counted.
- **`RecordsEmitted`**: The final count of records successfully deserialized and yielded by the reader. This matches the number of items in the resulting `IEnumerable` or `IAsyncEnumerable`. The `RecordsRead` property on the `ReaderProgress` object is populated from this value.
- **`ErrorCount`**: The total number of errors encountered and reported to the `ErrorSink`.
- **`TerminatedEarly`**: A boolean flag set to `true` if the read operation was stopped prematurely by the `Stop` error action or a fatal error.
- **`TerminationErrorMessage`**: If `TerminatedEarly` is true, this may contain the message of the error that caused the termination.
- **`StartedUtc` / `CompletedUtc`**: Timestamps for the start and successful completion of the read operation. `CompletedUtc` will be null if the operation is terminated early or cancelled.

### 1.4 Progress Reporting

Triggers when:

- Records since last >= ProgressRecordInterval (if > 0), OR
- Elapsed wall time >= ProgressTimeInterval

`ReaderProgress` includes counts, elapsed, optional percentage (JSON only currently).

JSON Single-Root (Non-Array) Progress Nuance:
- Fast path (no validation / guard rails): a percentage update can occur after the single value is fully deserialized (may appear as a direct jump from a very low initial percentage to 100% for small files).
- Validation / guard-rail path (`ValidateElements` = `true` OR `GuardRailsEnabled` OR `MaxStringLength` > 0): the implementation loads and processes the entire file in `ProcessSingleRootValidationFromStream` without intermediate progress callbacks; you typically see only an initial (near 0%) event (if any) and a final completion (100%). This is by design to avoid partial metrics while the full element is being materialized.

### 1.5 HandleError Workflow

1. Increment `ErrorCount`
2. Produce `ReaderError` -> `ErrorSink.Report`
3. Apply action logic (Throw / Stop / Skip)
4. Return boolean controlling loop continuation

### 1.6 Early Termination & Finalization

- Normal completion: the reader calls `Complete()`, which sets `CompletedUtc`.
- `ErrorAction = Stop`: the first error that triggers Stop sets `Metrics.TerminatedEarly = true`; the reader exits without calling `Complete()`; `CompletedUtc` remains null.
- `ErrorAction` = `Throw`: the first error throws `InvalidDataException` after any already‑parsed rows have been yielded; `Metrics.TerminatedEarly` = `true` and `CompletedUtc` remains null (`Complete()` is not invoked). In Throw mode, if an excerpt was captured for the error, it is appended to the exception message as:  " | excerpt: {excerpt}".
- Cancellation: enumeration stops; `Complete()` is not called; `CompletedUtc` remains null. (TerminatedEarly is not set unless future revisions decide to treat cancellation as a termination condition for that flag.)
- In all early termination cases (Stop or Throw), previously emitted records `(RecordsEmitted` > 0) remain valid and can be consumed, but the absence of `CompletedUtc` + `TerminatedEarly` = `true` signals the read did not finish normally.

---
### 1.7 Cancellation Tokens

All readers (CSV, JSON, YAML, Text) implement uniform cooperative cancellation for both async and sync APIs.

Cancellation sources:
- Options-level token: `ReadOptions.CancellationToken` (or format-specific options)
- Per-call token: the method parameter `CancellationToken`

Both tokens are observed; if either is canceled an `OperationCanceledException` is thrown (never routed through error handling, never downgraded, never converted to `TaskCanceledException`).

Semantics:
- Exception is propagated immediately (not logged, not counted as an error, not setting TerminatedEarly).
- `Metrics.CompletedUtc` remains null on cancellation; previously accumulated metrics (e.g. bytes read, rows parsed) are left as-is.
- No partial item is emitted after cancellation.

Check granularity (where cancellation is polled):
- CSV: during buffer refills and before each record is materialized.
- JSON: each outer read loop iteration, before/after token scans, during single-root materialization (including large validation copy).
- YAML: between document boundaries and major node events.
- Text line readers: per line (and during large buffer refills).
- Sync wrappers over async cores inherit the same checkpoints.

Best practice: pass a single upstream CancellationToken directly to the read call. Use the options-level token only when embedding cancellation into reusable option instances.

Example:
```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
await foreach (var row in Read.Csv<MyRow>("data.csv", csvOptions, cts.Token))
{
    // ...
}
```

---

## 2. Error Sinks

### 2.1 Interface
```csharp
public interface IReaderErrorSink : IDisposable
{
    void Report(ReaderError error);
}
```

### 2.2 Built-in Sinks

- NullErrorSink (default)
- JsonLinesFileErrorSink (thread-safe NDJSON)

### 2.3 Example JSON Error Record

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

### 2.4 Custom Sink Pattern


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

### 2.5  `onError` Delegates

When you use the simple overloads:

- CSV: Read.Csv<T>(path, separator?, onError?, schema?)
- JSON: Read.Json<T>(path, serializerOptions?, onError?)
- YAML: Read.Yaml<T>(path, deserializer?, onError?)

If you supply an onError delegate:

- ErrorAction is set to Skip.
- Your delegate is wrapped internally by a private bridge sink (an internal DelegatingErrorSink defined inside Read). This class is not part of the public API surface and cannot be instantiated directly.
- For CSV the delegate signature is (string rawExcerpt, Exception ex).
- For JSON/YAML the delegate signature is (Exception ex).

DelegatingErrorSink Wrapping Behavior (Important):

- The exception instance passed to your delegate is always a newly created InvalidDataException built from the reader error’s Message. If an excerpt is available it is appended as " | excerpt: {excerpt}". For CSV simple overloads the raw excerpt is still passed separately as the first delegate parameter. Original exception type, stack trace, InnerException, and any additional data are not preserved.
- Consequently you cannot distinguish (via the simple overload) between, for example, a schema width error vs. a conversion exception except by inspecting ex.Message or (for CSV) the excerpt text.
- If you need original exception types, stack traces, line/record numbers, errorType, or consistent excerpt policies, use the options-based API with a custom IReaderErrorSink.

If you need richer error data (line, record index, type, excerpt), use the options-based API with a custom sink:

Example minimal custom sink (property names aligned with ReaderError public model):

```csharp
sealed class ConsoleErrorSink : IReaderErrorSink
{
    public void Report(ReaderError error)
        => Console.WriteLine($"[{error.Reader}] file={error.FilePath} rec={error.RecordNumber} line={error.LineNumber} type={error.ErrorType} msg={error.Message} excerpt={error.RawExcerpt}");
    public void Dispose() { }
}

// Usage:
var opts = new CsvReadOptions {
    ErrorAction = ReaderErrorAction.Skip,
    ErrorSink = new ConsoleErrorSink()
};

await foreach (var r in Read.Csv<MyRow>("data.csv", opts)) { }
```

Naming note:
- The in-memory object uses RawExcerpt (original snippet). When serialized (e.g., by JsonLinesFileErrorSink) this appears as excerpt for consistency with the documentation tables.
- LineNumber / RecordNumber are the object property names; the serialized JSON uses line / record fields.

If you rely on JSON field names only, prefer deserializing to a DTO that maps line -> LineNumber etc., or keep them as-is for logging.

---

## 3. CSV (CsvReadOptions)

### 3.1 Core Fields & Defaults (Updated)

- Separator: `,`
- Schema: `string[]?` (if null & `HasHeader` true, header row consumed)
- HasHeader: `true`
- TrimWhitespace: `false`  (BREAKING CHANGE; previously true)
- AllowMissingTrailingFields: `true`
- AllowExtraFields: `false`

### 3.2 Quoting & QuoteMode

New enum `CsvQuoteMode`:

- RfcStrict (default): Only a quote at start of field opens quoted mode; stray mid-field quotes produce `CsvQuoteError`.
- Lenient: A quote transitions into quoted mode even mid-field.
- ErrorOnIllegalQuote: Mid-field quote triggers `CsvQuoteError`; action determined by `ErrorAction`.

Additional controls:

- ErrorOnTrailingGarbageAfterClosingQuote (default true): Characters other than separator/newline after closing quote generate `CsvQuoteError`.
- Unterminated quoted field at EOF -> `CsvQuoteError`.

### 3.3 Line Ending Fidelity

- PreserveLineEndings (default true): CRLF preserved exactly.
- NormalizeNewlinesInFields (default false): If enabled (and not preserving), CRLF inside quoted fields converted to LF. (Normalization is field-scoped, not global).
- Metrics LinesRead counts physical line terminations encountered.

### 3.4 Schema & Column Name Inference

Enable via:

- `InferSchema = true`
- `SchemaInferenceMode`:
  - ColumnNamesOnly
  - ColumnNamesAndTypes

Behavior:

- If no header and no schema: synthetic names generated `Column1..N`.
- Optional `GenerateColumnName` delegate `(rawHeaderCell, filePath, index, defaultName)` allows custom naming (e.g., sanitize, deduplicate).
- Sampling: up to `SchemaInferenceSampleRows` (default 100 unless changed) records buffered for inference; beyond that streaming resumes.


### 3.5 Type Inference & Field Conversion

Controlled via:

- `FieldTypeInference`:
  - None (all strings)
  - Primitive (default; bool,int,long,decimal,double,DateTime,Guid)
  - Custom (use `FieldValueConverter` delegate)

Two-phase approach when `SchemaInferenceMode = ColumnNamesAndTypes`:

1. Sampling Phase:
   - Candidate set per column starts with precedence:
     bool → int → long → decimal → double → DateTime → Guid
   - “Systematic error learning”: first parse failure for a candidate in a column is tolerated; the candidate is only removed after a second failure in the SAME column (treat single failure as anomaly).
   - Preservation rules:
     - PreserveNumericStringsWithLeadingZeros: if value matches leading-zero digits, numeric candidates removed (kept as string).
     - PreserveLargeIntegerStrings: if length > 18 digits, numeric candidates removed (avoid precision loss).
2. Enforcement Phase:
   - Inferred types stored in `InferredTypes`.
   - Runtime conversion is strict; on the FIRST conversion failure for a finalized inferred column type, that column is permanently demoted to `string` and subsequent rows use the raw string value (immediate demotion; no “second failure” tolerance at runtime). Demotion is one-way and does not emit an additional error after the failure that triggered demotion.
   - The demotion logic resides in CsvReadOptions.ConvertFieldValue (not shown here). It consults internal per-column state (InferredTypeFinalized + inferred type array). If future changes alter this behavior (e.g., two-failure tolerance at runtime), this section must be updated accordingly.
   - Casting order: direct parse to the inferred type; no fallback chain except demotion-to-string.

Custom Conversion:

- When `FieldTypeInference = Custom`, `FieldValueConverter(string raw)` is used for EVERY field (bypass primitive chain). Return any object (including leaving as string).

Fallback Behavior:

- If no candidate types survive sampling for a column, it defaults to `string`.

### 3.6 Raw Record Capture & Auditing

- `RawRecordObserver` (recordNumber, rawLine): When set, full raw record accumulation is enabled and the original record text (as read, including separators and original line endings if preserved) is streamed to the observer for each logical record.
- Raw capture is literal; doubled quotes remain doubled.
- Even when no observer is set, a small always-on 0..128 character raw prefix buffer is kept solely for error excerpts and guard-rail checks (CsvQuoteError, CsvLimitExceeded) to avoid full per-record memory costs.
- For large files, prefer RawRecordObserver for streaming audit pipelines.

### 3.7 Legacy Behavior Emulation (Migration Guidance)

To replicate pre-overhaul (lenient) style:

```csharp
var legacyLike = new CsvReadOptions {
    TrimWhitespace = true,
    QuoteMode = CsvQuoteMode.Lenient,
    InferSchema = false,
    FieldTypeInference = FieldTypeInferenceMode.Primitive,
    PreserveLineEndings = false,
    NormalizeNewlinesInFields = true // old behavior tended to normalize
};
```

### 3.8 Strict Ingestion Recommendation

```csharp
var strict = new CsvReadOptions {
    HasHeader = true,
    TrimWhitespace = false,
    QuoteMode = CsvQuoteMode.RfcStrict,
    AllowMissingTrailingFields = false,
    AllowExtraFields = false,
    ErrorAction = ReaderErrorAction.Throw
};
```

### 3.9 Error Types (CSV)

The CSV reader can produce several distinct error types, which are reported to the configured `ErrorSink`. Common types include:

- `SchemaError`
- `CsvQuoteError`
- `CsvLimitExceeded`: A configured guard rail (MaxColumnsPerRow or MaxRawRecordLength) was exceeded. Row skipped or ingestion terminated per ErrorAction.

See **Section 6.3 Common Error Types** for detailed descriptions.

### 3.10 Field Mapping Pipeline

Order in row processing:

1. Raw parsing (respect quotes, line endings)
2. Optional trim (if TrimWhitespace = true)
3. Schema width adjustment (missing vs. extra fields)
4. Type conversion using `ConvertFieldValue` (inference-aware)
5. Object materialization (`ObjectMaterializer.Create<T>`)

### 3.11 Progress & Metrics

- `LinesRead` increments with each completed physical line delimiter (CR, LF, or CRLF).
- `RecordsEmitted` increments after each successfully emitted logical record (post-mapping). This is the value reported as `RecordsRead` in `ReaderProgress` events.
- `RawRecordsParsed` increments for each logical row processed from the file, including those that are later skipped due to errors.
- In CSV parsing, percentage is not computed (file length not consulted).
- Raw record capture does not affect metrics.

---

### 3.12 CSV Guard Rails (Limits)

CSV ingestion can be defensively bounded using two optional limits. Both default to 0 (disabled). When a limit is exceeded, the reader reports errorType = CsvLimitExceeded and applies ErrorAction (Throw | Skip | Stop).

Fields (CsvReadOptions):
- MaxColumnsPerRow (int, default 0)
  Maximum allowed number of parsed columns (fields) in a single logical record (after RFC quoting normalization, before schema mapping). If the row exceeds this count, the record is discarded or terminates the read per ErrorAction.
- MaxRawRecordLength (int, default 0)
  Maximum allowed raw character length of a single record, measured as the number of characters accumulated while reading the record, including separators, quotes, internal embedded newlines inside quoted fields, and (if present) the line terminator characters that ended the record. CRLF counts as 2 characters; quotes and doubled quotes each count individually. If normalized newline handling (NormalizeNewlinesInFields) is later applied, it does not retroactively affect the length used for this check.

Behavior & Order of Evaluation:
1. Parsing collects fields for a record.
2. When a record boundary is reached (newline or EOF), the parser invokes guard rail checks BEFORE yielding the string[] to higher-level mapping & schema logic.
3. If a limit is exceeded:
   - A ReaderError is produced with:
     errorType: CsvLimitExceeded
     message: (e.g.) Row has 312 columns (limit 256). OR Raw record length 51342 exceeds limit 32768.
     excerpt: Up to the first 128 raw characters of the offending record (pre-truncation of fields; may include quotes and partial trailing data).
   - Metrics:
     RawRecordsParsed is incremented (the record was fully parsed structurally).
     RecordsEmitted is NOT incremented.
     ErrorCount increments.
     LinesRead increments (one per logical record boundary). (See Section 3.11 note on physical line counting.)
4. Application of ErrorAction:
   - Throw: enumeration stops immediately after raising InvalidDataException (no further rows).
   - Skip: the row is silently skipped after error reporting; enumeration continues.
   - Stop: the row is skipped; TerminatedEarly is set and enumeration ends gracefully (CompletedUtc remains null).

Relationship to Schema Errors:
- MaxColumnsPerRow fires BEFORE schema width validation. If both could apply (e.g., a row with vastly more columns than schema allows), only CsvLimitExceeded is emitted (the row never reaches schema comparison).
- AllowExtraFields does not bypass MaxColumnsPerRow; if the guard rail limit is stricter than the schema, the guard rail wins.
- AllowMissingTrailingFields is unrelated; it operates later when mapping fields to schema after a record passes guard rails.

Interaction with Inference:
- Guard rails apply during schema/type inference sampling. A record exceeding limits is not added to the inference sample set.
- If many initial lines exceed limits and are skipped, schema inference may have fewer samples; this can degrade type inference robustness. Adjust limits (or temporarily disable them) during phased ingestion if needed.

Excerpt Policy for Guard Rail Errors:
- The excerpt for CsvLimitExceeded is a raw 0–128 character prefix of the entire record (not the “first 8 fields” summary used by some schema errors). This raw prefix may contain partial fields or embedded quotes. (See Section 6.2 for global excerpt policies.)
- To harmonize excerpts across error types, you can customize your sink to re-tokenize if desired.

Performance Notes:
- Guard rail checks require only O(1) additional operations at record boundary.
- MaxRawRecordLength enables early discard of pathologically large lines (e.g., accidental file concatenation or binary data).
- If CaptureRawRecord is enabled, both guard rails run against the same raw accumulation; setting a very large MaxRawRecordLength while enabling capture can increase peak memory per record (due to the StringBuilder growth). Choose a defensive ceiling aligned with expected maxima.

Choosing Limits:
Examples:
- Wide but reasonable spreadsheets: MaxColumnsPerRow = 512
- Narrow operational logs: MaxColumnsPerRow = 64
- Large but bounded records (e.g., product catalogs): MaxRawRecordLength = 64_000
- Strict microservice logs: MaxRawRecordLength = 8_192

Example Configuration (Skip on violation):

```csharp
var guarded = new CsvReadOptions {
    HasHeader = true,
    MaxColumnsPerRow = 256,
    MaxRawRecordLength = 32_768,
    ErrorAction = ReaderErrorAction.Skip,
    ErrorSink = new JsonLinesFileErrorSink("csv_limit_errors.ndjson")
};

await foreach (var row in Read.Csv<MyRow>("incoming.csv", guarded))
{
    // Only rows within limits reach here
}
Console.WriteLine($"Rows Emitted={guarded.Metrics.RecordsEmitted} Errors={guarded.Metrics.ErrorCount}");
```

Strict Ingestion with Fail-Fast:

```csharp
var strictLimited = new CsvReadOptions {
    HasHeader = true,
    MaxColumnsPerRow = 200,
    MaxRawRecordLength = 20_000,
    ErrorAction = ReaderErrorAction.Throw
};

try
{
    await foreach (var r in Read.Csv<MyRow>("batch.csv", strictLimited)) { }
}
catch (InvalidDataException ex)
{
    Console.Error.WriteLine($"Ingestion aborted: {ex.Message}");
}
```

Operational Monitoring:
For high-volume ingestion you can set ErrorAction = Skip and rely on metrics to alert on spikes in CsvLimitExceeded counts:

```csharp
if (guarded.Metrics.ErrorCount > 0)
    Console.WriteLine($"Guard rail violations: {guarded.Metrics.ErrorCount}");
```

Edge Cases & Notes:
- A record exactly equal to the limit (columns == MaxColumnsPerRow) passes; only strictly greater triggers the error.
- A record whose raw length equals MaxRawRecordLength passes; only lengths strictly greater trigger the error.
- If both limits would be exceeded, MaxColumnsPerRow check occurs first (order in current implementation), but only one CsvLimitExceeded error is emitted per record.
- Progress events may still occur after skipped guard-rail records (progress is not suppressed by skipped rows).
- If your pipeline depends on precise physical line tallies for compliance and you have embedded newlines inside quoted fields, review Section 3.11 (Line Ending Fidelity) for the current interpretation of LinesRead.
  
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
  - If `RequireArrayRoot` is `true` AND `AllowSingleObject` is `false` -> `JsonRootError`.
  - Otherwise, the single object is processed as one logical record.
- **Metrics note**: For a valid single non-array root RawRecordsParsed becomes 1 only after successful (or skipped) processing. If a JsonRootError occurs (disallowed single root) RawRecordsParsed remains 0.

### 4.3. Fast Path vs. Validation Path

- **Fast Path** (default): Uses `JsonSerializer.Deserialize<T>(ref reader)` for direct, high-throughput streaming. The fast path is also disabled when `GuardRailsEnabled = true OR MaxStringLength > 0`, even if `ValidateElements` is false.
- **Validation Path** (if `ValidateElements` is true): Each element is parsed into a `JsonDocument` to be validated by `ElementValidator` before deserialization. This path has higher overhead.

### 4.4. Progress Percentage

The JSON reader is the only one that currently reports `Percentage` (when the underlying stream is seekable so total length and position are known). For non-seekable streams `Percentage` is `null`. (Future enhancements may add heuristic percentages for other formats; treat absence of a value as “unknown”.)

Single Root (Non-Array) Clarification:
- Fast path: Percentage can update after deserialization of the single value (may appear as a jump).
- Validation / guard-rail path: The entire file is read in a single pass (`ProcessSingleRootValidationFromStream`). No intermediate progress callbacks are emitted; expect an initial (optional) and final (100%) report only.

### 4.5. ElementValidator Usage Example

```csharp
var opts = new JsonReadOptions<MyItem> {
    ValidateElements = true,
    ElementValidator = e => e.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number,
    ErrorAction = ReaderErrorAction.Skip
};
```

---
### 4.6 JSON Guard Rails and Limits
- **MaxElements** (default 0 = unlimited): Maximum number of top-level elements (array items or single root value). When an array’s element index would exceed this value a `JsonSizeLimit` error is raised and reading terminates or continues per `ErrorAction`. The violating element is NOT counted in `RawRecordsParsed`.
- **MaxElementBytes** (default 0 = unlimited; validation path only): Caps the byte size of a single element. Measured as the difference in Utf8JsonReader.BytesConsumed before and after parsing the element (i.e., the exact number of UTF‑8 bytes consumed for that JSON value, excluding the trailing comma or closing bracket but including interior whitespace and all structural tokens). Violation → `JsonSizeLimit`.
- **MaxStringLength** (default 0 = unlimited): Maximum length of any string value anywhere inside an element. A single over-length string triggers `JsonSizeLimit`. This option forces the validation path (fast path disabled) because recursive traversal is required.
- **GuardRailsEnabled** (default false): Forces validation path even if no validator is set, enabling string length enforcement or future guard rails. Fast path is disabled if ANY of: (`ValidateElements && ElementValidator != null`) OR `GuardRailsEnabled` OR `MaxStringLength > 0`. `JsonSizeLimit` error triggers: `element count exceeded`, `element byte size exceeded`, or `string length exceeded`.

Implementation Note on MaxElementBytes:
- Enforcement occurs only on the validation path (ParseValue + JsonDocument). The measured size excludes the delimiter (comma) and any following whitespace outside the element.
- Large elements on the fast path are not currently size-checked. A future enhancement could: (a) probe element completeness with TrySkip, (b) compute size from start/end BytesConsumed, and (c) enforce MaxElementBytes without forcing full materialization. If adopted, documentation will be updated to reflect support on both paths.

## 5. YAML (`YamlReadOptions<T>`)

### 5.1. YAML Fields & Defaults

- **`RestrictTypes`**: `true` (Enforces a type whitelist).
- **`AllowedTypes`**: `null` (If `null` while `RestrictTypes` is true, only type `T` is allowed).
- **`DisallowAliases`**: `true` (Disallows both alias references and anchor definitions; violations emit `YamlSecurityError`).
- **`DisallowCustomTags`**: `true` (Enforced by SecurityFilteringParser; non-core tags produce YamlSecurityError).
- **`MaxDepth`**: `64` (Enforced; exceeding depth triggers YamlSecurityError).
- **`MaxTotalDocuments`**: 0 (no limit) – Each document (multi-doc mode) or top-level sequence element (sequence root mode) counts toward this limit. Enforced by `SecurityFilteringParser`.
- **`MaxNodeScalarLength`**: 0 (no limit) – Maximum allowed length of any scalar node’s value. Violations raise `YamlSecurityError` (excerpt contains `Len=<actual> Max=<limit>`).


### 5.2. Structural Mode Detection

The reader automatically detects the YAML structure:

- If the root is a sequence (`[...]` or a multi-line list), it iterates each item.
- Otherwise, it falls back to multi-document mode, where each document (`--- ...`) is a record.

### 5.3. Type Restriction Logic

If `RestrictTypes` is `true`:

- If `AllowedTypes` is `null`, only objects of the exact type `T` are permitted (subclasses are rejected).
- If `AllowedTypes` is provided, only types in the set are permitted.
- A rejected type triggers a `TypeRestriction` error.

### 5.4. Security Hardening

The YAML reader is hardened by default against common YAML abuse patterns (entity expansion, deeply nested structures, oversized scalars, tag-based exploits). Protection is implemented by a streaming `SecurityFilteringParser<T>` that inspects events before deserialization and enforces guard rails without buffering the whole file.

All listed guard rails are enforced in the streaming pre‑deserialization stage without whole‑file buffering.

Key security features (all active when their option is non‑zero/true):

- `DisallowAliases` (default `true`): Blocks both alias references (*alias) and anchor definitions (&name). Violations raise `YamlSecurityError`; excerpt = alias or anchor name.
- `DisallowCustomTags` (default `true`): Rejects any node whose tag is not part of a core whitelist (standard YAML 1.2 scalar/collection tags). Violation → `YamlSecurityError`; excerpt = tag value.
- `MaxDepth` (default `64`): Limits nesting depth of sequences + mappings. On exceeding the limit the offending container is skipped; error excerpt = Depth=<current> Max=<limit>.
- `MaxTotalDocuments`:  Counts each top‑level document in multi‑document mode, or each top‑level element when the root is a sequence. Once the next count would exceed the limit a `YamlSecurityError` is emitted; `excerpt = MaxTotalDocuments=<limit>`. The offending document/element is skipped.
- `MaxNodeScalarLength` (default 0 = unlimited): Caps the character length of any scalar node’s value. Oversized scalars are skipped; excerpt = Len=<actual> Max=<limit>
- Scalar / Container Skipping Behavior: For violations that occur at the start of a container (sequence or mapping), the entire container subtree is skipped to prevent partial injection of malformed or malicious structure.
  
#### Error Model:

- All guard rail violations produce `errorType = YamlSecurityError`.
- Excerpt patterns:
  - Alias / Anchor: the alias or anchor identifier.
  - Custom Tag: the tag string (e.g., !Foo or tag:example.com,2020:Foo).
  - Depth: `Depth=<current> Max=<limit>`.
  - Document / Element Count: `MaxTotalDocuments=<limit>`.
  - Scalar Length: `Len=<actual> Max=<limit>`.
  
#### Result Handling:

- Whether processing continues depends on ErrorAction (Throw | Skip | Stop).
- Skipped offending nodes do not yield deserialized objects and do not increment `RecordsEmitted`; `RawRecordsParsed` reflects only fully processed (attempted) logical records.

### 5.5. Error Handling

Deserialization exceptions are handled per document/item. On `Skip`, the reader attempts to consume events until the next `DocumentEnd` to re-synchronize.

The `excerpt` field in error records for YAML has specific behavior:
- For general `YamlException` errors (e.g., malformed syntax), the excerpt is typically empty.
- For `YamlSecurityError` violations (e.g., disallowed alias, custom tag), the excerpt contains a short, non-truncated detail string, such as the name of the disallowed anchor or tag.

### 5.6. Progress & Metrics

- `RecordsEmitted` increments per successfully emitted item. This is the value reported as `RecordsRead` in `ReaderProgress` events.
- `RawRecordsParsed` increments for each document or sequence item processed, including those that are later skipped.
- `LinesRead` is not updated (remains `0`).
- `Percentage` is always `null`.

### 5.7. Example Hardened Configuration

For maximum security when processing untrusted YAML files, explicitly configure all security-related options.

```csharp
var hardenedYaml = new YamlReadOptions<ConfigNode> {
    // Only allow deserialization into the specified type.
    RestrictTypes = true,
    AllowedTypes = new HashSet<Type> { typeof(ConfigNode) },

    // Prevent resource exhaustion and code execution attacks.
    DisallowAliases = true,
    DisallowCustomTags = true,

    // Set sensible limits to prevent resource exhaustion.
    MaxDepth = 32,
    MaxTotalDocuments = 1000,
    MaxNodeScalarLength = 1024 * 1024, // 1MB limit per scalar value

    // Handle security violations by stopping the read operation.
    ErrorAction = ReaderErrorAction.Stop,
    ErrorSink = new JsonLinesFileErrorSink("yaml_security_errors.ndjson")
};

// This read operation is now protected against common YAML vulnerabilities.
await foreach (var node in Read.Yaml<ConfigNode>("untrusted.yaml", hardenedYaml))
{
    // ...
}
```

---

## 6. Error Record & Excerpt Details

The `ReaderError` object, which is passed to the configured `IReaderErrorSink`, provides structured information about issues encountered during a read operation.

The JSON-serialized record includes the following fields:

- **`ts`**: ISO 8601 timestamp of when the error was reported.
- **`reader`**: The format being read: "CSV", "JSON", or "YAML".
- **`file`**: The file path provided in the read options.
- **`line`**: The line number where the error occurred. This is most reliable for line-based formats like CSV. For other formats, it may be `-1`.
- **`record`**: The logical record index (1-based) being processed when the error occurred. This corresponds to the `RawRecordsParsed` metric.
- **`errorType`**: A string classifying the error (e.g., `SchemaError`, `CsvQuoteError`). See Section 6.3 for common types.
- **`message`**: A human-readable description of the error.
- **`excerpt`**: A snippet of the source data related to the error. The content and truncation policy of this field vary by reader (see Section 6.1).
- **`action`**: The `ReaderErrorAction` that was taken in response to the error (`Skip`, `Stop`, or `Throw`).

### 6.1 ReaderError Property Name Mapping

Internally the ReaderError object uses CLR property names shown below. When serialized by built-in sinks (e.g., JsonLinesFileErrorSink) they appear with the JSON field names already documented.

| In-memory (CLR) | Serialized JSON |
| --- | --- |
| TimestampUtc | ts |
| Reader | reader |
| FilePath | file |
| LineNumber | line |
| RecordNumber | record |
| ErrorType | errorType |
| Message | message |
| RawExcerpt | excerpt |
| Action | action |

If you build a custom sink that serializes manually, you may choose either the CLR names or align to the canonical JSON names above for consistency.

### 6.2 Excerpt Generation Policy

The excerpt field provides a concise, source‑derived snippet to help operators and tooling quickly diagnose errors without re‑opening the full data file. Excerpts are intentionally short, never contain stack traces, and are safe to log verbatim (subject to your own data governance constraints).

This revision clarifies (a) how excerpts are produced per format and error type, (b) the difference between “raw prefix” vs. “field summary” styles in CSV, and (c) truncation rules. It also lists planned adjustments should you choose to unify behavior.

#### 6.2.1 Quick Reference Table

| Format | Error Type / Category | Excerpt Style | Source Basis | Truncation (current) | Notes |
|--------|-----------------------|---------------|--------------|----------------------|-------|
| CSV | SchemaError | Field summary | First N (currently 8) parsed fields, post‑quote normalization | No further char truncation (only field count) | Extra or missing field situations; joined with commas. |
| CSV | CsvQuoteError | Raw prefix | Raw record text as accumulated (including quotes, separators) | 128 chars (current) | Fired on illegal quotes, trailing garbage, unterminated quotes. |
| CSV | CsvLimitExceeded | Raw prefix | Raw record text (same as above) | 128 chars (current) | Guard rails (MaxColumnsPerRow / MaxRawRecordLength). |
| CSV | Conversion / Materialization Exceptions (exType name in errorType) | Field summary | First 8 fields | No char truncation of those 8 fields | Arises during type conversion or object materialization. |
| CSV | Generic / Other Internal (rare) | Raw prefix | Raw record | 128 chars (fallback) | Safety fallback if no specific mapping rule applies. |
| JSON | JsonException / JsonRootError | Raw element | Token or root fragment | 128 chars | Raw UTF‑8 slice re‑materialized as text. |
| JSON | JsonValidationError / JsonValidationFailed | Raw element | Element’s GetRawText() | 128 chars | Provided only if element fully buffered. |
| JSON | JsonSizeLimit | Raw element (if available) else empty | Offending value (when captured) | 128 chars | Over‑limit element may sometimes have empty excerpt if length guard triggers early. |
| YAML | YamlSecurityError (aliases, tags, depth, etc.) | Detail atom | Violation detail (alias/tag/len/depth token) | No truncation | Intentionally concise (anchor/tag names, numeric spec). |
| YAML | TypeRestriction | Type name | Fully qualified runtime type or "null" | No truncation | Helps whitelist diagnostics. |
| YAML | YamlException | (Usually empty) | Parser does not always provide reliable raw slice | Empty | Avoids misleading partial fragments. |
| All | Stop / Throw final error (same types above) | As per error type | Same | Same | ErrorAction does not alter stored excerpt content. In Throw mode the emitted InvalidDataException message includes " | excerpt: {excerpt}" when available. |

#### 6.2.2 CSV Excerpt Styles

CSV uses two main styles:

1. Field Summary (Schema / Conversion Context):
   - Rationale: Shows logical interpretation aligned with schema decisions.
   - Construction: Take the first N fields (currently N = 8), post‑quote and un-escape, pre‑type conversion.
   - Presentation: Joined with commas. Fields containing commas or quotes are not re‑re‑quoted (this is a diagnostic summary, not a round‑trippable CSV fragment).

2. Raw Prefix (Structural / Guard Rail Context):
   - Rationale: When the structural integrity (quoting, raw length, column explosion) is suspect, surfacing the exact raw slice is more useful than a tokenized view.
   - Construction: Take the first up‑to 128 raw characters accumulated for the record (includes quotes, separators, doubled quotes, and any embedded newline chars already read).
   - Normalization: No trimming or whitespace normalization; if NormalizeNewlinesInFields later alters internal representation, the excerpt still shows the original raw data at error time.

Planned (optional) unification (if adopted later):
- Make truncation size consistent with JSON (128 chars).
- Introduce a configurable option CsvExcerptMode (RawPrefixAlways | FieldSummaryForSchema | UnifiedRaw) and CsvExcerptMaxChars (default 128).
- Provide ColumnIndex/ColumnName metadata to avoid needing extra fields inside excerpt for column-level conversion errors.

Until such unification is implemented, sinks should not assume a single style for all CSV error types.

#### 6.2.3 JSON Excerpts

- Always derived from the logical JSON value (object / array / primitive) that triggered or was being processed at error time.
- Truncation: Hard cap at 128 characters (configurable only by modifying code currently).
- For incomplete / partially buffered elements (fast path) excerpt may be empty if the serializer raised an exception before a stable raw slice could be reconstructed.
- Size limit violations (JsonSizeLimit) can result in empty excerpts if enforcement triggers prior to full buffering.

#### 6.2.4 YAML Excerpts

- Security violations (YamlSecurityError) produce minimal atomic details:
  - Alias / Anchor: the name (no surrounding markers added).
  - Tag violation: full tag string (e.g., !Foo, tag:example.org,2020:Foo).
  - Depth / Document / Scalar length: structured “Depth=X Max=Y”, “MaxTotalDocuments=Z”, or “Len=X Max=Y”.
- TypeRestriction: Fully qualified type name or “null”.
- General parsing (YamlException): Excerpt left empty to avoid misleading half-tokens; YAML token-based reconstruction is more complex and would risk noise over signal.

#### 6.2.5 Truncation & Length Rules

Current (implemented):
- CSV raw prefix: 128 chars (hard-coded).
- CSV field summary: Up to first 8 fields (each full; no further char truncation).
- JSON element excerpts: 128 chars.
- YAML security/type excerpts: No truncation (bounded by naturally short content).
- YAML general parse errors: Empty.

Recommended (if standardizing):
- Single default max excerpt length across all raw or element-based excerpts (e.g., 128).
- Explicit option: ReadOptions.MaxExcerptChars (0 = unlimited, with per-format safety ceilings).
- Option: CsvFieldSummaryFieldCount (default 8).

#### 6.2.6 Edge Cases & Guidance

- Multi-line CSV Fields: Raw prefix may include a newline if the quoting issue occurs after crossing a physical line boundary—this is intentional. Logging sinks should not assume single-line excerpts.
- Binary / Control Characters: Non-printable characters in CSV excerpts are emitted as-is today. If you need sanitization, perform it in your sink before persistence.
- Guard Rail Skips: If a record is rejected before schema mapping, a field summary is impossible—raw prefix is the only reliable slice.
- Redaction/Sensitivity: Excerpts may contain sensitive substrings (emails, IDs). Implement sink-side redaction if required; the core layer remains neutral.
- Consuming Tools: Do not parse structured meaning (like column counts) from the excerpt; rely on dedicated error fields (errorType, message).

### 6.3. Common Error Types

The `errorType` field helps categorize issues programmatically. While any exception name can appear here, the following are common types generated by the readers:

| Error Type                  | Reader(s) | Description                                                                                             |
| --------------------------- | --------- | ------------------------------------------------------------------------------------------------------- |
| `SchemaError`               | CSV       | The number of fields in a row does not match the schema, or a required field is missing.                |
| `CsvQuoteError`             | CSV       | A violation of quoting rules, such as an unclosed quote, a stray quote mid-field, or trailing characters after a closing quote. |
| `CsvLimitExceeded`          | CSV       | A CSV guard rail limit (MaxColumnsPerRow or MaxRawRecordLength) was exceeded; the offending row was not emitted. |
| `JsonRootError`             | JSON      | The root of the JSON document is not an array, and the configuration forbids single-object roots.       |
| `JsonException`             | JSON      | General JSON syntax / structural error.                                                                 |
| `JsonValidationError`       | JSON      | The custom `ElementValidator` threw an exception during validation.                                     |
| `JsonValidationFailed`      | JSON      | The custom `ElementValidator` returned `false` for an element.                                          |
| `JsonSizeLimit`             | JSON      | A configured resource limit was exceeded (`MaxElements`, `MaxElementBytes`, or `MaxStringLength`). See Section 4.6.|
| `YamlSecurityError`         | YAML      | A security guardrail was violated, such as use of a disallowed alias, a custom tag, or excessive depth. |
| `TypeRestriction`           | YAML      | A deserialized object's type is not in the configured `AllowedTypes` set. The excerpt field contains the fully qualified runtime type name (or "null").                               |
| `YamlException`             | YAML      | General YAML syntax or parsing error.                                                                   |

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

**CSV**:
  - Column indices are not included in error records (only line and record numbers).
  - Type inference is limited to a fixed primitive set and uses current culture Parse methods; there is no culture-override hook. Use `FieldTypeInference.Custom` for custom parsing.
  -  `MaxRawRecordLength` counts raw character length including quotes and line terminators; if you normalize newlines post-parse the measured length may appear larger than the final stored representation.
**JSON**:
- Element validation mode (`ValidateElements = true`) is slower and more memory-intensive due to per-element JsonDocument materialization.
- Percentage-based progress is only available for JSON (uses file length + stream position).
- A single non-array root processed under validation/guard-rail paths is read using a full file pass (non-streaming) to validate and deserialize.
- The simple overload’s onError delegate provides only exception context (no line/record/excerpt); use options + custom sink for structured error metadata.
**YAML**:
- LinesRead metric is not populated for YAML (remains 0).

**General**:
- `CompletedUtc` remains `null` if the read terminates early due to Stop, Throw, cancellation, or an unhandled exception.
- Simple overloads (CSV/JSON/YAML) implicitly set `ErrorAction = Skip` when an `onError` delegate is supplied; you cannot override `ErrorAction` or attach a custom sink through those overloads.
- DelegatingErrorSink wraps all reported errors in a new InvalidDataException (original stack / type discarded). Use options + custom sink to retain richer context.

---

## 9. Side-by-Side Quick Reference

| Format | Simple Overload      | Options Record       | Special Features                                                                        |
| ------ | -------------------- | -------------------- | --------------------------------------------------------------------------------------- |
| CSV    | `Read.Csv<T>(path)`  | `CsvReadOptions`     | RFC4180 fidelity, quote modes, schema & type inference, raw record capture              |
| JSON   | `Read.Json<T>(path)` | `JsonReadOptions<T>` | Streaming Utf8JsonReader, single-or-array root, element validation, percentage progress |
| YAML   | `Read.Yaml<T>(path)` | `YamlReadOptions<T>` | Auto sequence vs multi-doc detection, type restriction, streaming security hardening (depth, alias, tag control)                        |

Stream Equivalents (options-based; file overloads delegate internally):

| Format | Stream Overload Signature |
|--------|---------------------------|
| CSV  | `Read.Csv<T>(Stream stream, CsvReadOptions options, string? filePath = null, CancellationToken ct = default)` |
| JSON | `Read.Json<T>(Stream stream, JsonReadOptions<T> options, string? filePath = null, CancellationToken ct = default)` |
| YAML | `Read.Yaml<T>(Stream stream, YamlReadOptions<T> options, string? filePath = null, CancellationToken ct = default)` |
| TEXT | `Read.Text(Stream stream, CancellationToken ct = default)` / `Read.TextSync(Stream stream, CancellationToken ct = default)` |
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

var alertPipeline =
    Read.Csv<RawOrder>("orders.csv", csvOpts)
        .Select(o => new OrderEnriched(
            o.Id,
            o.Amount,
            o.Country,
            Tier: o.Amount >= 5000 ? "Platinum" :
                o.Amount >= 1000 ? "Gold" :
                o.Amount >= 250 ? "Silver" : "Standard",
            Priority: o.Priority,
            IngestedUtc: DateTime.UtcNow))
        .Cases(
            o => o.Priority,
            o => o.Tier == "Gold" || o.Tier == "Platinum",
            o => o.Country != "US"
         )
    .SelectCase(
        pri => new Alert(pri.Id, "High", "Priority flag"),
        tier => new Alert(tier.Id, "Info", "High tier loyalty"),
        export => new Alert(export.Id, "Info", "Export shipment"),
        _=> null
    )
    .Where(x => x.newItem != null)
    .AllCases();

await alertPipeline.WriteJson("alerts.json");

Console.WriteLine($"CSV Records Emitted={csvOpts.Metrics.RecordsEmitted} Errors={csvOpts.Metrics.ErrorCount} Completed={csvOpts.Metrics.CompletedUtc != null}");
```

---

### Example 2: JSON Stream → Validation → Side-Effects → Write CSV

This pipeline validates incoming JSON events, normalizes them, performs console actions for high-priority events, and writes all normalized events to a CSV file.

```csharp
var jsonOpts = new JsonReadOptions<EventIn> {
    RequireArrayRoot = true,
    AllowSingleObject = true,
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
        n => n.Bucket == "Critical",
        n => n.Bucket == "High"
    )
    .ForEachCase(
        critical => Console.WriteLine($"CRIT {critical.Source}:{critical.Type} Sev={critical.Severity}"),
        high => Console.WriteLine($"HIGH  {high.Source}:{high.Type} Sev={high.Severity}"),
        n => { }
    )
    .AllCases()
    .WriteCsv("events_processed.csv");

Console.WriteLine($"JSON Records Emitted={jsonOpts.Metrics.RecordsEmitted} Errors={jsonOpts.Metrics.ErrorCount}");

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
        c => c.Environment == "prod",
        c => c.Environment == "staging"
    )
    .SelectCase(
        prod => $"[PROD] {prod.Key}={prod.Value}",
        staging => $"[STAGING] {staging.Key}={staging.Value}",
        other => null
    )
    .Where(x => x != null)
    .WriteText("important_config.txt");

Console.WriteLine($"YAML Records Emitted={yamlOpts.Metrics.RecordsEmitted} Errors={yamlOpts.Metrics.ErrorCount}");
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
Console.WriteLine($"Orders: {csvOpts.Metrics.RecordsEmitted} rows, errors={csvOpts.Metrics.ErrorCount}");
Console.WriteLine($"Events: {jsonOpts.Metrics.RecordsEmitted} events, errors={jsonOpts.Metrics.ErrorCount}");
Console.WriteLine($"Configs: {yamlOpts.Metrics.RecordsEmitted} docs, errors={yamlOpts.Metrics.ErrorCount}");
```

---

## 11. Additional Example: CSV Type Inference with Preservation Flags

```csharp
var opts = new CsvReadOptions {
    HasHeader = true,
    InferSchema = true,
    SchemaInferenceMode = SchemaInferenceMode.ColumnNamesAndTypes,
    PreserveNumericStringsWithLeadingZeros = true,
    PreserveLargeIntegerStrings = true,
    FieldTypeInference = FieldTypeInferenceMode.Primitive
};

await foreach (var row in Read.Csv<dynamic>("accounts.csv", opts)) { }

Console.WriteLine("Types:");
for (int i = 0; i < opts.InferredTypes!.Length; i++)
    Console.WriteLine($"{opts.Schema![i]} -> {opts.InferredTypes[i]}");
```

---

## 12. Auditing & Compliance Pattern

```csharp
var audit = new CsvReadOptions {
    HasHeader = true,
    RawRecordObserver = (n, raw) => RawRecordStore.Enqueue(new RawAuditRow(n, raw))
};
await foreach (var r in Read.Csv<MyRow>("inbound.csv", audit)) { }
```
