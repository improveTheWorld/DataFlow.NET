# Stream Merging with UnifiedStream

> **This document covers DataFlow.NET's multi-source stream merging capabilities using the `UnifiedStream<T>` class.**

---

## Table of Contents

1. [Overview](#1-overview)
2. [Basic Usage](#2-basic-usage)
3. [Configuration Options](#3-configuration-options)
4. [Multi-Source Architecture](#4-multi-source-architecture)
5. [Performance Optimization](#5-performance-optimization)
6. [API Reference](#6-api-reference)

---

## 1. Overview

The `UnifiedStream<T>` class (aliased as `AsyncEnumerable<T>`) merges multiple `IAsyncEnumerable<T>` sources into a single unified stream. It manages concurrent `MoveNextAsync` calls, synchronization, and source lifecycle during enumeration.

**Key Characteristics:**

- **Zero Built-in Buffering**: Pull-based streaming (use opt-in buffering when needed)
- **Source Management**: Register/unregister sources before enumeration
- **Fairness Policies**: `FirstAvailable` or `RoundRobin` scheduling
- **Error Modes**: `FailFast` or `ContinueOnError`
- **Per-Source Filtering**: Optional predicates per source

---

## 2. Basic Usage

### Simple Multi-Source Merge

```csharp
// Create merger and register sources
var unifiedLogs = new UnifiedStream<LogEntry>()
    .Unify(webServerLogs, "web")
    .Unify(databaseLogs, "db")
    .Unify(authServiceLogs, "auth");

// Process with standard LINQ/DataFlow operations
await foreach (var log in unifiedLogs)
{
    Console.WriteLine($"[{log.Source}] {log.Message}");
}
```

### With Cases Pattern

```csharp
var unifiedLogs = new UnifiedStream<LogEntry>()
    .Unify(webServerLogs, "web")
    .Unify(databaseLogs, "db")
    .Unify(authServiceLogs, "auth");

await unifiedLogs
    .Cases(
        log => log.Level == LogLevel.Error,
        log => log.Level == LogLevel.Warning
    )
    .SelectCase(
        error => $"CRITICAL: {error.Service} - {error.Message}",
        warning => $"WARN: {warning.Service} - {warning.Message}",
        info => $"INFO: {info.Message}"
    )
    .ForEachCase(
        error => await alertSystem.SendCriticalAsync(error),
        warning => await alertSystem.SendWarningAsync(warning),
        info => await generalLogger.LogAsync(info)
    )
    .AllCases()
    .WriteText("unified_logs.txt");
```

---

## 3. Configuration Options

### UnifyOptions

```csharp
var options = new UnifyOptions
{
    ErrorMode = UnifyErrorMode.ContinueOnError,  // Don't fail on single source error
    Fairness = UnifyFairness.RoundRobin          // Fair scheduling across sources
};

var merger = new UnifiedStream<Event>(options);
```

### Error Modes

| Mode | Behavior |
|------|----------|
| `FailFast` | Any source exception fails the whole stream |
| `ContinueOnError` | Drop failing source, continue with others |

### Fairness Policies

| Policy | Behavior |
|--------|----------|
| `FirstAvailable` | Yields whichever source completes first (performance) |
| `RoundRobin` | Cycles through sources to prevent starvation |

### Per-Source Filtering

```csharp
var merger = new UnifiedStream<LogEntry>()
    .Unify(webServerLogs, "web", log => log.Level >= LogLevel.Info)
    .Unify(databaseLogs, "db", log => log.Level >= LogLevel.Warning)
    .Unify(authServiceLogs, "auth", log => log.Level >= LogLevel.Error);
```

---

## 4. Multi-Source Architecture

### Heterogeneous Event Processing

```csharp
public class MultiSourceProcessor
{
    public async Task ProcessBusinessEvents()
    {
        // Create separate mergers for different event types
        var orderStream = new UnifiedStream<OrderEvent>()
            .Unify(orderChannel.Reader.ReadAllAsync(), "orders");
            
        var inventoryStream = new UnifiedStream<InventoryEvent>()
            .Unify(inventoryChannel.Reader.ReadAllAsync(), "inventory");

        // Process each stream type with specialized logic
        var orderTask = ProcessOrderEvents(orderStream);
        var inventoryTask = ProcessInventoryEvents(inventoryStream);

        // Run all processors concurrently
        await Task.WhenAll(orderTask, inventoryTask);
    }

    private async Task ProcessOrderEvents(IAsyncEnumerable<OrderEvent> stream)
    {
        await stream
            .Cases(
                order => order.Type == OrderType.HighValue,
                order => order.Type == OrderType.International,
                order => order.Customer.IsVIP
            )
            .SelectCase(
                highValue => new HighValueOrderAlert { OrderId = highValue.OrderId },
                international => new InternationalOrderProcess { OrderId = international.OrderId },
                vip => new VIPOrderProcess { OrderId = vip.OrderId },
                standard => new StandardOrderProcess { OrderId = standard.OrderId }
            )
            .ForEachCase(
                highValue => await approvalSystem.RequestApprovalAsync(highValue),
                international => await internationalProcessor.ProcessAsync(international),
                vip => await vipProcessor.PrioritizeAsync(vip),
                standard => await standardQueue.EnqueueAsync(standard)
            )
            .AllCases()
            .WriteJson($"orders_{DateTime.Now:yyyyMMdd}.json");
    }
}
```

### Priority-Based Stream Separation

```csharp
// Create conditional mergers for different severity levels
var criticalEvents = new UnifiedStream<SystemEvent>()
    .Unify(webEvents.Where(e => e.Severity == Severity.Critical), "web")
    .Unify(dbEvents.Where(e => e.Severity == Severity.Critical), "db")
    .Unify(authEvents.Where(e => e.Severity == Severity.Critical), "auth");

var warningEvents = new UnifiedStream<SystemEvent>()
    .Unify(webEvents.Where(e => e.Severity == Severity.Warning), "web")
    .Unify(dbEvents.Where(e => e.Severity == Severity.Warning), "db")
    .Unify(authEvents.Where(e => e.Severity == Severity.Warning), "auth");

// Process each severity level differently
await Task.WhenAll(
    ProcessCriticalEvents(criticalEvents),
    ProcessWarningEvents(warningEvents)
);
```

---

## 5. Performance Optimization

### Channel-Based Backpressure

```csharp
var channelOptions = new BoundedChannelOptions(1000)
{
    FullMode = BoundedChannelFullMode.Wait,  // Apply backpressure
    SingleReader = false,
    SingleWriter = false,
    AllowSynchronousContinuations = false
};

var channel = Channel.CreateBounded<DataPoint>(channelOptions);

await channel.Reader.ReadAllAsync()
    .Cases(data => data.Priority == Priority.High, data => data.Priority == Priority.Medium)
    .SelectCase(ProcessHigh, ProcessMedium, ProcessLow)
    .AllCases()
    .WriteCsv("processed_data.csv");
```

### Buffering Extensions

```csharp
// Add bounded buffer to high-throughput source
var bufferedSource = highThroughputSource.WithBoundedBuffer(
    capacity: 500,
    fullMode: BoundedChannelFullMode.Wait
);

// Or with custom options
var customBuffered = source.WithBoundedBuffer(new BoundedChannelOptions(1000)
{
    SingleReader = true,
    AllowSynchronousContinuations = true
});
```

### Converting Sync to Async

```csharp
// Convert IEnumerable to IAsyncEnumerable with yielding
var asyncData = syncData.Async(yieldThresholdMs: 15);

// Or with explicit buffering
var bufferedAsync = syncData.BufferAsync(
    yieldThresholdMs: 15,
    runOnBackgroundThread: true
);
```

---

## 6. API Reference

### UnifiedStream<T>

```csharp
public sealed class UnifiedStream<T> : IAsyncEnumerable<T>
{
    // Construction
    public UnifiedStream(UnifyOptions? options = null);
    
    // Source registration (before enumeration starts)
    public UnifiedStream<T> Unify(
        IAsyncEnumerable<T> source, 
        string name, 
        Func<T, bool>? predicate = null);
    
    // Source removal (before enumeration starts)
    public bool Unlisten(string name);
    
    // Enumeration
    public IAsyncEnumerator<T> GetAsyncEnumerator(
        CancellationToken cancellationToken = default);
}
```

### UnifyOptions

```csharp
public sealed class UnifyOptions
{
    public UnifyErrorMode ErrorMode { get; init; } = UnifyErrorMode.FailFast;
    public UnifyFairness Fairness { get; init; } = UnifyFairness.FirstAvailable;
}

public enum UnifyErrorMode { FailFast, ContinueOnError }
public enum UnifyFairness { FirstAvailable, RoundRobin }
```

### Buffering Extensions

```csharp
// Convert sync to async with optional yielding
public static IAsyncEnumerable<T> Async<T>(
    this IEnumerable<T> items, 
    long yieldThresholdMs = 15);

// Add bounded buffer to async stream
public static IAsyncEnumerable<T> WithBoundedBuffer<T>(
    this IAsyncEnumerable<T> source, 
    int capacity, 
    BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait);
```

---

## See Also

- [Unified Processing](Unified-Processing.md) — Cases/SelectCase pattern
- [Data Reading](DataFlow-Data-Reading-Infrastructure.md) — CSV, JSON, YAML readers  
- [LINQ-to-Spark](LINQ-to-Spark.md) — Distributed processing
