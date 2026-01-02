# Parallel Processing in DataFlow.NET

DataFlow.NET provides two parallel execution models that integrate seamlessly with the core streaming architecture.

---

## Overview

| Type | Purpose | Source |
|------|---------|--------|
| **`ParallelQuery<T>`** | CPU-bound parallel work | PLINQ (built-in) |
| **`ParallelAsyncQuery<T>`** | I/O-bound async parallel work | DataFlow.NET custom |

Both maintain the **lazy, composable** nature of DataFlow.NET pipelines.

---

## ParallelQuery (PLINQ)

For synchronous parallel processing, DataFlow.NET extends the standard .NET `ParallelQuery<T>` with additional utility methods:

```csharp
var results = data
    .AsParallel()                           // Convert to ParallelQuery
    .Select(x => ExpensiveCompute(x))       // PLINQ built-in
    .ForEach(x => Log(x))                   // DataFlow extension (pass-through)
    .Do();                                  // DataFlow extension (terminal)
```

Extensions added by DataFlow.NET:
- `ForEach(Action<T>)` / `ForEach(Action<T,int>)` — lazy side-effects
- `Do()` / `Do(Action<T>)` — terminal execution
- `Take(start, count)` — convenience slice
- `BuildString(...)` — string aggregation
- `IsNullOrEmpty()` — utility check

---

## ParallelAsyncQuery (DataFlow.NET)

For asynchronous parallel processing (concurrent I/O, API calls, database writes), DataFlow.NET provides a fully custom `ParallelAsyncQuery<T>`:

```csharp
await asyncStream
    .AsParallel()                                   // Convert to ParallelAsyncQuery
    .WithMaxConcurrency(8)                          // Limit concurrent operations
    .WithOrderPreservation(true)                    // Optional: preserve order
    .Select(async item => await FetchAsync(item))  // Async projection
    .ForEach(async item => await SaveAsync(item))  // Async side-effects
    .Do();                                          // Terminal execution
```

Key features:
- **Proper `await` handling** — unlike PLINQ, async delegates are correctly awaited  
- **Configurable concurrency** — `WithMaxConcurrency(n)`  
- **Order preservation** — optional via `WithOrderPreservation(true)`  
- **Error handling** — `ContinueOnError()` to skip failures  
- **Full LINQ operators** — `Select`, `Where`, `Take`, `Aggregate`, etc.

---

## Lightweight Async LINQ Implementation

DataFlow.NET includes a **self-contained async LINQ implementation** in `AsyncLinqOperators.cs`. This avoids a dependency on `System.Linq.Async` (Ix.NET) while providing equivalent functionality:

- Query: `Select`, `SelectMany`, `Where`
- Slicing: `Take`, `Skip`, `Take(start, count)`
- Terminal: `First`, `FirstOrDefault`, `Count`, `Any`, `All`, `Aggregate`
- Materialization: `ToList`, `ToArray`, `ToDictionary`
- Set operations: `Distinct`, `Concat`, `Append`, `Prepend`
- Batching: `Buffer`, `Batch`

> [!TIP]
> For most use cases, the built-in async LINQ operators are sufficient. Consider `System.Linq.Async` only for advanced operators or performance tuning.

---

## API Reference

For the complete list of extension methods across all four paradigms:

**[Extension Methods API Reference →](Extension-Methods-API-Reference.md)**

| Paradigm | Extensions Class |
|----------|------------------|
| `IEnumerable<T>` | `EnumerableExtensions` |
| `IAsyncEnumerable<T>` | `AsyncEnumerableExtensions`, `AsyncLinqOperators` |
| `ParallelQuery<T>` | `ParallelQueryExtensions` |
| `ParallelAsyncQuery<T>` | `ParallelAsyncQueryExtensions` |

---

## When to Use Which

| Scenario | Recommended |
|----------|-------------|
| Sequential streaming (default) | `IAsyncEnumerable<T>` |
| CPU-intensive processing | `ParallelQuery<T>` |
| Concurrent I/O (API calls, DB) | `ParallelAsyncQuery<T>` |
| Cloud execution (Spark/Snowflake) | Use premium providers |
