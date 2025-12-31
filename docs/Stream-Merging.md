# Stream Merging with UnifiedStream

> **This document covers DataFlow.NET's multi-source stream merging capabilities using the `UnifiedStream<T>` class.**

---

## Table of Contents

1. [Overview](#1-overview)
2. [Basic Usage](#2-basic-usage)
3. [Configuration Options](#3-configuration-options)
4. [Multi-Source Patterns](#4-multi-source-patterns)
5. [API Reference](#5-api-reference)

---

## 1. Overview

The `UnifiedStream<T>` class merges multiple `IAsyncEnumerable<T>` sources into a single unified stream. It manages concurrent `MoveNextAsync` calls, synchronization, and source lifecycle during enumeration.

**Key Characteristics:**

- **Zero Built-in Buffering**: Pull-based streaming (no internal buffering)
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

## 4. Multi-Source Patterns

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

## 5. API Reference

### UnifiedStream\<T\>

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

---

## See Also

- [Polling and Buffering](Polling-Buffering.md) — Data acquisition patterns
- [Cases Pattern](Cases-Pattern.md) — Cases/SelectCase pattern
- [Data Reading](DataFlow-Data-Reading-Infrastructure.md) — CSV, JSON, YAML readers
