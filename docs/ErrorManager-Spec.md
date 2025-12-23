# ErrorManager Specification

> **Version:** V1.1 Feature  
> **Status:** Design Draft  
> **Updated:** December 2024

---

## Overview

ErrorManager is a unified error handling system that provides a single configuration point for entire DataFlow pipelines (Read → Transform → Write).

---

## Problem Statement

| Component | Current Error Handling |
|-----------|----------------------|
| **Read** | `IReaderErrorSink`, `ReaderErrorAction` |
| **Write** | None (throws on error) |
| **Transform** | None (exceptions propagate) |

This creates inconsistent error behavior across pipeline stages.

---

## Proposed API

### Core Interface

```csharp
public interface IErrorManager
{
    void ReportError(DataFlowError error);
    void ReportSuccess(object item, string stage);
    void StageCompleted(string stageName, StageMetrics metrics);
    
    ErrorAction DefaultAction { get; }
    bool ShouldContinue(DataFlowError error);
}
```

### Error Record

```csharp
public record DataFlowError
{
    public string Stage { get; init; }         // "Read", "Transform", "Write"
    public string Operation { get; init; }     // "CsvRead", "SelectCase", "WriteCsv"
    public Exception? Exception { get; init; }
    public object? Item { get; init; }
    public string? Message { get; init; }
    public DateTime Timestamp { get; init; }
}

public enum ErrorAction { Throw, Skip, Stop, Log }
```

### Fluent Builder

```csharp
var em = ErrorManager.Create()
    .WithAction(ErrorAction.Skip)
    .WithMaxErrors(100)
    .WithSink(new JsonLinesFileErrorSink("errors.ndjson"))
    .OnError(e => Log.Warning(e.Message))
    .Build();
```

### Stage-Specific Overrides

```csharp
var em = ErrorManager.Create()
    .WithDefaultAction(ErrorAction.Skip)
    .ForStage("Read", ErrorAction.Log)
    .ForStage("Write", ErrorAction.Throw)
    .Build();
```

---

## Usage

```csharp
var em = ErrorManager.Create().WithAction(ErrorAction.Skip).Build();

await Read.Csv<Order>("orders.csv", new CsvReadOptions { ErrorManager = em })
    .Select(order => Transform(order))
    .WriteCsv("output.csv", new CsvWriteOptions { ErrorManager = em });

Console.WriteLine($"Errors: {em.TotalErrors}");
```

---

## Unified Metrics

```csharp
public class ErrorManagerMetrics
{
    public long TotalItemsProcessed { get; }
    public long TotalErrors { get; }
    public Dictionary<string, long> ErrorsByStage { get; }
    public TimeSpan TotalDuration { get; }
}
```

---

## Backward Compatibility

- Existing `IReaderErrorSink` continues to work
- ErrorManager wraps sinks internally
- Old API: `new CsvReadOptions { ErrorSink = ... }` unchanged

---

## Implementation Notes

1. Add `ErrorManager` property to all Options classes
2. Internal: bridge to existing error handling
3. Transform integration via `.WithErrorManager()` extension

---

*See also: [V1.0 Prep](../../V1.0%20Prep/V1-Release-Plan.md) | [Roadmap](../Roadmap.md)*
