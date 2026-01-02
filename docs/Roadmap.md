# DataFlow.NET Development Roadmap

> **Document Purpose:** This is the official roadmap consolidating all planned enhancements. It replaces the previous `Enhancements.md` and `Features.md` documents.

---

## V1.0 — Initial Release (Current Focus)

Features that enhance **already implemented** functionality. These are polish, DX improvements, and production-readiness features.


### 🔧 V1.0 Enhancements (Polish & DX)

| # | Enhancement | Description | Effort | Status |
|---|-------------|-------------|--------|--------|
| 1 | **XML Documentation** | Add XML comments to all public API for IntelliSense | Low | Planned |
| 2 | **Typed Categories (Enum)** | `Cases<TEnum>` overload for type-safe categories | Low | Planned |
| 3 | **Reader Security Hardening** | Path traversal prevention with `Path.GetFullPath()` | Low | Planned |
| 4 | **Promote Record Types** | Update all examples to use C# `record` types | Low | Planned |

#### Typed Categories API Example

```csharp
enum LogLevel { Error, Warn, Info }

logs.Cases(
    (LogLevel.Error, log => log.Severity == "E"),
    (LogLevel.Warn, log => log.Severity == "W")
)
.SelectCase(
    (LogLevel.Error, log => new FormattedLog(log)),
    (LogLevel.Warn, log => new FormattedLog(log))
);
```

### 📦 V1.0 Release Readiness (BLOCKING)

> [!IMPORTANT]
> These items are **critical for public release**. Without them, the framework cannot be adopted.

| # | Item | Description | Effort | Status |
|---|------|-------------|--------|--------|
| 1 | **Publish NuGet Packages** | Package and publish to nuget.org | Medium | 🔜 Planned |
| 2 | **CI/CD Pipeline** | GitHub Actions: build, test, publish on tag | Medium | 🔜 Planned |
| 3 | **Fix Nullability Warnings** | Resolve remaining `CS8600`/`CS8603` nullable warnings | Low | 🔜 Planned |
| 4 | **Real-World Demo Project** | End-to-end ETL example with multiple formats | Medium | 🔜 Planned |
| 5 | **CONTRIBUTING.md** | Contribution guidelines for open-source | Low | 🔜 Planned |
| 6 | **Community Discord** | Set up Discord server for user support | Low | 🔜 Planned |
| 7 | **README Polish** | Add NuGet badges, demo GIF, quick start | Low | 🔜 Planned |
| 8 | **GitHub Sponsors Setup** | Enable funding for project sustainability | Low | 🔜 Planned |

---

## V1.1 — Production Hardening

Features for enterprise production deployments. Focus on **resilience** and **observability**.

### 🛡️ Resilience & Error Handling

| # | Feature | Description | Effort |
|---|---------|-------------|--------|
| 1 | **Read Layer 70% Coverage** | Improve Read layer from 65% to 70% with mock infrastructure. [Plan](Read-Coverage-70-Plan.md) | Medium |
| 2 | **ErrorManager (Unified Error Handling)** | Single config for Read/Transform/Write errors. [Spec](ErrorManager-Spec.md) | Medium |
| 3 | **Pipeline Resilience Policies** | Retry, circuit breaker for `SelectCase` operations | Medium |
| 4 | **Dead Letter Queues** | Route failed items to error streams | Medium |

#### Resilience API Design

```csharp
public class ResiliencePolicyBuilder
{
    public ResiliencePolicyBuilder WithRetry(int count, TimeSpan delay) { /* ... */ }
    public ResiliencePolicyBuilder WithCircuitBreaker(int exceptionsAllowed, TimeSpan breakDuration) { /* ... */ }
    public ResiliencePolicyBuilder OnError(Action<Exception, object> handler) { /* dead-letter routing */ }
}

// Usage
var processedData = sourceStream
    .SelectCase(...)
    .WithPolicy(new ResiliencePolicyBuilder()
        .WithRetry(3, TimeSpan.FromSeconds(2))
        .OnError((ex, item) => deadLetterQueue.Add(item))
    );
```

### 📊 Observability

| # | Feature | Description | Effort |
|---|---------|-------------|--------|
| 7 | **OpenTelemetry Metrics** | `System.Diagnostics.Metrics` integration | Medium |
| 8 | **Trace Context Propagation** | Distributed tracing support | Low |

#### Metrics API Design

```csharp
// Fluent extension
var processedStream = myStream
    .WithMetrics("customer-ingestion")  // Auto-emits counters/histograms
    .Cases(...);

// Standard .NET Metrics (OpenTelemetry compatible)
DataFlowMetrics.ItemsProcessed.Add(1);
DataFlowMetrics.ProcessingDuration.Record(elapsed.TotalMilliseconds);
```

### ❄️ SnowflakeQuery Enhancements

| # | Feature | Description | Effort |
|---|---------|-------------|--------|
| 9 | **Correlated Subqueries** | Auto-translate `Any()`/`All()` on navigation properties to `EXISTS` subqueries | High |

---

## V1.2 — Performance & Scale

Focus on **throughput** and **large-scale processing**.

### ⚡ Performance

| # | Feature | Description | Impact |
|---|---------|-------------|--------|
| 9 | **Intelligent Micro-Batching** | `SelectCaseBatched()` for parallel batch processing | 3-8x throughput |
| 10 | **Object Pooling** | `Microsoft.Extensions.ObjectPool` for hot paths | 40-60% GC reduction |
| 11 | **SparkQuery Guardrails** | Safety limits on `ToList()`, best practice docs | Prevents OOM |

### 🏗️ Architecture Refactoring

| # | Feature | Description | Effort |
|---|---------|-------------|--------|
| 1 | **Refine Type Strategy Selection** | Smarter auto-detection of record vs class vs struct; user-configurable strategy hints | Medium |
| 2 | **CSV Reader Lightening** | Externalize all type conversion attempts from CSV reader to ObjectMaterializer (single responsibility). [Plan](ObjectMaterializer-String-Based-Materialization-Plan.md) | High |
| 3 | **Error Externalization** | Delegate all error management/reporting from readers to ErrorManager | Medium |

#### Micro-Batching API

```csharp
var processedStream = source
    .Cases(...)
    .SelectCaseBatched(
        batchSize: 100,
        maxConcurrency: 4,
        error => ProcessError(error),
        warning => ProcessWarning(warning)
    );
```

---

## V2.0 — Advanced Analytics & Streaming

New **streaming analytics** capabilities. This is where we compete with Kafka Streams/Flink.

> [!NOTE]
> **Connector Architecture Not Needed for V2.0**
> 
> DataFlow.NET already integrates seamlessly with external systems using existing primitives:
> - **EF Core** → Native via `.AsAsyncEnumerable()`
> - **Kafka/RabbitMQ** → Wrap consumer + `.WithBoundedBuffer()`
> - **REST APIs** → `.Poll()` + `.SelectMany()`
> - **WebSocket** → Async iterator + buffer
> 
> See [Integration Patterns Guide](Integration-Patterns-Guide.md) for examples.
> No dedicated "connector packages" are required.

### 📈 Advanced Analytics (NEW Functionality)

| # | Feature | Description | Effort |
|---|---------|-------------|--------|
| 12 | **Tumbling Windows** | Fixed-size, non-overlapping time windows | High |
| 13 | **Hopping Windows** | Overlapping time windows | High |
| 14 | **Session Windows** | Gap-based session detection | High |
| 15 | **Stateful Processing** | Per-key state across invocations | Very High |

#### Windowing API Design

```csharp
// Tumbling window: 10-second non-overlapping buckets
IAsyncEnumerable<double> averages = source
    .Window(TimeSpan.FromSeconds(10))
    .Select(window => window.Average(item => item.Value));

// Hopping window: 10-second windows, every 5 seconds
IAsyncEnumerable<int> counts = source
    .Window(TimeSpan.FromSeconds(10), hop: TimeSpan.FromSeconds(5))
    .Select(window => window.Count());
```

---

## V3.0 — No-Code & Enterprise

Features for **non-developers** and **enterprise governance**.

### 📋 Configuration-as-Code

Allow business analysts to define pipelines via YAML without writing C#.

```yaml
# pipeline.yaml
source:
  type: csv
  path: orders.csv

cases:
  - name: highValue
    condition: amount > 10000
  - name: international
    condition: country != 'US'

actions:
  highValue: 
    sink: compliance_review.json
  international:
    sink: forex_processing.json
  default:
    sink: standard_orders.json
```

```csharp
// C# loader
var pipeline = DataFlowPipeline.FromYaml("pipeline.yaml");
await pipeline.ExecuteAsync();
```

### 🔐 Enterprise Features (Future Consideration)

- Schema Registry integration
- Data lineage tracking
- Column-level encryption
- Audit logging

### 🎨 Visual Pipeline Designer

**Concept:** Drag-and-drop UI for building DataFlow pipelines without writing C#.

| Component | Technology Options |
|-----------|-------------------|
| Desktop App | WPF, Avalonia, or Electron |
| Web App | Blazor WebAssembly |
| Output | Generates YAML config or C# code |

```
┌─────────────┐    ┌─────────────┐    ┌─────────────┐
│  CSV Source │───►│   Cases     │───►│  JSON Sink  │
│  orders.csv │    │  HighValue  │    │  output.json│
└─────────────┘    │  Standard   │    └─────────────┘
                   └─────────────┘
```

**Use Case:** Enable business analysts to create ETL pipelines visually.

### 🔍 Row Tracing & Monitoring

**Concept:** Service for debugging data pipelines by tagging and tracing individual records.

**Features:**
- Tag a specific row at pipeline entry
- Trace its evolution through each transformation step
- Visual timeline of row state changes
- Cloud-hosted dashboard with debugging UI

```csharp
// Tag a row for tracing
var tracedPipeline = source
    .WithTracing(options => options
        .TagRow(row => row.Id == "ORD-12345")
        .SendTo("https://dataflow-monitor.example.com/api/trace")
    )
    .Cases(...)
    .SelectCase(...);
```

**Use Case:** Debug complex pipelines, compliance auditing, data quality monitoring.

## Implementation Matrix

| Version | Feature | Impact | Effort | Priority |
|---------|---------|--------|--------|----------|
| **V1.0** | NuGet Packages | Critical | Medium | **P0** 🔜 |
| **V1.0** | CI/CD Pipeline | Critical | Medium | **P0** 🔜 |
| **V1.0** | Fix Nullability Warnings | Medium | Low | **P0** 🔜 |
| **V1.0** | Integration Patterns Guide | High | Low | ✅ Done |
| **V1.0** | Benchmarks Suite | Medium | Low | ✅ Done |
| **V1.0** | Test Coverage Report | High | Medium | ✅ Done |
| **V1.0** | ObjectMaterializer Optimization | High | Medium | ✅ Done |
| **V1.0** | Real-World Demo | High | Medium | P0 🔜 |
| **V1.1** | Resilience Policies | High | Medium | P1 |
| **V1.1** | OpenTelemetry Metrics | Medium | Medium | P1 |
| **V1.2** | Micro-Batching | High | Medium | P1 |
| **V1.2** | CSV Reader Lightening | High | High | P2 |
| **V2.0** | Tumbling Windows | High | High | P2 |
| **V2.0** | Stateful Processing | High | Very High | P2 |
| **V3.0** | Configuration-as-YAML | High | High | P3 |

---

## Ideas Backlog (Unprioritized)

Captured from brainstorming sessions for future consideration. These are raw ideas that need further evaluation.

### 1. `AsFiles(filters)` — Directory Stream Reader

**Concept:** Return all files in a directory (recursively) as an `IEnumerable<FileInfo>`, with gitignore-style filtering.

```csharp
// Stream all .cs files, excluding bin/obj
var sourceFiles = Read.AsFiles("./src", filters: new[] { "*.cs", "!**/bin/**", "!**/obj/**" });

await sourceFiles
    .Select(file => new { file.Name, file.Length, file.LastWriteTime })
    .WriteCsv("file_inventory.csv");
```

**Use Case:** Batch file processing, code analysis tools, backup systems.

---

### 2. `GetEmails(filters)` — Email Inbox Reader

**Concept:** Connect to a mailbox (IMAP/Exchange) and stream emails as structured objects.

```csharp
public record EmailMessage(string Subject, string From, string[] To, string Body, DateTime ReceivedAt);

var unreadEmails = Read.GetEmails(
    server: "imap.gmail.com",
    credentials: emailCredentials,
    filters: new { Unread = true, After = DateTime.Today.AddDays(-7) }
);

await unreadEmails
    .Where(e => e.Subject.Contains("Invoice"))
    .Cases(e => e.From.Contains("vendor-a"), e => e.From.Contains("vendor-b"))
    .ForEachCase(
        vendorA => ProcessVendorAInvoice(vendorA),
        vendorB => ProcessVendorBInvoice(vendorB)
    );
```

**Use Case:** Email automation, invoice processing, support ticket ingestion.

---

### 3. `GetList(URL, selector)` — HTML Scraping to Stream

**Concept:** Fetch a web page and parse specific elements into an `IEnumerable<T>` using CSS selectors.

```csharp
// Scrape product listings from an e-commerce page
var products = Read.GetList<Product>(
    url: "https://store.example.com/products",
    selector: ".product-card",
    mapping: element => new Product(
        Name: element.QuerySelector(".title")?.TextContent,
        Price: decimal.Parse(element.QuerySelector(".price")?.TextContent ?? "0"),
        ImageUrl: element.QuerySelector("img")?.GetAttribute("src")
    )
);

await products.WriteCsv("scraped_products.csv");
```

**Use Case:** Price monitoring, competitor analysis, data collection from legacy systems without APIs.

---

### 4. AI Service Integration — LLM as Transformation Function

**Concept:** Use an LLM (OpenAI, Azure OpenAI, local models) as a transformation step in the pipeline.

```csharp
var aiService = new OpenAIService(apiKey);

var enrichedData = sourceData
    .Select(item => new { 
        Original = item,
        Summary = aiService.Summarize(item.Description),
        Sentiment = aiService.Analyze(item.CustomerFeedback)
    });

// Or as a fluent extension
var categorized = reviews
    .WithAI(aiService)
    .Categorize(r => r.Text, categories: ["Positive", "Negative", "Neutral"]);
```

**Use Case:** Content summarization, sentiment analysis, automatic categorization, data enrichment.

---

### 5. Incremental Processing — Checkpointing for Resume

**Concept:** Save pipeline state to enable resume-on-failure and incremental runs.

```csharp
// Resume-able pipeline
await source
    .WithCheckpoint("daily-etl-2024-12-05", storage: new FileCheckpointStorage("./checkpoints"))
    .Cases(...)
    .SelectCase(...)
    .OnFailure(checkpoint => checkpoint.SaveState())
    .Execute();

// Resume from last checkpoint
await pipeline.ResumeFrom("daily-etl-2024-12-05");
```

**Use Case:** Long-running ETL jobs, failure recovery, incremental data loads.

---

### 6. REST/GraphQL API Source — Paginated API Reader

**Concept:** Stream data from REST APIs with automatic pagination handling.

```csharp
// Cursor-based pagination
var users = Read.Api<User>(
    url: "https://api.example.com/users",
    pagination: Pagination.Cursor(response => response.NextCursor),
    headers: new { Authorization = $"Bearer {token}" }
);

// Offset-based pagination
var products = Read.Api<Product>(
    url: "https://api.example.com/products",
    pagination: Pagination.Offset(pageSize: 100)
);

await users.WriteCsv("all_users.csv");
```

**Use Case:** API data extraction, SaaS integrations, webhook replay.

---

### 7. Data Sampling & Preview — Quick Debugging

**Concept:** Lightweight extensions for quick data inspection during development.

```csharp
// Sample N random items
var sample = largeDataset.Sample(100);

// Preview first N items (with console output)
largeDataset.Preview(10);  // Prints to console

// Preview with custom formatter
largeDataset.Preview(5, item => $"{item.Id}: {item.Name}");

// Inline spy without breaking the chain
var results = source
    .Cases(...)
    .Spy("After Cases", count: 5)  // Logs 5 items, passes all through
    .SelectCase(...)
    .AllCases();
```

**Use Case:** Debugging, development, data exploration.

---

## Appendix: Completed Features (V1.0)

For reference, these features are already implemented and documented:

| Feature | Documentation |
|---------|---------------|
| Cases/SelectCase/ForEachCase pattern | [Cases-Pattern.md](Cases-Pattern.md) |
| AsyncEnumerable stream merging | [Stream-Merging.md](Stream-Merging.md) |
| CSV/JSON/YAML/Text readers | [DataFlow-Data-Reading-Infrastructure.md](DataFlow-Data-Reading-Infrastructure.md) |
| LINQ-to-Spark (SparkQuery) | [LINQ-to-Spark.md](LINQ-to-Spark.md) |
| LINQ-to-Snowflake (SnowflakeQuery) | [LINQ-to-Snowflake.md](LINQ-to-Snowflake.md) |
| Production-grade error handling | ErrorAction, ErrorSink, Metrics |
| ObjectMaterializer optimization | [ObjectMaterializer.md](ObjectMaterializer.md) |
| Integration Patterns Guide | [Integration-Patterns-Guide.md](Integration-Patterns-Guide.md) |
| Benchmarks Suite | [Benchmarks.md](Benchmarks.md) |
| Test Coverage Report | [COVERAGE.md](COVERAGE.md) |

---

*Last Updated: January 2026*

