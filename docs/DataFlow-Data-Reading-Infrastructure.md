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

Auto-defaults: `HasHeader = true` if no schema is passed. Errors throw by default.

```csharp
// Async
var rows = Read.Csv<MyRow>("data.csv");

// Sync
var rowsSync = Read.CsvSync<MyRow>("data.csv");

// Provide a schema for a header-less file
var rows2 = Read.Csv<MyRow>("data_no_header.csv",
    new CsvReadOptions { HasHeader = false, Schema = new[] { "Id", "Name", "Price" } });

// Handle errors by skipping instead of throwing
var rows3 = Read.Csv<MyRow>("maybe_dirty.csv",
    new CsvReadOptions {
        ErrorAction = ReaderErrorAction.Skip,
        ErrorSink = new DelegatingErrorSink(e => Console.WriteLine($"{e.line}:{e.message}"))
    });
```

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
    // rec fields now strongly typed where inferred (int, decimal, bool, etc.)
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
    TrimWhitespace = false, // default now false
    QuoteMode = CsvQuoteMode.RfcStrict,
    ErrorAction = ReaderErrorAction.Skip,
    ErrorSink = new JsonLinesFileErrorSink("csv_errors.ndjson"),
    Progress = new Progress<ReaderProgress>(p => Console.WriteLine($"Read {p.RecordsRead} recs"))
};

await foreach (var rec in Read.Csv<MyRow>("data.csv", options))
{
    // process
}
```

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

await foreach (var d in Read.Json<MyDoc>("data.json", jsonOpts)) { /* ... */ }
```

### 0.8 Simple YAML

Auto mode: detects sequence root OR multi-document format.

```csharp
await foreach (var obj in Read.Yaml<MyType>("file.yaml")) { /* ... */ }
```

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

- LinesRead
- RecordsRead
- ErrorCount
- LastLineNumber (legacy; not maintained)
- TerminatedEarly
- TerminationErrorMessage
- StartedUtc / CompletedUtc (CompletedUtc only on normal completion)

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

(unchanged; see original content—retained above in previous version)

### 2.5 Upgrading Legacy `onError` Delegates

- null delegate => Throw + NullErrorSink
- provided delegate => Skip + DelegatingErrorSink

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
- Warnings: Anomalies in inference may emit `CsvSchemaInferenceWarning` (governed by `ErrorAction`). (Ensure sinks expect warnings as ordinary errors with that `errorType`.)

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
   - Runtime conversion is strict; on first conversion failure for a finalized column type, that column is permanently demoted to `string` and subsequent rows use raw strings.
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

- CsvQuoteError: mid-field stray quote, trailing garbage after closing quote, unterminated quoted field.
- CsvSchemaInferenceWarning: anomalies during type or width inference (e.g., contradictory rows).
- SchemaError: column count mismatches or missing required fields.

### 3.10 Field Mapping Pipeline

Order in row processing:

1. Raw parsing (respect quotes, line endings)
2. Optional trim (if TrimWhitespace = true)
3. Schema width adjustment (missing vs. extra fields)
4. Type conversion using `ConvertFieldValue` (inference-aware)
5. Object materialization (`NEW.GetNew<T>`)

### 3.11 Progress & Metrics

- LinesRead increments with each completed physical line delimiter (CR, LF, or CRLF).
- RecordsRead increments after each successfully emitted logical record (post-mapping).
- Percentage not computed (file length not consulted).
- Raw record capture does not affect metrics.

---

## 4. JSON (JsonReadOptions<T>)

(Section unchanged from prior version except numbering — retained for completeness.)

### 4.1 Fields & Defaults

### 4.2 Root Handling Matrix

### 4.3 Fast Path vs. Validation Path

### 4.4 Progress Percentage

### 4.5 ElementValidator Usage Example

(See original content; behavior unchanged.)

---

## 5. YAML (YamlReadOptions<T>)

(Generally unchanged; see original content.)

### 5.1 Fields & Defaults

### 5.2 Structural Mode Detection

### 5.3 Type Restriction Logic

### 5.4 Aliases / Tags Security Note

### 5.5 Error Handling

### 5.6 Progress & Metrics

### 5.7 Example Hardened Configuration

---

## 6. Error Record & Excerpt Details

No changes except new CSV error types:

- CsvQuoteError
- CsvSchemaInferenceWarning

Excerpt truncation still ~128 chars.

---

## 7. Progress Usage Examples

(Examples remain valid; CSV inference use-cases can also attach progress.)

---

## 8. Known Limitations (Updated)

- CSV:
  - Column numbers not reported in errors.
  - Type inference limited to primitive set; no culture-specific parsing hooks (use Custom + delegate).
  - Raw record capture increases allocations (avoid for huge files unless required).
- JSON:
  - Validation path alloc-heavy.
  - Percentage only for JSON (file-length based).
- YAML:
  - DisallowAliases / DisallowCustomTags not fully enforced.
  - MaxDepth not enforced.
- General:
  - Error sink failures swallowed.
  - CompletedUtc null on Stop / exception / cancellation.
  - No global StringMapper reconfiguration API (intentional).

---

## 9. Side-by-Side Quick Reference

| Format | Simple Overload      | Options Record       | Special Features                                                                        |
| ------ | -------------------- | -------------------- | --------------------------------------------------------------------------------------- |
| CSV    | `Read.Csv<T>(path)`  | `CsvReadOptions`     | RFC4180 fidelity, quote modes, schema & type inference, raw record capture              |
| JSON   | `Read.Json<T>(path)` | `JsonReadOptions<T>` | Streaming Utf8JsonReader, single-or-array root, element validation, percentage progress |
| YAML   | `Read.Yaml<T>(path)` | `YamlReadOptions<T>` | Auto sequence vs multi-doc detection, type restriction whitelist                        |

---

## 

10. Full Integration Examples (Pipeline Style)

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


