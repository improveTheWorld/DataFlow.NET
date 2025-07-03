# The *DataFlow.NET* Framework

**Unified Data Processing for Batch and Streaming with Identical Syntax**

DataFlow.NET is a revolutionary open-source framework that **unifies batch and streaming data processing** in C#. Write your data processing logic once using our intuitive Cases/SelectCase/ForEachCase pattern, then apply it seamlessly to static files, real-time streams, or any data source - without changing a single line of processing code.

> **🚀 The Vision**: *Process CSV files during development, deploy the same code to handle live data streams in production*

**⚠️ Please note that DataFlow.NET is currently under active development and the current version is a prototype.**

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
- **Stream Merging**: Combine multiple data sources with `AsyncEnumerableMerger<T>`

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
var logStream = new AsyncEnumerableMerger<LogEntry>(
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

### 🏗️ **Stream Collection with AsyncEnumerableMerger**
```csharp
// Collect from multiple sources
var dataStream = new AsyncEnumerableMerger<SensorData>(
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
