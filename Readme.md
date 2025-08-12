# DataFlow.NET: Unified Data Processing for C#

DataFlow.NET is a high-performance C# framework that revolutionizes data processing by providing a single, elegant API for batch, streaming, and parallel workloads. It empowers developers to write transformation logic once and seamlessly apply it across synchronous, asynchronous, and parallel execution models without changing the code.

## Core Principles

-   **Write Once, Process Anywhere**: A unified API for `IEnumerable`, `IAsyncEnumerable`, `ParallelQuery` (PLINQ), and `ParallelAsyncQuery` ensures your logic is portable across different execution strategies.
-   **Single-Pass Transformation Engine**: The innovative `Cases`/`SelectCase`/`ForEachCase` pattern executes multiple, distinct transformation paths over a data stream in a single iteration, eliminating inefficient multi-pass processing and complex conditional statements.
-   **Seamlessly Integrated Parallelism**: Effortlessly switch from sequential to parallel processing to leverage multi-core architectures for both synchronous and asynchronous operations, dramatically improving performance for CPU-bound and I/O-bound tasks.
-   **Fluent & Readable Pipelines**: Build complex data processing pipelines that are declarative, easy to read, and simple to maintain, promoting clean separation of concerns.

---

## The Core Innovation: Single-Pass, Multi-Path Processing

A common challenge in data processing is applying different transformations based on the characteristics of each data item. Traditional approaches often lead to inefficient or unreadable code.

### The Problem: Inefficient and Cluttered Conditional Logic

Without DataFlow.NET, you might write code like this:

```csharp
// "Before" - Traditional Approach

// Inefficient: Multiple passes over the data
var errors = logs.Where(log => log.Level == "ERROR").Select(log => new Alert(log));
var warnings = logs.Where(log => log.Level == "WARNING").Select(log => new Alert(log));
var info = logs.Where(log => log.Level == "INFO").Select(log => new Alert(log));

// Unreadable: Complex logic inside a single Select
var results = logs.Select(log => {
    if (log.Level == "ERROR") {
        // ... transformation logic for errors
    } else if (log.Level == "WARNING") {
        // ... transformation logic for warnings
    } else {
        // ... transformation logic for info
    }
    // ... more complex logic
});
```

### The Solution: The `Cases` / `SelectCase` / `ForEachCase` Pattern

DataFlow.NET solves this with a powerful, three-stage pattern that is both highly efficient and exceptionally readable. It processes everything in a **single pass**.

1.  **`.Cases(...)`**: Categorizes each item into a numbered "lane" based on a set of predicates. Items that don't match any predicate are automatically placed in a final "else" lane.
2.  **`.SelectCase(...)`**: Applies a specific, clean transformation function to each lane.
3.  **`.ForEachCase(...)`**: Executes a side-effect (like logging or saving to a database) for each lane.

```csharp
// "After" - The DataFlow.NET Way

// A clean, readable, single-pass pipeline
var processedLogs = logs
    // 1. Categorize each log entry in a single pass
    .Cases(
        log => log.Level == "ERROR" || log.Level == "FATAL", // Lane 0
        log => log.Level == "WARN",                         // Lane 1
        log => log.Level == "INFO"                          // Lane 2
        // All other logs automatically go to Lane 3
    )
    // 2. Apply a different transformation to each category
    .SelectCase(
        critical => $"🚨 CRITICAL: [{critical.Source}] {critical.Message}",
        warning  => $"⚠️ WARNING: [{warning.Source}] {warning.Message}",
        info     => $"ℹ️ INFO: [{info.Source}] {info.Message}",
        other    => $"❓ UNKNOWN: [{other.Source}] {other.Message}"
    )
    // 3. (Optional) Perform a different action for each category
    .ForEachCase(
        criticalAlert => alertSystem.Send(criticalAlert),
        warningAlert  => logger.LogWarning(warningAlert),
        infoAlert     => logger.LogInfo(infoAlert)
    )
    // 4. Collect all transformed items
    .AllCases();
```

This pattern is the cornerstone of the framework, enabling you to build sophisticated, maintainable, and performant data pipelines.

---

## The Power of Four: A Unified API for All Execution Paths

DataFlow.NET's most powerful feature is its ability to run the exact same pipeline logic across four different execution models. This allows you to develop sequentially and scale to parallel or async processing with a single line of code change.

| Execution Path          | Interface               | Description                                           | How to Use                               |
| ----------------------- | ----------------------- | ----------------------------------------------------- | ---------------------------------------- |
| **Sequential**          | `IEnumerable<T>`        | Standard, single-threaded synchronous processing.     | (default)                                |
| **Parallel (PLINQ)**    | `ParallelQuery<T>`      | Multi-threaded synchronous processing for CPU-bound tasks. | `.AsParallel()`                          |
| **Async Sequential**    | `IAsyncEnumerable<T>`   | Single-threaded asynchronous processing for I/O-bound tasks. | `.ToAsyncEnumerable()` or `DataFlow<T>` |
| **Async Parallel**      | `ParallelAsyncQuery<T>` | Multi-threaded asynchronous processing for high-throughput. | `.AsParallel()` on an async source       |

### A Unified Example

The following example demonstrates the **exact same processing logic** being applied to data sources from all four execution paths.

```csharp
// Define the core processing logic ONCE
public IEnumerable<string> ProcessMetrics(IEnumerable<MetricEntry> metrics) => metrics
    .Cases(
        m => m.Name == "cpu_usage" && m.Value > 75,
        m => m.Name == "memory_usage" && m.Value > 85,
        m => m.Name == "network_latency" && m.Value > 180
    )
    .SelectCase(
        cpu     => $"🔥 HIGH CPU: {cpu.Value:F1}% on {cpu.Tags["host"]}",
        memory  => $"💾 HIGH MEMORY: {memory.Value:F1}% on {memory.Tags["host"]}",
        latency => $"🌐 HIGH LATENCY: {latency.Value:F1}ms on {latency.Tags["host"]}"
    )
    .AllCases();

//  Now, apply this logic to all four execution paths 

// ✅ Path 1: Sequential (IEnumerable)
var sequentialResults = ProcessMetrics(allMetrics);

// ✅ Path 2: Parallel (PLINQ)
var plinqResults = ProcessMetrics(allMetrics.AsParallel());

// ✅ Path 3: Async Sequential (IAsyncEnumerable)
var asyncSequentialSource = allMetrics.ToAsyncEnumerable();
var asyncSequentialResults = await ProcessMetrics(asyncSequentialSource); // Note: An async version of ProcessMetrics would be used here

// ✅ Path 4: Async Parallel (ParallelAsyncQuery)
var asyncParallelSource = allMetrics.ToAsyncEnumerable().AsParallel();
var asyncParallelResults = await ProcessMetrics(asyncParallelSource); // Note: An async version of ProcessMetrics would be used here
```
*(This example is a conceptual illustration. The `ProcessMetrics` method would have overloads to accept each collection type, but the internal `Cases`/`SelectCase` logic remains identical.)*

This unique capability allows you to choose the best execution model for your needs—from simple batch processing to high-throughput real-time streams—without ever rewriting your core business logic.

---


## Simplified & Integrated Regex Processing

DataFlow.NET provides a fluent, efficient, and deeply integrated API for regular expression processing. It allows you to deconstruct structured text within a data pipeline, transforming raw strings into structured data in a single, readable pass.

### 1. The `Regex` Builder: Composable & Readable Patterns

Instead of writing cryptic, hard-to-maintain regex strings, you can compose them using a declarative, fluent builder.

```csharp
using static DataFlow.Framework.Regex;

// Instead of this:
var webLogPattern_Old = @"INFO: \[WebServer\] Request ""(GET|POST) ([^""]+)"" completed in (\d+)ms with status (\d{3})";

// You can write this:
var webLogPattern = "INFO: [WebServer] Request " +
                    $"\"{OneOf("GET", "POST").As("Method")} {ANY_CHARS.As("Path")}\" " +
                    $"completed in {NUMS.As("Duration")}ms with status {NUMS.As("Status")}";

var dbErrorPattern = "ERROR: [Database] Query failed on table " +
                     $"'{ALPHANUMS.As("Table")}': {ANY_CHARS.As("Reason")}";
```

This approach makes your patterns self-documenting and significantly easier to build and modify.

### 2. Pipeline Integration with `MapLines` and `Cases`

The true power of the framework is revealed when you combine regex mapping with the core processing patterns.

1.  **`.MapLines(patterns)`**: This extension method is the entry point. It applies your regex patterns to each line in a stream. For each line, it efficiently returns a `List` of named parts that matched, using string slices to minimize memory allocations.
2.  **`.Cases(...)`**: This categorizes the result from `MapLines`. You can check for the existence of key named groups (`"Status"`, `"Table"`, etc.) to determine the log type.
3.  **`.SelectCase(...)`**: This transforms the list of parts for each category into a strongly-typed object (`WebLog`, `DbError`, etc.).
4.  **`.ForEachCase(...)`**: This performs a final action on each of the newly created objects, such as logging, alerting, or saving to a database.

### A Complete Example: Processing a Mixed-Format Log File

This example demonstrates the end-to-end pattern: reading a file, deconstructing lines with regex, and processing each type differently, all within a single, elegant pipeline.

**Input File (`system.log`):**
```
INFO: [WebServer] Request "GET /api/users" completed in 25ms with status 200
ERROR: [Database] Query failed on table 'Orders': Timeout expired
INFO: [WebServer] Request "POST /api/products" completed in 152ms with status 201
WARN: [Cache] Key 'user:123' not found. Falling back to source.
```

**Processing Pipeline:**
```csharp
using DataFlow.Data;
using DataFlow.Extensions;
using DataFlow.Framework;
using static DataFlow.Framework.Regex;

// 1. Define composable regex patterns for each log type
var webLogPattern = "INFO: [WebServer] Request " +
                    $"\"{OneOf("GET", "POST").As("Method")} {ANY_CHARS.As("Path")}\" " +
                    $"completed in {NUMS.As("Duration")}ms with status {NUMS.As("Status")}";

var dbErrorPattern = "ERROR: [Database] Query failed on table " +
                     $"'{ALPHANUMS.As("Table")}': {ANY_CHARS.As("Reason")}";

var cacheWarnPattern = "WARN: [Cache] Key " +
                       $"'{ANY_CHARS.As("CacheKey")}' not found.";

// 2. Combine patterns into a single RegexTokenizer object
var logPatterns = new RegexTokenizer(webLogPattern, dbErrorPattern, cacheWarnPattern);

// 3. Build the full, declarative processing pipeline
Read.text("system.log")
    // For each line, get a list of named parts that matched.
    .MapLines(logPatterns)

    // 4. Categorize each line's parts based on key group names.
    .Cases(
        parts => parts.Any(p => p.groupName == "Status"),      // Category 0: WebLog
        parts => parts.Any(p => p.groupName == "Table"),       // Category 1: DbError
        parts => parts.Any(p => p.groupName == "CacheKey")     // Category 2: CacheWarn
        // Unmatched lines fall into the final "else" category
    )

    // 5. Transform each category into a specific, strongly-typed object.
    .SelectCase(
        // Selector for WebLogs (Category 0)
        webLogParts => {
            var dict = webLogParts.ToDictionary(p => p.groupName, p => p.subpart);
            return (object)new WebLog(
                Method: dict["Method"],
                Path: dict["Path"],
                DurationMs: int.Parse(dict["Duration"]),
                Status: int.Parse(dict["Status"])
            );
        },
        // Selector for DbErrors (Category 1)
        dbErrorParts => {
            var dict = dbErrorParts.ToDictionary(p => p.groupName, p => p.subpart);
            return (object)new DbError(Table: dict["Table"], Reason: dict["Reason"]);
        },
        // Selector for CacheWarnings (Category 2)
        cacheWarnParts => {
            var dict = cacheWarnParts.ToDictionary(p => p.groupName, p => p.subpart);
            return (object)new CacheWarning(CacheKey: dict["CacheKey"]);
        },
        // Selector for unmatched lines (the "else" case)
        unmatchedParts => (object)unmatchedParts.First().subpart // The whole line
    )

    // 6. Perform a different action for each type of transformed object.
    .ForEachCase(
        // Action for WebLog objects
        logEvent => {
            var web = (WebLog)logEvent;
            Console.WriteLine($"[WEB] => Path: {web.Path}, Duration: {web.DurationMs}ms");
            // webAnalytics.Track(web);
        },
        // Action for DbError objects
        logEvent => {
            var db = (DbError)logEvent;
            Console.WriteLine($"[DB_ERROR] => Table: {db.Table}, Reason: {db.Reason}");
            // alertSystem.CreateTicket(db);
        },
        // Action for CacheWarning objects
        logEvent => {
            var cache = (CacheWarning)logEvent;
            Console.WriteLine($"[CACHE_WARN] => Key: {cache.CacheKey}");
            // monitoring.IncrementCacheMisses(cache);
        },
        // Action for unmatched strings
        logEvent => {
            var unmatched = (string)logEvent;
            Console.WriteLine($"[UNMATCHED] => {unmatched}");
        }
    )

    // 7. Trigger the pipeline execution.
    .Do(); // .Do() consumes the IEnumerable and executes the ForEachCase actions.

// Define records for the structured data
public record WebLog(string Method, string Path, int DurationMs, int Status);
public record DbError(string Table, string Reason);
public record CacheWarning(string CacheKey);
```

This integrated approach turns complex text parsing from a messy, imperative task into a clean, declarative part of your data flow, fully leveraging the power and readability of the entire framework.


DataFlow.NET provides a fluent, efficient, and deeply integrated API for regular expression processing. It allows you to deconstruct structured text within a data pipeline, transforming raw strings into structured data in a single, readable pass.

## Architecture Overview

The framework is designed in layers to ensure a clean separation of concerns:

-   **`DataFlow.Data`**: The data access layer. Provides unified readers (`Read.*`) and writers (`.Write...`) for various file formats (CSV, text, JSON) with full async and streaming support.
-   **`DataFlow.Extensions`**: The core logic layer. Contains the unified extension methods (`Cases`, `SelectCase`, `ForEach`, etc.) that work across all four execution paths.
-   **`DataFlow.Framework`**: The streaming and parallel infrastructure layer. Includes the `DataFlow<T>` stream merger, `DataPublisher<T>` for real-time events, and the `ParallelAsyncQuery<T>` engine.

## Quick Start Guide

### 1. Installation

Reference the DataFlow projects in your solution. (NuGet package coming soon).

### 2. A Simple, Unified Processing Example

This example processes log entries from a file (batch) and a live stream using the **exact same logic**.

```csharp
using DataFlow.Data;
using DataFlow.Extensions;
using DataFlow.Framework;

// Define your data model
public record LogEntry(DateTime Timestamp, string Level, string Message, string Service);

// --- BATCH PROCESSING from a file ---
var batchResults = Read.csv<LogEntry>("historical_logs.csv")
    .Cases(
        log => log.Level == "ERROR",
        log => log.Level == "WARNING"
    )
    .SelectCase(
        error   => $"🚨 {error.Service}: {error.Message}",
        warning => $"⚠️ {warning.Service}: {warning.Message}",
        info    => $"ℹ️ {info.Service}: {info.Message}"
    )
    .AllCases()
    .WriteText("processed_batch_alerts.log");

// --- STREAM PROCESSING from a live source ---
var liveLogStream = new DataFlow<LogEntry>(/* ... connect to live sources ... */);

var streamResults = await liveLogStream
    // The logic is IDENTICAL to the batch version!
    .Cases(
        log => log.Level == "ERROR",
        log => log.Level == "WARNING"
    )
    .SelectCase(
        error   => $"🚨 {error.Service}: {error.Message}",
        warning => $"⚠️ {warning.Service}: {warning.Message}",
        info    => $"ℹ️ {info.Service}: {info.Message}"
    )
    .AllCases()
    .WriteTextAsync("processed_stream_alerts.log"); // Use async writer for streams
```

This demonstrates the power of writing your logic once and deploying it anywhere, from simple batch jobs to complex, real-time streaming applications.

> **🚀 The Vision**: *Process CSV files during development, deploy the same code to handle live data streams in production*


## Why DataFlow.NET?

### 🔄 **Write Once, Process Anywhere**
```csharp
// This SAME processing logic works for both batch and streaming:
var processLogs = (data) => data
    .Cases(log => log.Level == "ERROR", log => log.Level == "WARNING")
    .SelectCase(error => $"🚨 {error.Message}", warning => $"⚠️ {warning.Message}")
    .ForEachCase(SendAlert, LogWarning)
    .AllCases();

// BATCH: Process historical logs
processLogs(Read.csv<LogEntry>("historical_logs.csv"));

// STREAMING: Process live logs (IDENTICAL CODE!)
await processLogs(liveLogStream.AsAsyncEnumerable());
```

### 🎯 **Intelligent Category Processing**
DataFlow.NET's **Supra Category Pattern** lets you focus on what matters while gracefully handling unexpected data:

```csharp
// Only process errors and warnings, ignore everything else
logs.Cases(IsError, IsWarning)  // Info/Debug logs become "supra category"
    .SelectCase(HandleError, HandleWarning)  // Only transform what you care about
    .ForEachCase(AlertError, LogWarning)     // Supra category gracefully ignored
    .AllCases();
```

## Key Features

### 🚀 **Unified Processing Architecture**
- **Identical Syntax**: Same fluent API for batch files and real-time streams
- **Zero Migration Cost**: Convert batch processing to streaming without code changes
- **Async-First Design**: Built on `IAsyncEnumerable<T>` with sync compatibility
- **Stream Merging**: Combine multiple data sources with `DataFlow<T>`

### 📊 **Powerful Data Reading**
- **Multiple Formats**: Text, CSV, JSON, YAML, and custom CFG Grammar files
- **Memory Efficient**: Lazy evaluation processes huge files without memory issues
- **Stream-Ready**: All readers work with both files and live data sources

### 🎨 **Fluent Data Transformation**
- **Cases Pattern**: Categorize data with multiple predicates in one operation
- **SelectCase**: Transform each category with different logic
- **ForEachCase**: Execute side effects per category (logging, alerts, etc.)
- **Supra Category**: Gracefully handle unmatched data

### ⚡ **Performance Optimized**
- **Single-Pass Processing**: Transform, filter, and write in one iteration
- **Lazy Evaluation**: Process data as needed, not all at once
- **Channel-Based Streaming**: High-performance async data flow
- **Memory Conscious**: Handle massive datasets efficiently

### 🔧 **Developer Experience**
- **Intuitive Regex**: Simplified pattern matching with human-friendly syntax
- **Fluent API**: Chain operations naturally with method chaining
- **LINQ Integration**: Works seamlessly with existing .NET code
- **Rich Extensions**: Comprehensive `IEnumerable` and `IAsyncEnumerable` extensions

## Quick Start Examples

### 📁 **Batch Processing: CSV Transformation**
```csharp
// Read, transform, and write CSV in one fluent chain
Read.csv<Person>("people.csv")
    .Where(p => p.Age >= 18)
    .Select(p => { p.Name = p.Name.ToUpper(); return p; })
    .WriteCSV("adults_uppercase.csv");
```

### 🔄 **Streaming: Real-Time Log Processing**
```csharp
// Merge multiple log sources and process in real-time
var logStream = new DataFlow<LogEntry>(
    webServerLogs, databaseLogs, authServiceLogs
);

await logStream
    .Cases(log => log.Level == "ERROR", log => log.Level == "WARNING")
    .SelectCase(
        error => $"🚨 CRITICAL: {error.Message}",
        warning => $"⚠️ WARNING: {warning.Message}"
    )
    .ForEachCase(
        critical => await alertSystem.SendAsync(critical),
        warning => await logger.LogAsync(warning)
    )
    .AllCases()
    .WriteTextAsync("processed_logs.txt");
```

### 🎯 **Smart Category Processing**
```csharp
// Process only what you care about, ignore the rest
Read.text("mixed_logs.txt")
    .Cases(
        line => line.Contains("ERROR"),
        line => line.Contains("WARNING")
        // INFO, DEBUG, TRACE logs become supra category (ignored)
    )
    .SelectCase(
        error => $"ERROR: {error}",     // Handle errors
        warning => $"WARNING: {warning}" // Handle warnings
        // No selector for supra category = graceful ignoring
    )
    .ForEachCase(
        error => errorWriter.WriteLine(error),
        warning => warningWriter.WriteLine(warning)
        // Supra category automatically skipped
    )
    .AllCases()
    .Where(line => line != null)  // Filter out ignored items
    .WriteText("filtered_logs.txt");
```

### 🔍 **Regex Pattern Matching**
```csharp
// Extract and process HTTP status codes from logs
Read.text("access.log")
    .Map($"HTTP/1.1\" {NUMS.As("StatusCode")} {NUMS.As("ResponseSize")}")
    .Cases("StatusCode")
    .SelectCase(code => 
        code.StartsWith("4") || code.StartsWith("5") ? 
        $"Error: {code}" : $"Success: {code}")
    .AllCases()
    .Display();
```

## Architecture Highlights

### 🏗️ **Stream Collection with DataFlow**
```csharp
// Collect from multiple sources
var dataStream = new DataFlow<SensorData>(
    temperatureSensors, humiditySensors, pressureSensors
);

// Process with familiar syntax
await dataStream
    .Cases(data => data.IsCritical(), data => data.IsWarning())
    .SelectCase(ProcessCritical, ProcessWarning)
    .AllCases()
    .WriteCSVAsync("sensor_data.csv");
```

### 🔄 **Seamless Batch-to-Stream Migration**
```csharp
// Start with batch processing during development
var testResults = Read.csv<Order>("test_orders.csv")
    .Cases(IsUrgent, IsStandard)
    .SelectCase(ProcessUrgent, ProcessStandard)
    .AllCases();

// Deploy with streaming (ZERO code changes!)
var liveResults = await orderStream
    .Cases(IsUrgent, IsStandard)        // Same predicates
    .SelectCase(ProcessUrgent, ProcessStandard)  // Same transformations
    .AllCases();                        // Same result extraction
```

## Getting Started

### Installation
```bash
# NuGet Package (coming soon)
dotnet add package DataFlow.NET

# Or clone from GitHub
git clone https://github.com/yourusername/DataFlow.NET.git
```

### Your First DataFlow.NET Program
```csharp
using DataFlow.NET;

// Read CSV, process, and write results
Read.csv<Customer>("customers.csv")
    .Cases(c => c.IsVIP, c => c.IsActive)
    .SelectCase(
        vip => $"VIP: {vip.Name}",
        active => $"Active: {active.Name}",
        inactive => $"Inactive: {inactive.Name}"
    )
    .AllCases()
    .WriteText("customer_status.txt");
```

## What's Next?

- 📖 **[Technical Documentation](docs/README.md)**: Deep dive into advanced features
- 🎯 **[API Reference](docs/api/)**: Complete method documentation  
- 💡 **[Examples](examples/)**: Real-world usage scenarios
- 🚀 **[Getting Started Guide](docs/getting-started.md)**: Step-by-step tutorials

## Community & Support

- 🐛 **[Issues](https://github.com/yourusername/DataFlow.NET/issues)**: Bug reports and feature requests
- 💬 **[Discussions](https://github.com/yourusername/DataFlow.NET/discussions)**: Community Q&A
- 📧 **Email**: [tecnet.paris@gmail.com](mailto:tecnet.paris@gmail.com)

## Licensing

- **Open Source**: Apache V2.0 License for free software use - see [LICENSE](./LICENSE-APACHE.txt)
- **Commercial**: For commercial software use, see [LICENSE_NOTICE](./LICENSE_NOTICE.md)

---

**DataFlow.NET** - *Where Data Processing Meets Simplicity* 🚀
