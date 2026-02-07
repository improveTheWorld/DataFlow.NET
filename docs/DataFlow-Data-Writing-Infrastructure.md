# DataFlow.Data.Write - Writing Infrastructure

> **Version:** V1.2.1  
> **Status:** Production Ready  
> **Coverage:** 87.3%

---

## Quick Start

```csharp
using DataFlow;

// CSV - simple
await records.WriteCsv("output.csv");

// CSV - with options
await records.WriteCsv("output.csv", new CsvWriteOptions 
{ 
    Separator = ";",
    WriteHeader = false 
});

// JSON streaming
await asyncItems.WriteJson("output.json");

// YAML with batching
await items.WriteYaml("output.yaml", new YamlWriteOptions { BatchSize = 1000 });
```

---

## Unified Write API

Every format follows the **same 6-overload pattern** — no surprises, no format-specific gaps:

| # | Source | Target | Mode | Example |
|---|--------|--------|------|---------|
| 1 | `IEnumerable<T>` | file path | sync | `data.WriteCsvSync("out.csv")` |
| 2 | `IEnumerable<T>` | file path | async | `await data.WriteCsv("out.csv")` |
| 3 | `IAsyncEnumerable<T>` | file path | async | `await stream.WriteCsv("out.csv")` |
| 4 | `IEnumerable<T>` | stream | sync | `data.WriteCsvSync(memoryStream)` |
| 5 | `IEnumerable<T>` | stream | async | `await data.WriteCsv(memoryStream)` |
| 6 | `IAsyncEnumerable<T>` | stream | async | `await stream.WriteCsv(memoryStream)` |

**Design Principles:**

- **All formats support sync and async writes** — `WriteXxxSync` for blocking, `WriteXxx` for async.
- **All writers accept an optional `XxxWriteOptions?` parameter** for encoding, append mode, metrics, and format-specific settings.
- **All async overloads accept an optional `CancellationToken`** — pass it directly or via `Options.CancellationToken`.

> **4 formats × 6 overloads = 24 write methods**, all consistent.

---

## Options Architecture

### Base Options

```csharp
public record WriteOptions
{
    public Encoding Encoding { get; init; } = Encoding.UTF8;
    public bool Append { get; init; } = false;
    public CancellationToken CancellationToken { get; init; } = default;
    public WriterMetrics Metrics { get; }  // Read-only, auto-tracked
}
```

### CSV Options

```csharp
public record CsvWriteOptions : WriteOptions
{
    public string Separator { get; init; } = ",";
    public bool WriteHeader { get; init; } = true;
    public string? NewLine { get; init; } = null;  // Uses Environment.NewLine
}
```

### JSON Options

```csharp
public record JsonWriteOptions : WriteOptions
{
    public bool Indented { get; init; } = true;  // Ignored when JsonLinesFormat = true
    public JsonSerializerOptions? SerializerOptions { get; init; }
    public bool JsonLinesFormat { get; init; } = false;  // One object per line, no array
}
```

> **JSON Lines Format:** When `JsonLinesFormat = true`, outputs one JSON object per line without array wrapper. Compatible with Elasticsearch, BigQuery, and streaming processors.

### YAML Options

```csharp
public record YamlWriteOptions : WriteOptions
{
    public bool WriteEmptySequence { get; init; } = true;  // Write "[]" for empty
    public int? BatchSize { get; init; } = null;  // Multi-document batching
}
```

---

## API Reference

All four formats follow the same pattern. Here's a representative example using CSV:

```csharp
// Zero config — just works
records.WriteCsvSync("output.csv");
await records.WriteCsv("output.csv");
await asyncRecords.WriteCsv("output.csv");

// Same methods, with options when needed
await records.WriteCsv("output.csv", new CsvWriteOptions
{
    Separator = ";",
    WriteHeader = false,
    Encoding = Encoding.Latin1,
    Append = true
});

// Stream target — same pattern
records.WriteCsvSync(memoryStream);
await records.WriteCsv(memoryStream);
await asyncRecords.WriteCsv(memoryStream, new CsvWriteOptions { Separator = "\t" });
```

Replace `Csv` with `Json`, `Yaml`, or `Text` — the pattern is identical.

---

## Metrics

All write operations track metrics automatically:

```csharp
var options = new CsvWriteOptions();
await records.WriteCsv("output.csv", options);

Console.WriteLine($"Records: {options.Metrics.RecordsWritten}");
Console.WriteLine($"Started: {options.Metrics.StartedUtc}");
Console.WriteLine($"Completed: {options.Metrics.CompletedUtc}");
```

---

## CSV Quoting (RFC 4180)

Fields are automatically quoted when containing:
- The separator character
- Double quotes (escaped as `""`)
- Carriage return (`\r`)
- Line feed (`\n`)
- Leading or trailing spaces

```csharp
var record = new { Name = "Hello, World" };
record.ToCsvLine();  // Returns: "Hello, World"

var record2 = new { Quote = "Say \"Hi\"" };
record2.ToCsvLine();  // Returns: "Say ""Hi"""
```

---

## Cancellation

All write operations respect `CancellationToken`:

```csharp
var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
var options = new CsvWriteOptions { CancellationToken = cts.Token };

try
{
    await largeDataset.WriteCsv("huge.csv", options);
}
catch (OperationCanceledException)
{
    Console.WriteLine("Write cancelled");
}
```

---

## Stream Support

Write directly to any `Stream`:

```csharp
// Memory stream
using var ms = new MemoryStream();
await records.WriteCsv(ms);

// Network stream
await records.WriteJson(networkStream);

// Azure Blob (via SDK stream)
await records.WriteYaml(blobStream);
```

> **Note:** Stream overloads use `leaveOpen: true` so the stream is not disposed.

---

## Cloud Writers

Write data to Snowflake and Spark with unified API (O(1) memory):

### Snowflake

```csharp
// From SnowflakeQuery (already has context - just pass table name)
await context.Read.Table<Order>("ORDERS")
    .Where(o => o.Amount > 1000)
    .WriteTable("HIGH_VALUE_ORDERS");

await context.Read.Table<Order>("ORDERS")
    .Where(o => o.Status == "Pending")
    .MergeTable("PROCESSED_ORDERS", o => o.OrderId);

// From IEnumerable/List (needs context)
await records.WriteTable(context, "ORDERS");
await records.MergeTable(context, "ORDERS", o => o.Id).UpdateOnly("AMOUNT");
```

### Spark

```csharp
// From SparkQuery (just path)
await query.WriteParquet("/data/orders");
await query.WriteParquet("/data/orders").Overwrite();
await query.WriteCsv("/data/export.csv").WithHeader();
await query.WriteJson("/data/events.json");
await query.WriteTable("catalog.db.orders");

// From IEnumerable/List (context + path)
await records.WriteParquet(context, "/data/orders");
```

---

## See Also

- [DataFlow-Data-Reading-Infrastructure.md](DataFlow-Data-Reading-Infrastructure.md) - Reading APIs
- [LINQ-to-Snowflake.md](LINQ-to-Snowflake.md) - Snowflake Write API
- [LINQ-to-Spark.md](LINQ-to-Spark.md) - Spark Write API
- [Architecture-APIs.md](Architecture-APIs.md) - Overall architecture

