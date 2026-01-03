# DataFlow.Data.Write - Writing Infrastructure

> **Version:** V1.0  
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

## Supported Formats

| Format | Sync | Async IEnumerable | Async IAsyncEnumerable | Stream |
|--------|------|-------------------|------------------------|--------|
| **CSV** | ✅ | ✅ | ✅ | ✅ |
| **JSON** | ✅ | ✅ | ✅ | ✅ |
| **YAML** | ✅ | ✅ | ✅ | ✅ |
| **Text** | ✅ | ✅ | ✅ | ✅ |

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

### CSV Writer

```csharp
// File path - simple
await records.WriteCsv("file.csv");
await records.WriteCsv("file.csv", withHeader: false, separator: ";");
records.WriteCsvSync("file.csv");

// File path - with options
await asyncRecords.WriteCsv("file.csv", options);

// Stream
await records.WriteCsv(stream, options);
await asyncRecords.WriteCsv(stream, options);
```

### JSON Writer

```csharp
// File path
await items.WriteJson("file.json");
await asyncItems.WriteJson("file.json");
items.WriteJsonSync("file.json");

// Stream
await items.WriteJson(stream, options);
await asyncItems.WriteJson(stream, options);
```

### YAML Writer

```csharp
// File path - single document
await items.WriteYaml("file.yaml");
items.WriteYamlSync("file.yaml");

// File path - multi-document batching
await asyncItems.WriteYamlBatched("file.yaml", batchSize: 1000);

// Stream
await asyncItems.WriteYaml(stream, options);
```

### Text Writer

```csharp
// File path
await lines.WriteText("file.txt");
lines.WriteTextSync("file.txt");

// Stream
await lines.WriteText(stream, options);
```

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

## See Also

- [DataFlow-Data-Reading-Infrastructure.md](DataFlow-Data-Reading-Infrastructure.md) - Reading APIs
- [Architecture-APIs.md](Architecture-APIs.md) - Overall architecture
- [Roadmap.md](Roadmap.md) - Future enhancements (V1.1: ErrorManager)
