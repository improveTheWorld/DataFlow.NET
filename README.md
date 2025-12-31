# DataFlow.NET

> **We make Data fit for C#.**

From local files to cloud scale — **LINQ all the way down**.  
Let IntelliSense and the compiler do the work.

```diff
- df.filter(pl.col("ammount") > 1000)   # Typo? Runtime error.
+ .Where(o => o.Amount > 1000)          // Typo? Won't compile. ✓
```

[![License](https://img.shields.io/badge/License-Apache%202.0-green)](LICENSE) 
[![Coverage](https://img.shields.io/badge/Core%20Coverage-77%25-brightgreen)](docs/COVERAGE.md)

---

## Table of Contents

1. [Sound Familiar?](#1-sound-familiar)
2. [Three Simple Rules](#2-three-simple-rules)
3. [Everything is a Stream](#3-everything-is-a-stream)
4. [Quick Start](#4-quick-start)
5. [Documentation](#5-documentation)
6. [Community & Support](#6-community--support)

---

## 1. Sound Familiar?

.NET developers know the story — You write a clean, type-safe data processor in C# — It works perfectly on your dev machine — Then reality hits:

1.  **The Data Grows**: 
    - **10 MB**: `List<T>` works fine.
    - **10 GB**: `OutOfMemoryException`. You rewrite using `StreamReader`.
    - **10 TB**: You abandon C# for Spark/SQL. You lose type safety and duplicate logic.

2.  **The Logic Tangles**:
    - New requirements mean new `if/else` branches.
    - You loop over the same data 5 times to handle 5 different cases.
    - The code becomes spaghetti, and the data lifecycle becomes a black box.

3.  **The Source Fragments**:
    - Today it's a CSV file. Tomorrow it's a REST API. Next week it's a Kafka Stream.
    - For each source, you write different adapter code.
    - You end up with a **"Code Salad"**: mixed abstractions, different error handling, and no reuse.

**DataFlow.NET was built to stop this cycle:**

- ✅ **Unified API** — Same code for CSV, JSON, Kafka, Spark
- ✅ **Constant memory** — Stream billions of rows without `OutOfMemoryException` ([see benchmarks](docs/Benchmarks.md))
- ✅ **No spaghetti** — Declarative `Cases` pattern replaces nested `if/else`
- ✅ **Pure C#** — LINQ all the way down

> [!TIP]
> **Define the *what*. DataFlow.NET handles the *how*.**

--- 

## 2. 🧠 Three Simple Rules

DataFlow.NET is more than a framework — it defines a pattern to process data.

```mermaid
graph LR
    S[**S**ink] --> U[**U**nify]
    U --> P[**P**rocess]
    P --> R[**R**oute]
    R --> A[**A**pply]
    
    style S fill:#f9f,stroke:#333,stroke-width:2px
    style A fill:#bbf,stroke:#333,stroke-width:2px
```

We call this the **SUPRA** pattern — the name comes from gathering the first letter of each stage: **S**ink, **U**nify, **P**rocess, **R**oute, **A**pply.

> [!NOTE]
> The SUPRA pattern ensures memory stays constant and items flow one at a time. [Read the SUPRA-Pattern Guide →](docs/DataFlow-SUPRA-Pattern.md)

To achieve the SUPRA pattern, just follow these rules:

1. **Sink First** — Buffer and normalize at the edge, never in the middle.
2. **Flow Lazy** — Items stream one by one. Constant memory.
3. **Route Declaratively** — No more `if/else` spaghetti.

DataFlow.NET provides all the ready-to-use blocks to natively apply these rules.

---
## 3. 🚀 Everything is a Stream

DataFlow.NET provides tools to abstract the *source* of data from the *processing*. Use these to make every data source an `IAsyncEnumerable<T>` stream — the essence of the "unified API" — same LINQ operators, same processing logic, regardless of origin.

[See Integration Patterns Guide →](docs/Integration-Patterns-Guide.md)

| Source Type | Pattern | Output |
|-------------|---------|--------|
| **EF Core (SQL Server, PostgreSQL, etc.)** | `.AsAsyncEnumerable()` | `IAsyncEnumerable<T>` |
| **JSON/CSV/YAML Files** | `Read.Json<T>()` / `Read.Csv<T>()` | `IAsyncEnumerable<T>` |
| **REST APIs** | `.Poll()` + `.SelectMany()` | `IAsyncEnumerable<T>` |
| **Kafka / RabbitMQ / WebSocket** | Wrap + `.WithBoundedBuffer()` | `IAsyncEnumerable<T>` |
| **Snowflake** *(Premium)* | `Read.SnowflakeTable<T>()` | `IAsyncEnumerable<T>` |
| **Apache Spark** *(Premium)* | `SparkQueryFactory.Create<T>()` | `IAsyncEnumerable<T>` |


> [!IMPORTANT]
> Any `IAsyncEnumerable<T>` source integrates natively.

### Examples

Already using Entity Framework Core? DataFlow.NET plugs right in:

```csharp
// EF Core — Native support
await dbContext.Orders.AsAsyncEnumerable()
    .Where(o => o.Amount > 100)
    .WriteCsv("orders.csv");
```
*   ✅ EF Core handles database access
*   ✅ DataFlow.NET handles processing logic
*   ✅ Works with SQL Server, PostgreSQL, MySQL, SQLite

Need to integrate REST APIs or message queues? Use polling and buffering:

```csharp
// REST API — Poll and flatten
var orders = (() => httpClient.GetFromJsonAsync<Order[]>("/api/orders"))
    .Poll(TimeSpan.FromSeconds(5), token)
    .SelectMany(batch => batch.ToAsyncEnumerable());

// Kafka/WebSocket — Wrap in async iterator + buffer
var kafkaStream = ConsumeKafka(token).WithBoundedBuffer(1024);
```



### High-Performance Streaming File Readers

DataFlow.NET provides high-performance file readers: no Reflection on the hot path; expression trees are compiled once and cached.

*   **4x faster** than standard reflection-based creation ([benchmark results →](docs/Benchmarks.md))
*   **Zero allocation overhead** — same 48 bytes as native `new()` instantiation
*   Handles CSV, JSON, and YAML files generically.

We carefully crafted an intuitive, fully-featured readers API with advanced error handling — all while streaming row-by-row.

> [!TIP]
> The streaming row-by-row approach — absent in most other frameworks — is the cornerstone of DataFlow.NET's constant memory usage.

### LINQ Extensions

DataFlow.NET implements additional LINQ extensions to make every data loop composable—even side-effect loops.

- **Independent implementation** — Re-implemented `IAsyncEnumerable` methods without depending on `System.Linq.Async`
- **Clear terminal vs non-terminal separation** — Terminal methods (`Do()`, `Display()`) force execution; non-terminal methods (`ForEach()`, `Select()`, `Where()`) stay lazy

[See Extension Methods API Reference →](docs/Extension-Methods-API-Reference.md)

### Cases/SelectCase/ForEachCase

We've extended standard LINQ with custom operators for declarative branching. Using `Cases`, `SelectCase`, and `ForEachCase`, you can replace complex nested `if/else` blocks with an optimized, single-pass dispatch tree — while remaining fully composable.

[See Cases Pattern Guide →](docs/Cases-Pattern.md)

### Multi-Source Stream Merging
This is the "U" (Unify) step of the SUPRA pattern — "absorb many sources into one stream."

```csharp
var unifiedStream = new UnifiedStream<Log>()
    .Unify(fileLogs, "archive")
    .Unify(apiLogs, "live")
    .Unify(dbLogs, "backup");
// Result: A single IAsyncEnumerable<Log> you can query
```

[See Stream Merging Guide →](docs/Stream-Merging.md)
### Debug with Spy()
Insert observation points anywhere in your pipeline without changing data flow. Because `Spy()` is fully composable, you can add or remove traces by simply commenting a line — no code rewriting required.

```csharp
await data
    .Where(...)
    .Spy("After filtering")       // 👈 See items flow through
    .Select(...)
    .Spy("After transformation")
    .ForEach(...)                 // 👈 Side-effect iteration, still composable
    .Do();                        // 👈 Force execution (no output needed)
```

> ⚠️ **Note:** Due to lazy execution, output from multiple `Spy()` calls appears interleaved 
> (item-by-item), not grouped by stage. This preserves the streaming nature of the pipeline.

### Scale to the cloud *(Premium)*
If you hit the limit of local computing power, DataFlow.NET lets you **seamlessly** scale to the cloud with **LINQ-to-Spark & Snowflake**.
Your C# lambda expressions are decompiled at runtime and translated into **native Spark/SQL execution plans**.
*   ✅ No data transfer to client
*   ✅ Execution happens on the cluster
*   ✅ Full type safety

[LINQ-to-Spark Guide →](docs/LINQ-to-Spark.md) | [LINQ-to-Snowflake Guide →](docs/LINQ-to-Snowflake.md)


---

## 4. ⚡ Quick Start

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later

### Installation
```bash
git clone https://github.com/improveTheWorld/DataFlow.NET
cd DataFlow.NET
```

### Run the Usage Examples
```bash
dotnet run --project DataFlow.Test.UsageExamples/DataFlow.App.UsageExamples.csproj
```

Or open the full solution in Visual Studio 2022:
```
DataFlow.Net.sln
```

### Your First Pipeline
```csharp
using DataFlow.Data;
using DataFlow.Extensions;

// A complete, memory-efficient pipeline in 10 lines
await Read.Csv<Order>("orders.csv")
    .Cases(
        o => o.Amount > 1000, 
        o => o.CustomerType == "VIP"
    )
    .SelectCase(
        highValue => ProcessHighValue(highValue),
        vip => ProcessVip(vip)
    )
    .AllCases()
    .WriteJson("output.json");
```

### Advanced: One Logic, Multiple Targets

Your business rule is: *"Flag high-value transactions from international customers."*

```csharp
// 1. DEVELOPMENT: Read from a local CSV file
await Read.Csv<Order>("orders.csv")
    .Cases(o => o.Amount > 10000, o => o.IsInternational) // 👈 Your Logic
    .SelectCase(...) 
    .AllCases()
    .WriteCsv("output.csv");

// 2. PRODUCTION: Merge multiple async streams
await new UnifiedStream<Order>()
    .Unify(ordersApi, "api")
    .Unify(ordersDb, "db")
    .Cases(o => o.Amount > 10000, o => o.IsInternational) // 👈 SAME Logic
    .SelectCase(...)
    .AllCases()
    .WriteJson("output.json");

// 3. CLOUD: Query Snowflake Data Warehouse
// Filters and aggregations execute on the server
await Read.SnowflakeTable<Order>(options, "orders")
    .Where(o => o.Year == 2024)
    .Cases(o => o.Amount > 10000, o => o.IsInternational) // 👈 SAME Logic
    .SelectCase(...)
    .ToListAsync();

// 4. SCALE: Run on Apache Spark (Petabyte Scale)
// Translates your C# Expression Tree to native Spark orchestration
SparkQueryFactory.Create<Order>(spark, ordersDf)
    .Where(o => o.Amount > 10000)
    .Cases(o => o.Amount > 50000, o => o.IsInternational) // 👈 SAME Logic
    .SelectCase(...)
    .AllCases()
    .Write().Parquet("s3://data/output");
```

---

## 5. 📚 Documentation

| Topic | Description |
|-------|-------------|
| 🏰 **[SUPRA Pattern](docs/DataFlow-SUPRA-Pattern.md)** | The SUPRA Pattern deep dive |
| 🔀 **[Cases Pattern](docs/Cases-Pattern.md)** | The Cases/SelectCase/ForEachCase Engine |
| 📖 **[Data Reading](docs/DataFlow-Data-Reading-Infrastructure.md)** | Reading CSV, JSON, YAML, Text |
| ✍️ **[Data Writing](docs/DataFlow-Data-Writing-Infrastructure.md)** | Writing CSV, JSON, YAML, Text |
| 🌊 **[Stream Merging](docs/Stream-Merging.md)** | UnifiedStream & Multi-Source Streams |
| 🔄 **[Polling & Buffering](docs/Polling-Buffering.md)** | Data acquisition patterns |
| 🔥 **[Big Data](docs/LINQ-to-Spark.md)** | Running C# on Apache Spark |
| ❄️ **[Snowflake](docs/LINQ-to-Snowflake.md)** | LINQ-to-Snowflake Provider |
| 🚀 **[Performance](docs/ObjectMaterializer.md)** | The Zero-Allocation Engine |
| 📋 **[API Reference](docs/API-Reference.md)** | Complete API Documentation |
| 🧩 **[Extension Methods](docs/Extension-Methods-API-Reference.md)** | IEnumerable/IAsyncEnumerable/Parallel API Matrix |
| 🔌 **[Integration Patterns](docs/Integration-Patterns-Guide.md)** | HTTP, Kafka, EF Core, WebSocket examples |
| ⚡ **[ParallelAsyncQuery](docs/ParallelAsyncQuery-API-Reference.md)** | Parallel async processing API |
| 🧪 **[Test Coverage](docs/COVERAGE.md)** | Coverage Reports (~77% Core) |
| 🗺️ **[Roadmap](docs/Roadmap.md)** | Future Enterprise Connectors |

---

## 6. Community & Support

*   **Issues**: [GitHub Issues](https://github.com/improveTheWorld/DataFlow.NET/issues)
*   **Discord**: [Join the Community](https://discord.gg/placeholder)
*   **Email**: [tecnet.paris@gmail.com](mailto:tecnet.paris@gmail.com)

**DataFlow.NET** — *Sink the chaos. Let the rest flow pure.* 🚀
