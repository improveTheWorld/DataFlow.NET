# DataFlow.NET — Architecture & APIs

## Table of Contents

1. [Project Structure](#1-project-structure)
2. [Architecture](#2-architecture)
3. [Core APIs](#3-core-apis)
4. [Advanced Topics](#4-advanced-topics)
5. [Best Practices](#5-best-practices)
6. [Going Deeper](#6-going-deeper)

---

## 1. Project Structure

The solution is organized into **solution folders** that group related projects. Each project is a minimal, single-responsibility unit to keep dependencies lean.

### Solution Folder Organization

```
DataFlow.Net.sln
│
├── 📁 DataFlow.App/                    ← Demo & Test Applications
│   ├── DataFlow.App.UsageExamples      ★ Start here - documentation examples
│   ├── DataFlow.App.Tools
│   ├── DataFlow.App.DataFlowTest
│   ├── DataFlow.App.EnumerableExtensionsTest
│   └── ...
│
├── 📁 DataFlow.Data/                   ← Data I/O Layer
│   ├── DataFlow.Data.Read              CSV, JSON, YAML, Text readers
│   └── DataFlow.Data.Write             Writers for all formats
│
├── 📁 DataFlow.Extensions/             ← Extension Methods
│   ├── DataFlow.Extensions.EnumerableExtensions     Cases, Until, ForEach
│   ├── DataFlow.Extensions.AsyncEnumerableExtensions Async variants
│   ├── DataFlow.Extensions.StringExtensions
│   ├── DataFlow.Extensions.ArrayExtensions
│   ├── DataFlow.Extensions.DictionaryExtensions
│   ├── DataFlow.Extensions.RegexTokenizerExtensions
│   ├── DataFlow.Extensions.SparkQueryExtensions
│   └── ...
│
├── 📁 DataFlow.Framework/              ← Core Infrastructure
│   ├── DataFlow.Framework.DataFlow     Pub/sub, channels
│   ├── DataFlow.Framework.Guard        Defensive programming
│   ├── DataFlow.Framework.RegexTokenizer
│   ├── DataFlow.Framework.SparkQuery   LINQ-to-Spark translation
│   ├── DataFlow.Framework.Syntaxi      Grammar parsing
│   ├── DataFlow.Framework.UnixStyleArgs
│   └── ...
│
├── 📁 DataFlow.Logger/                 ← Logging Infrastructure
│   ├── DataFlow.Logger
│   └── DataFlow.Logger.Test.UsageExamples
│
└── 📁 UnitTests/                       ← Automated Tests
    ├── DataFlow.Core.Tests             Core extensions, Cases, Materialization
    ├── DataFlow.Data.Tests             Read layer tests
    ├── DataFlow.Data.Write.Tests       Write layer tests
    └── DataFlow.SparkQuery.Tests       Spark integration tests
```

### Design Rationale

- **Single Responsibility**: Each project contains one focused capability
- **Minimal Dependencies**: Reference only what you need
- **Easy Compilation**: Smaller dependency graphs = faster builds
- **Future NuGet Packaging**: Each project can become an independent package

---

## 2. Architecture

The DataFlow.NET framework follows a **four-layer architecture**:

### 2.1 DataFlow.Data

**Unified Data Access Layer**

- File I/O operations (Read, Write) with async support
- Data format handling (CSV, Text, JSON, YAML)
- Stream-aware readers that work with both files and live data sources

### 2.2 DataFlow.Extensions

**Unified Extension Methods Layer**

- Dual `IEnumerable`/`IAsyncEnumerable` extensions
- **Cases/SelectCase/ForEachCase pattern** for both sync and async
- String processing and file system utilities

### 2.3 DataFlow.Framework

**Stream Processing Infrastructure Layer**

- **UnifiedStream<T>** for multi-source stream union/merging
- Channel-based async communication
- Regular expression utilities

### 2.4 DataFlow.SparkQuery

**Distributed Processing Layer** (Optional - requires Apache Spark)

- LINQ-to-Spark translation for petabyte-scale processing
- Expression tree translation to Spark Column operations
- Window functions, aggregations, and distributed joins

---

## 3. Core APIs

### 3.1 Read Class — Unified Data Reading

*Default method names are **ASYNCHRONOUS**. Synchronous variants use the `Sync` suffix.*

```csharp
// Text file reading
IAsyncEnumerable<string> lines = Read.Text("file.txt");
IEnumerable<string> linesSync = Read.TextSync("file.txt");

// CSV reading with type mapping
IAsyncEnumerable<T> records = Read.Csv<T>("data.csv");
IEnumerable<T> recordsSync = Read.CsvSync<T>("data.csv", separator: ";");

// JSON/YAML reading
IAsyncEnumerable<T> items = Read.Json<T>("data.json");
IAsyncEnumerable<T> docs = Read.Yaml<T>("config.yaml");
```

> [!NOTE]
> For advanced configuration, error handling, and format-specific options, see [Data Reading Infrastructure](DataFlow-Data-Reading-Infrastructure.md).

### 3.2 Writers — Unified Data Writing

```csharp
// All writers support both IEnumerable and IAsyncEnumerable
await records.WriteCsv("output.csv");
await records.WriteJson("output.json");
await records.WriteYaml("output.yaml");
await lines.WriteText("output.txt");

// Synchronous versions
records.WriteCsvSync("output.csv");
```

### 3.3 Cases/SelectCase Pattern

The core processing pattern for categorized data transformation:

```csharp
data
    .Cases(predicate1, predicate2, ...)      // Categorize items
    .SelectCase(transform1, transform2, ...) // Transform per category
    .ForEachCase(action1, action2, ...)      // Side-effects per category
    .AllCases()                              // Extract results
```

> [!NOTE]
> For full pattern documentation, see [Unified Processing](Unified-Processing.md).

### 3.4 UnifiedStream

Merge multiple async sources into a single stream:

```csharp
var merger = new UnifiedStream<T>(new UnifyOptions
{
    ErrorMode = UnifyErrorMode.ContinueOnError,
    Fairness = UnifyFairness.RoundRobin
})
.Unify(source1, "name1")
.Unify(source2, "name2", predicate: item => item.IsValid);
```

> [!NOTE]
> For full streaming documentation, see [Stream Merging](Stream-Merging.md).

---

## 4. Advanced Topics

### 4.1 Lazy Evaluation

All pipelines use lazy evaluation—nothing executes until enumeration:

```csharp
// This doesn't execute yet
var pipeline = data.Cases(...).SelectCase(...).AllCases();

// Execution happens here
var results = await pipeline.ToListAsync();

// Or streaming execution
await foreach (var item in pipeline) { }
```

### 4.2 Buffering Extensions

```csharp
// Convert sync to async with yielding
var asyncData = syncData.Async(yieldThresholdMs: 15);

// Add bounded buffer for backpressure
var buffered = source.WithBoundedBuffer(capacity: 500);
```

### 4.3 Utility Extensions

```csharp
// Conditional processing
data.Until(item => item.IsLast)   // Stop when condition met

// Debugging
data.Spy(item => Console.WriteLine(item))  // Peek at items

// Aggregation
data.Cumul((a, b) => a + b)  // Running aggregation
```

---

## 5. Best Practices

### 5.1 Reader Configuration

Configure error handling at the boundary:

```csharp
var options = new CsvReadOptions {
    ErrorAction = ReaderErrorAction.Skip,
    ErrorSink = new JsonLinesFileErrorSink("errors.ndjson"),
    Progress = new Progress<ReaderProgress>(p => Console.WriteLine($"{p.RecordsRead} rows"))
};

await Read.Csv<Order>("orders.csv", options)
    .Select(EnrichOrder)
    .Cases(...)
    .AllCases();
```

### 5.2 Testing Pipelines

```csharp
[Test]
public async Task TestProcessingLogic()
{
    var testData = new[] {
        new Order { Id = 1, Amount = 1500 },
        new Order { Id = 2, Amount = 500 }
    }.ToAsyncEnumerable();

    var results = await testData
        .Cases(o => o.Amount > 1000)
        .SelectCase(
            high => new { o.Id, Category = "High" },
            standard => new { o.Id, Category = "Standard" }
        )
        .AllCases()
        .ToListAsync();

    Assert.AreEqual("High", results.First(r => r.Id == 1).Category);
}
```

---

## 6. Going Deeper

| Document | Description |
|----------|-------------|
| [Unified Processing](Unified-Processing.md) | Cases/SelectCase pattern, Supra Category |
| [Stream Merging](Stream-Merging.md) | UnifiedStream, multi-source streams |
| [Data Reading Infrastructure](DataFlow-Data-Reading-Infrastructure.md) | CSV, JSON, YAML readers with full options |
| [LINQ-to-Spark](LINQ-to-Spark.md) | Distributed processing with SparkQuery |
| [Roadmap](Roadmap.md) | Future enhancements and versions |

*For API references, see: [DataFlow.Data](DataFlow-Data-Layer.md) · [DataFlow.Extensions](DataFlow-Extensions-Layer.md) · [DataFlow.Framework](DataFlow-Framework-Layer.md)*

---

*DataFlow.NET — Unified Data Processing for Batch and Streaming*
