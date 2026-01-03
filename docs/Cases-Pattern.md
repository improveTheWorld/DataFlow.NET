# Cases/SelectCase/ForEachCase Pattern

> **This document covers DataFlow.NET's core innovation: writing processing logic once and deploying it across batch, streaming, and distributed paradigms.**

---

## Table of Contents

1. [Configuration-Driven Transformation Trees](#1-configuration-driven-transformation-trees)
2. [Write Once, Process Anywhere](#2-write-once-process-anywhere)
3. [The Cases/SelectCase/ForEachCase Pattern](#3-the-casesselectcaseforeachcase-pattern)
4. [The Supra Category Pattern](#4-the-supra-category-pattern)
5. [Multi-Type Branching](#5-multi-type-branching)
6. [API Reference](#6-api-reference)

---

## 1. Configuration-Driven Transformation Trees

DataFlow.NET introduces  **Cases/SelectCase/ForEachCase pattern** that lets you configure complex, multi-branch transformation trees **declaratively**. Despite defining multiple transformation paths upfront, the framework executes them **lazily**—each item flows through the pipeline **exactly once**, with **zero buffering** and **minimal memory footprint**.

```csharp
// Configure a complete transformation tree ONCE
await dataSource
    .Cases(
        data => data.Type == "Customer",
        data => data.Type == "Order", 
        data => data.Type == "Product"
    )
    .SelectCase(
        customer => EnrichCustomer(customer),      // Branch 1: Transform
        order => CalculateOrderTotal(order),       // Branch 2: Transform
        product => NormalizeProduct(product),      // Branch 3: Transform
        unknown => LogUnknownType(unknown)         // Supra: Catch-all
    )
    .ForEachCase(
        customer => await customerDB.SaveAsync(customer),   // Branch 1: Side-effect
        order => await orderDB.SaveAsync(order),            // Branch 2: Side-effect
        product => await productDB.SaveAsync(product),      // Branch 3: Side-effect
        unknown => await errorLogger.LogAsync(unknown)      // Supra: Error handling
    )
    .AllCases()
    .WriteCsv("processed_output.csv");

// ✅ Tree configured once, executed lazily
// ✅ Each item processed exactly once
// ✅ No intermediate collections
// ✅ Constant memory usage
```

**Key Benefits:**

- **🎯 Declarative Configuration**: Define all transformation branches upfront in a readable, maintainable way
- **⚡ Single-Pass Execution**: Despite multiple branches, each item flows through the pipeline exactly once
- **💾 Memory Efficient**: Lazy evaluation means zero buffering—process gigabytes with constant memory
- **🔧 Developer-Friendly**: Simple, configuration-like syntax that reads like a decision tree

---

## 2. Write Once, Process Anywhere

The framework's **unified processing model** delivers on a revolutionary promise: **write your processing logic once, deploy it across batch and streaming paradigms without code changes**.

### Identical Syntax Across Paradigms

```csharp
// Define processing logic ONCE
public static async Task ProcessBusinessLogic<T>(T dataSource) 
    where T : IAsyncEnumerable<Transaction>
{
    await dataSource
        .Cases(
            tx => tx.Amount > 10000,
            tx => tx.IsFlagged,
            tx => tx.Country != "US"
        )
        .SelectCase(
            highValue => ProcessHighValue(highValue),
            suspicious => ProcessSuspicious(suspicious),
            international => ProcessInternational(international),
            standard => ProcessStandard(standard)
        )
        .ForEachCase(
            highValue => await complianceDB.SaveAsync(highValue),
            suspicious => await fraudDB.SaveAsync(suspicious),
            international => await forexDB.SaveAsync(international),
            standard => await standardDB.SaveAsync(standard)
        )
        .AllCases()
        .WriteCsv("processed_transactions.csv");
}

// BATCH: Historical file processing
await ProcessBusinessLogic(Read.Csv<Transaction>("historical_data.csv"));

// STREAMING: Real-time event processing (IDENTICAL CODE!)
await ProcessBusinessLogic(liveTransactionStream);
```

### Zero-Cost Migration Path

```csharp
// DEVELOPMENT: Start with in-memory data
var testData = new[] { 
    new Order { Id = 1, IsUrgent = true },
    new Order { Id = 2, IsUrgent = false }
}.Async();

// VALIDATION: Test with static files
var devPipeline = Read.Csv<Order>("test_orders.csv")
    .Cases(IsUrgent, IsInternational, IsHighValue)
    .SelectCase(
        urgent => ProcessUrgent(urgent),
        international => ProcessInternational(international),
        highValue => ProcessHighValue(highValue),
        standard => ProcessStandard(standard)
    )
    .AllCases();

// PRODUCTION: Deploy to live streams (ZERO CODE CHANGES!)
var prodPipeline = liveOrderStream
    .Cases(IsUrgent, IsInternational, IsHighValue)     // Same predicates
    .SelectCase(
        urgent => ProcessUrgent(urgent),                // Same transforms
        international => ProcessInternational(international),
        highValue => ProcessHighValue(highValue),
        standard => ProcessStandard(standard)
    )
    .AllCases();
```

**Migration Benefits:**

- ✅ **Develop with in-memory tables**: Test and debug directly in your IDE
- ✅ **Validate performance with files**: Benchmark with realistic datasets
- ✅ **Deploy to streams**: Switch to Kafka/EventHub/SignalR without refactoring
- ✅ **Zero code changes**: Same predicates, same transformations, same side-effects

---

## 3. The Cases/SelectCase/ForEachCase Pattern

### Pattern Overview

The pattern consists of four chained operations:

| Method | Purpose | Input | Output |
|--------|---------|-------|--------|
| `Cases()` | Categorize items | `IAsyncEnumerable<T>` | `IAsyncEnumerable<(int, T)>` |
| `SelectCase()` | Transform by category | `(int, T)` | `(int, T, R)` |
| `ForEachCase()` | Side-effects | `(int, T, R)` | `(int, T, R)` |
| `AllCases()` | Extract results | `(int, T, R)` | `IAsyncEnumerable<R>` |

### Complete Example

```csharp
var results = await logs
    .Cases(
        log => log.Level == "ERROR",    // Category 0
        log => log.Level == "WARNING",  // Category 1
        log => log.Level == "INFO"      // Category 2
        // Everything else → Category 3 (supra)
    )
    .SelectCase(
        error => $"🚨 {error.Message}",     // Transform errors
        warning => $"⚠️ {warning.Message}", // Transform warnings
        info => $"ℹ️ {info.Message}",       // Transform info
        other => $"📝 {other.Message}"      // Transform supra
    )
    .ForEachCase(
        error => await alerter.SendAsync(error),      // Alert on errors
        warning => await logger.WarnAsync(warning),   // Log warnings
        info => await logger.InfoAsync(info),         // Log info
        other => { }                                  // Ignore supra
    )
    .AllCases()
    .ToListAsync();
```

---

## 4. The Supra Category Pattern

The **Supra Category Pattern** is DataFlow.NET's signature feature for intelligent, selective data processing.

### How It Works

When using `Cases()` to categorize data:

1. Items matching the first predicate get category `0`
2. Items matching the second predicate get category `1`
3. Items matching the nth predicate get category `n-1`
4. **Items not matching ANY predicate get category `n` (the "supra category")**

### Selective Processing Philosophy

The supra category enables a **"selective processing"** approach:

- **Express Intent**: Provide selectors/actions only for categories you care about
- **Graceful Ignoring**: Missing selectors return `default(T)`, enabling natural filtering
- **Future-Proof**: New data patterns don't break existing processing pipelines
- **Performance Optimized**: Single-pass processing with minimal overhead

### Example: Process Only What Matters

```csharp
// Process only ERROR and WARNING logs, ignore everything else
var processedLogs = Read.Text("application.log")
    .Cases(
        line => line.Contains("ERROR"),   // Category 0
        line => line.Contains("WARNING")  // Category 1
        // DEBUG, INFO, TRACE → Category 2 (supra category)
    )
    .SelectCase(
        error => $"🚨 CRITICAL: {error}",     // Handle category 0
        warning => $"⚠️ WARNING: {warning}"   // Handle category 1
        // No selector for category 2 → gets default(string) = null
    )
    .Where(result => result.newItem != null)  // Filter out ignored items
    .AllCases();
```

### Advanced Multi-Category Processing

```csharp
var transactionAlerts = await liveTransactionStream
    .Cases(
        tx => tx.Amount > 10000,           // Category 0: High value
        tx => tx.IsFlagged,                // Category 1: Suspicious
        tx => tx.IsInternational,          // Category 2: International
        tx => tx.Customer.IsVIP            // Category 3: VIP customer
        // Regular transactions → Category 4 (supra category)
    )
    .SelectCase(
        highValue => new ComplianceReview { Transaction = highValue, Priority = Priority.High },
        suspicious => new FraudInvestigation { Transaction = suspicious, Urgent = true },
        international => new CurrencyConversion { Transaction = international },
        vip => new VIPProcessing { Transaction = vip, FastTrack = true }
        // No selector for regular transactions → they get default(object) = null
    )
    .Where(x => x.newItem != null)  // Remove regular transactions
    .ForEachCase(
        compliance => await complianceSystem.ReviewAsync(compliance),
        fraud => await fraudDetection.InvestigateAsync(fraud),
        currency => await currencyService.ConvertAsync(currency),
        vip => await vipProcessor.FastTrackAsync(vip)
    )
    .AllCases()
    .WriteCsv("special_transactions.csv");
```

---

## 5. Multi-Type Branching

When different branches require **different return types**, DataFlow.NET provides dedicated `SelectCases` and `ForEachCases` methods (note the **plural form**) that maintain full type safety without requiring a common base type.

### The Challenge

In standard C#, all branches of a switch expression must return the same type. But real-world processing often requires different types per branch:

```csharp
// ❌ This doesn't compile - different return types
.SelectCase(
    error => new ErrorReport { ... },      // Returns ErrorReport
    warning => new WarningLog { ... },     // Returns WarningLog  
    info => new InfoMetric { ... }         // Returns InfoMetric
)
```

### The Solution: Dedicated Multi-Type Methods

DataFlow.NET solves this with `SelectCases` (plural) which returns a **flat tuple with nullable types**. Only the slot matching the executed branch contains a value:

```csharp

await logs
    .Cases(
        log => log.Level == "ERROR",
        log => log.Level == "WARNING",
        log => log.Level == "INFO"
    )
    // Each branch returns a DIFFERENT type - use SelectCases (with 's')
    .SelectCases<Log, ErrorReport, WarningLog, InfoMetric>(
        error => new ErrorReport { Severity = 1, Message = error.Text },
        warning => new WarningLog { Category = warning.Source },
        info => new InfoMetric { MetricName = info.Key, Value = info.Count }
    )
    // ForEachCases receives the correct type for each branch
    .ForEachCases<Log, ErrorReport, WarningLog, InfoMetric>(
        error => await errorDb.SaveAsync(error),
        warning => await logDb.SaveAsync(warning),
        info => await metricsDb.SaveAsync(info)
    )
    .UnCase();  // Returns to original items
```

### How It Works

| Branch | Result Tuple |
|--------|-------------|
| Category 0 (ERROR) | `(ErrorReport, null, null)` |
| Category 1 (WARNING) | `(null, WarningLog, null)` |
| Category 2 (INFO) | `(null, null, InfoMetric)` |

### API Variants

Multi-type branching supports **2 to 7 different types**:

```csharp
// Single-type: all selectors return the same type R
.SelectCase(selector1, selector2, selector3)  // Uses params Func<T,R>[]

// Multi-type: each selector returns a DIFFERENT type
.SelectCases<T, R1, R2>(selector1, selector2)  // Returns flat tuple (R1?, R2?)

// 3 types
.SelectCases<T, R1, R2, R3>(selector1, selector2, selector3)

// Up to 7 types
.SelectCases<T, R1, R2, R3, R4, R5, R6, R7>(...)
```

### Available Collections

Multi-type branching is available for:
- `IAsyncEnumerable<T>` (2-7 types)
- `IEnumerable<T>` (2-7 types)
- `ParallelQuery<T>` (2-7 types)
- `ParallelAsyncQuery<T>` (2-7 types)
- `SparkQuery<T>` (2-4 types) — *Premium*

---

## 6. API Reference

### Cases<T>

```csharp
// Categorize items using predicates
public static IAsyncEnumerable<(int category, T item)> Cases<T>(
    this IAsyncEnumerable<T> items, 
    params Func<T, bool>[] filters)
```

### SelectCase<T, R>

```csharp
// Transform items per category
public static IAsyncEnumerable<(int category, T item, R newItem)> SelectCase<T, R>(
    this IAsyncEnumerable<(int category, T item)> items, 
    params Func<T, R>[] selectors)
```

### ForEachCase<T, R>

```csharp
// Execute side-effects per category
public static IAsyncEnumerable<(int category, T item, R newItem)> ForEachCase<T, R>(
    this IAsyncEnumerable<(int category, T item, R newItem)> items, 
    params Func<R, Task>[] actions)
```

### AllCases<T, R>

```csharp
// Extract transformed results
public static IAsyncEnumerable<R> AllCases<T, R>(
    this IAsyncEnumerable<(int category, T item, R newItem)> items)
```

---

## See Also

- [Stream Merging](Stream-Merging.md) — Multi-source stream processing
- [Data Reading](DataFlow-Data-Reading-Infrastructure.md) — CSV, JSON, YAML readers
- [LINQ-to-Spark](LINQ-to-Spark.md) — Distributed processing
