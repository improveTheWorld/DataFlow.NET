# DataFlow.Data Reading Infrastructure

This document provides a deep-dive into the reading infrastructure of the `DataFlow.Data` layer, covering configuration, error handling, and format-specific options.

## 0. Fast Usage Overview

### Asynchrony Convention (IMPORTANT)

Default method names are ASYNCHRONOUS. Synchronous variants use the `Sync` suffix.

- Async: `Read.Csv<T>()` returns `IAsyncEnumerable<T>`
- Sync: `Read.CsvSync<T>()` returns `IEnumerable<T>`

### 0.1 Read Raw Text Lines

```csharp
// Async
IAsyncEnumerable<string> lines = Read.Text("file.txt");

// Sync
IEnumerable<string> linesSync = Read.TextSync("file.txt");
```

### 0.2 Simple CSV (Default RFC-leaning behavior)

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
 
### 0.3 CSV With Schema & Type Inference

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

### 0.4 CSV Capturing Raw Records (Auditing)

```csharp
var auditOpts = new CsvReadOptions {
    HasHeader = true,
    CaptureRawRecord = true,
    RawRecordObserver = (n, raw) => AuditLog.WriteLine($"{n}:{raw}")
};
await foreach (var r in Read.Csv<MyRow>("audited.csv", auditOpts)) { }
```

### 0.5 Full CSV with Options (Strict ingestion)

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

### 0.6 Simple JSON

Defaults: `RequireArrayRoot = true`, `AllowSingleObject = true`

```csharp
await foreach (var item in Read.Json<MyDoc>("data.json")) { /* ... */ }
```

### 0.7 JSON with Validation / Progress / Single Object Handling

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

### 0.8 Simple YAML

```csharp
await foreach (var obj in Read.Yaml<MyType>("file.yaml")) { /* ... */ }
```

Note: The simple YAML overload accepts an optional custom IDeserializer argument; in the current implementation this parameter is not applied (a default deserializer is constructed internally). This is a forward-compatibility placeholder—use the options-based overload if you need guaranteed custom behavior.

### 0.9 YAML with Type Restrictions

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

### 1.5 HandleError Workflow

1. Increment ErrorCount
2. Produce `ReaderError` -> `ErrorSink.Report`
3. Apply action logic (Throw / Stop / Skip)
4. Return boolean controlling loop continuation

### 1.6 Early Termination & Finalization

- Normal completion -> `Complete()` sets `CompletedUtc`
- Stop / exception / cancellation -> `CompletedUtc` remains null

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
- Warnings: Anomalies in inference may emit `CsvSchemaInferenceWarning` (governed by `ErrorAction`). **Note: This warning is planned and not yet implemented.** Sinks should be designed to handle it as an ordinary error with that `errorType` in the future.

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
   - Runtime conversion is strict; on first conversion failure for a finalized column type, that column is permanently demoted to `string` and subsequent rows use raw strings. (Implemented in the field conversion pipeline invoked by ConvertFieldValue.)
   - Casting order: direct parse to the inferred type; no fallback chain except demotion-to-string.

Custom Conversion:

- When `FieldTypeInference = Custom`, `FieldValueConverter(string raw)` is used for EVERY field (bypass primitive chain). Return any object (including leaving as string).

Fallback Behavior:

- If no candidate types survive sampling for a column, it defaults to `string`.

### 3.6 Raw Record Capture & Auditing

- `CaptureRawRecord` (bool): If true, original record text (as read, including separators and original line endings if preserved) is captured per logical record.
- `RawRecordObserver` `(recordNumber, rawLine)` delegate: Observes each raw record (useful for auditing, lineage, compliance).
- Raw capture occurs after unescaping doubled quotes but before trimming (since TrimWhitespace default is false).
- Large files: prefer `RawRecordObserver` streaming rather than setting `CaptureRawRecord` just to gather the data—observer avoids extra retention.

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
- `CsvSchemaInferenceWarning` (Planned)
- `CsvLimitExceeded`: A configured guard rail (MaxColumnsPerRow or MaxRawRecordLength) was exceeded. Row skipped or ingestion terminated per ErrorAction.

See **Section 6.2 Common Error Types** for detailed descriptions.

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
- Percentage not computed (file length not consulted).
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

- **Fast Path** (default): Uses `JsonSerializer.Deserialize<T>(ref reader)` for direct, high-throughput streaming. The fast path is also disabled when `GuardRailsEnabled = true or MaxStringLength > 0`, even if `ValidateElements` is false.
- **Validation Path** (if `ValidateElements` is true): Each element is parsed into a `JsonDocument` to be validated by `ElementValidator` before deserialization. This path has higher overhead.

### 4.4. Progress Percentage

The JSON reader is the only one that currently reports `Percentage` because it can access the total file size and current stream position.(Future enhancement may add heuristic percentages for other formats; treat absence of a value as “unknown”.)

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
- **MaxElementBytes** (default 0 = unlimited; validation path only): Caps the byte size of a single element. Measured as the UTF-8 length of the full JSON value after buffering. Violation → `JsonSizeLimit`.
- **MaxStringLength** (default 0 = unlimited): Maximum length of any string value anywhere inside an element. A single over-length string triggers `JsonSizeLimit`. This option forces the validation path (fast path disabled) because recursive traversal is required.
- **GuardRailsEnabled** (default false): Forces validation path even if no validator is set, enabling string length enforcement or future guard rails. Fast path is disabled if ANY of: (`ValidateElements && ElementValidator != null`) OR `GuardRailsEnabled` OR `MaxStringLength > 0`. `JsonSizeLimit` error triggers: `element count exceeded`, `element byte size exceeded`, or `string length exceeded`.

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
- **`errorType`**: A string classifying the error (e.g., `SchemaError`, `CsvQuoteError`). See Section 6.2 for common types.
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

### 6.2. Excerpt Generation Policy

The `excerpt` field's content depends on the data format being read:

- **CSV**:Typically the first 8 fields joined by commas. Some early structural errors (e.g., missing schema) may include the entire row instead of truncating to 8 fields.
- **JSON**: The excerpt is the raw text of the JSON element that caused the error, explicitly truncated to a maximum of 128 characters.
- **YAML**: Behavior varies. For general parsing errors (`YamlException`), the excerpt is often empty. For security violations (`YamlSecurityError`), the excerpt contains a short, non-truncated detail about the violation, such as the name of a disallowed alias or the URI of a custom tag.

### 6.3. Common Error Types

The `errorType` field helps categorize issues programmatically. While any exception name can appear here, the following are common types generated by the readers:

| Error Type                  | Reader(s) | Description                                                                                             |
| --------------------------- | --------- | ------------------------------------------------------------------------------------------------------- |
| `SchemaError`               | CSV       | The number of fields in a row does not match the schema, or a required field is missing.                |
| `CsvQuoteError`             | CSV       | A violation of quoting rules, such as an unclosed quote, a stray quote mid-field, or trailing characters after a closing quote. |
| `CsvSchemaInferenceWarning` | CSV       | **(Planned)** An anomaly was detected during schema inference. This error is not yet implemented.       |
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
  - **`CsvSchemaInferenceWarning` is not yet implemented.** .(no emission occurs today).
  - Column indices are not included in error records (only line and record numbers).
  - Type inference is limited to a fixed primitive set t and uses current culture Parse methods;there is no culture-override hook. Use `FieldTypeInference.Custom` for custom parsing.
  -  Raw record capture (`CaptureRawRecord`) increases allocations; prefer `RawRecordObserver` for streaming audit pipelines.
  -  `MaxRawRecordLength` counts raw character length including quotes and line terminators; if you normalize newlines post-parse the measured length may appear larger than the final stored representation.
**JSON**:
- Element validation mode (`ValidateElements = true`) is slower and more memory-intensive due to per-element JsonDocument materialization.
- Percentage-based progress is only available for JSON (uses file length + stream position).
- A single non-array root processed under validation/guard-rail paths is read using a full file pass (non-streaming) to validate and deserialize.
- The simple overload’s onError delegate provides only exception context (no line/record/excerpt); use options + custom sink for structured error metadata.
**YAML**:
- The simple YAML overload’s custom IDeserializer parameter is currently ignored (placeholder for future enhancement).
- LinesRead metric is not populated for YAML (remains 0).

**General**:
- `IReaderErrorSink.Report` exceptions are not caught; a sink failure can terminate the read.
- `CompletedUtc` remains `null` if the read terminates early due to Stop, Throw, cancellation, or an unhandled exception.
- Simple overloads (CSV/JSON/YAML) implicitly set `ErrorAction = Skip` when an `onError` delegate is supplied; you cannot override `ErrorAction` or attach a custom sink through those overloads.

---

## 9. Side-by-Side Quick Reference

| Format | Simple Overload      | Options Record       | Special Features                                                                        |
| ------ | -------------------- | -------------------- | --------------------------------------------------------------------------------------- |
| CSV    | `Read.Csv<T>(path)`  | `CsvReadOptions`     | RFC4180 fidelity, quote modes, schema & type inference, raw record capture              |
| JSON   | `Read.Json<T>(path)` | `JsonReadOptions<T>` | Streaming Utf8JsonReader, single-or-array root, element validation, percentage progress |
| YAML   | `Read.Yaml<T>(path)` | `YamlReadOptions<T>` | Auto sequence vs multi-doc detection, type restriction, streaming security hardening (depth, alias, tag control)                        |

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
    CaptureRawRecord = true,
    RawRecordObserver = (n, raw) => RawRecordStore.Enqueue(new RawAuditRow(n, raw))
};
await foreach (var r in Read.Csv<MyRow>("inbound.csv", audit)) { }
```
