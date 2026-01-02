# DataFlow.NET Extension Methods API Reference

> **Version:** 1.0  
> **Last Updated:** January 1, 2026

This document provides a reference for all extension methods available across the DataFlow extension libraries, organized by functionality and target type.

---

## Library Overview

| Library | Target Type | Paradigm | Namespace |
|---------|-------------|----------|-----------|
| `DataFlow.Extensions.EnumerableExtensions` | `IEnumerable<T>` | Synchronous Sequential | `DataFlow.Extensions` |
| `DataFlow.Extensions.AsyncEnumerableExtensions` | `IAsyncEnumerable<T>` | Asynchronous Sequential | `DataFlow.Extensions` |
| `DataFlow.Extensions.ParallelQueryExtensions` | `ParallelQuery<T>` | Synchronous Parallel (PLINQ) | `DataFlow.Extensions` |
| `DataFlow.Extensions.ParallelAsyncQueryExtensions` | `ParallelAsyncQuery<T>` | Asynchronous Parallel | `DataFlow.Extensions` |

---

## 1. Cases Pattern

The categorization pattern for conditional branching in streaming pipelines.

### Cases Methods

| Method | IEnumerable | IAsyncEnumerable | ParallelQuery | ParallelAsyncQuery |
|--------|:-----------:|:----------------:|:-------------:|:------------------:|
| `Cases<C,T>((C,T) items, categories[])` | ✅ | ✅ | ✅ | ✅ |
| `Cases<T>(predicates[])` | ✅ | ✅ | ✅ | ✅ |

### SelectCase Methods

| Method | IEnumerable | IAsyncEnumerable | ParallelQuery | ParallelAsyncQuery |
|--------|:-----------:|:----------------:|:-------------:|:------------------:|
| `SelectCase<T,R>(Func<T,R>[])` | ✅ | ✅ | ✅ | ✅ |
| `SelectCase<T,R>(Func<T,int,R>[])` | ✅ | ✅ | ✅ | ✅ |
| `SelectCase<T,R,Y>(Func<R,Y>[])` | ✅ | ✅ | ✅ | ✅ |
| `SelectCase<T,R,Y>(Func<R,int,Y>[])` | ✅ | ✅ | ✅ | ✅ |

### ForEachCase Methods

| Method | IEnumerable | IAsyncEnumerable | ParallelQuery | ParallelAsyncQuery |
|--------|:-----------:|:----------------:|:-------------:|:------------------:|
| `ForEachCase(Action[])` | ✅ | ✅ | ✅ | ✅ |
| `ForEachCase(Action<T>[])` | ✅ | ✅ | ✅ | ✅ |
| `ForEachCase(Action<T,int>[])` | ✅ | ✅ | ✅ | ✅ |
| `ForEachCase<T,R>(Action[])` | ✅ | ✅ | ✅ | ✅ |
| `ForEachCase<T,R>(Action<R>[])` | ✅ | ✅ | ✅ | ✅ |
| `ForEachCase<T,R>(Action<R,int>[])` | ✅ | ✅ | ✅ | ✅ |

### UnCase / AllCases Methods

| Method | IEnumerable | IAsyncEnumerable | ParallelQuery | ParallelAsyncQuery |
|--------|:-----------:|:----------------:|:-------------:|:------------------:|
| `UnCase<T>` | ✅ | ✅ | ✅ | ✅ |
| `UnCase<T,Y>` | ✅ | ✅ | ✅ | ✅ |
| `AllCases<T,R>` | ✅ | ✅ | ✅ | ✅ |


---

## 2. Core Extensions

### Merging & Slicing

| Method | IEnumerable | IAsyncEnumerable | ParallelQuery | ParallelAsyncQuery | Notes |
|--------|:-----------:|:----------------:|:-------------:|:------------------:|-------|
| `MergeOrdered<T>` | ✅ | ✅ | ❌ | ❌ | Sequential merge only |
| `Take(start, count)` | ✅ | ✅ | ✅ | ❌ | Convenience wrapper for Skip+Take |

### Conditional Termination

| Method | IEnumerable | IAsyncEnumerable | ParallelQuery | ParallelAsyncQuery | Notes |
|--------|:-----------:|:----------------:|:-------------:|:------------------:|-------|
| `Until(Func<bool>)` | ✅ | ✅ | ❌ | ❌ | Conflicts with parallel semantics |
| `Until(Func<T,bool>)` | ✅ | ✅ | ❌ | ❌ | Conflicts with parallel semantics |
| `Until(Func<T,int,bool>)` | ✅ | ✅ | ❌ | ❌ | Conflicts with parallel semantics |
| `Until(int lastIdx)` | ✅ | ✅ | ❌ | ❌ | Conflicts with parallel semantics |

### Side-Effect Pipeline

| Method | IEnumerable | IAsyncEnumerable | ParallelQuery | ParallelAsyncQuery | Notes |
|--------|:-----------:|:----------------:|:-------------:|:------------------:|-------|
| `ForEach(Action<T>)` | ✅ | ✅ | ✅ | ✅ | Lazy, pass-through |
| `ForEach(Action<T,int>)` | ✅ | ✅ | ✅ | ✅ | Lazy, with index |
| `ForEach(Func<T,Task>)` | ❌ | ❌ | ❌ | ✅ | Async action |
| `ForEach(Func<T,int,Task>)` | ❌ | ❌ | ❌ | ✅ | Async with index |
| `Do()` | ✅ | ✅ | ✅ | ✅ | Terminal, no action |
| `Do(Action<T>)` | ✅ | ✅ | ✅ | ✅ | Terminal with action |
| `Do(Action<T,int>)` | ✅ | ✅ | ✅ | ✅ | Terminal with indexed action |


### String Building

| Method | IEnumerable | IAsyncEnumerable | ParallelQuery | ParallelAsyncQuery |
|--------|:-----------:|:----------------:|:-------------:|:------------------:|
| `BuildString(StringBuilder?, separator, before, after)` | ✅ | ✅ | ✅ | ✅ |
| `BuildString(separator, before, after)` | ✅ | ✅ | ✅ | ✅ |


### Utility Methods

| Method | IEnumerable | IAsyncEnumerable | ParallelQuery | ParallelAsyncQuery |
|--------|:-----------:|:----------------:|:-------------:|:------------------:|
| `IsNullOrEmpty<T>` | ✅ | ✅ | ✅ | ✅ |


### Aggregation (Parallel-Specific)

| Method | IEnumerable | IAsyncEnumerable | ParallelQuery | ParallelAsyncQuery | Notes |
|--------|:-----------:|:----------------:|:-------------:|:------------------:|-------|
| `Sum(int)` | ❌ | ❌ | ✅ | ✅ | Thread-safe via Interlocked |
| `Sum(long)` | ❌ | ❌ | ✅ | ✅ | Thread-safe via Interlocked |
| `Sum(float)` | ❌ | ❌ | ✅ | ❌ | Uses lock for thread-safety |
| `Sum(decimal)` | ❌ | ❌ | ✅ | ❌ | Uses lock for thread-safety |

---

## 3. Debugging Extensions

| Method | IEnumerable | IAsyncEnumerable | ParallelQuery | ParallelAsyncQuery |
|--------|:-----------:|:----------------:|:-------------:|:------------------:|
| `Spy(tag)` | ✅ | ✅ | ✅ | ✅ |
| `Spy(tag, customFormatter)` | ✅ | ✅ | ✅ | ✅ |
| `Display(tag)` | ✅ | ✅ | ✅ | ✅ |
| `ToLines(separator)` | ✅ | ✅ | ❌ | ❌ |

---

## 4. Flattening Extensions

| Method | IEnumerable | IAsyncEnumerable | ParallelQuery | ParallelAsyncQuery |
|--------|:-----------:|:----------------:|:-------------:|:------------------:|
| `Flatten<T>()` | ✅ | ✅ (3 variants) | ❌ | ❌ |
| `Flatten<T>(separator)` | ✅ | ✅ (3 variants) | ❌ | ❌ |

### IAsyncEnumerable Flatten Variants

| Variant | Description |
|---------|-------------|
| `IAsyncEnumerable<IAsyncEnumerable<T>>.Flatten()` | Async-of-async flattening |
| `IAsyncEnumerable<IEnumerable<T>>.Flatten()` | Async-of-sync flattening |
| `IEnumerable<IAsyncEnumerable<T>>.Flatten()` | Sync-of-async flattening |

---

## 5. Enumerator Extensions

| Method | IEnumerator | IAsyncEnumerator | Notes |
|--------|:-----------:|:----------------:|-------|
| `TryGetNext(out T)` | ✅ `bool` | ✅ `Task<(bool,T?)>` | Advance and retrieve |
| `GetNext()` | ✅ `T?` | ✅ `Task<T?>` | Nullable result |

---

## 6. Sync→Async Conversion (IEnumerable Only)

| Method | Description |
|--------|-------------|
| `Async(yieldThresholdMs)` | Cooperative async wrapper with periodic yielding |
| `BufferAsync(yieldThresholdMs, runOnBackground)` | Channel-based buffering with optional background thread |
| `WithBoundedBuffer(options)` | Backpressure for IAsyncEnumerable via bounded channel |
| `WithBoundedBuffer(capacity, fullMode)` | Convenience overload |
| `Throttle(TimeSpan)` | Rate-limited async emission |
| `Throttle(intervalMs)` | Rate-limited async emission (milliseconds overload) |

---

## 7. Async LINQ Operators

DataFlow.NET includes lightweight reimplementations of common `System.Linq.Async` operators in `AsyncLinqOperators.cs` to reduce external dependencies.

### Query Operators

| Method | Overloads | Description |
|--------|-----------|-------------|
| `Select<T,R>` | 2 | Project elements (with/without index) |
| `SelectMany<T,R>` | 4 | Flatten nested sequences |
| `Where<T>` | 3 | Filter elements |
| `Distinct<T>` | 1 | Remove duplicates |
| `Concat<T>` | 1 | Concatenate two sequences |
| `Append<T>` | 1 | Add element at end |
| `Prepend<T>` | 1 | Add element at beginning |

### Slicing Operators

| Method | Overloads | Description |
|--------|-----------|-------------|
| `Take<T>` | 5 | Take count, range, slice, or while predicate is true |
| `Skip<T>` | 2 | Skip count or while predicate is true |
| `SkipWhile<T>` | 2 | Skip while predicate is true |

### Terminal Operators

| Method | Overloads | Description |
|--------|-----------|-------------|
| `First<T>` | 3 | First element (throws if empty) |
| `FirstOrDefault<T>` | 3 | First element or default |
| `Any<T>` | 3 | Check if any element exists |
| `Aggregate<T>` | 2 | Reduce sequence to single value |

### Materialization Operators

| Method | Description |
|--------|-------------|
| `ToList<T>` | Materialize to `List<T>` |
| `ToArray<T>` | Materialize to `T[]` |
| `ToDictionary<T,K,V>` | Materialize to `Dictionary<K,V>` |

### Batching

| Method | Description |
|--------|-------------|
| `Buffer<T>(size)` | Split into fixed-size batches |

---

## Summary Matrix

| Category | IEnumerable | IAsyncEnumerable | ParallelQuery | ParallelAsyncQuery |
|----------|:-----------:|:----------------:|:-------------:|:------------------:|
| Cases Pattern | ✅ Full | ✅ Full | ✅ Full | ✅ Full |
| Core Extensions | ✅ Full | ✅ Full | ⚠️ Partial | ⚠️ Partial |
| Debugging | ✅ Full | ✅ Full | ⚠️ Partial | ⚠️ Partial |
| Flattening | ✅ 2 methods | ✅ 6 methods | ❌ | ❌ |
| Enumerator | ✅ 2 methods | ✅ 2 methods | ❌ | ❌ |
| Aggregation | ❌ | ❌ | ✅ 4 methods | ✅ 2 methods |


---

## Usage Notes

### Thread Safety
- All parallel extension delegates may execute concurrently
- Captured state must be thread-safe
- Console output in `Spy` is serialized via internal locks

### Lazy Execution
- Methods returning `IEnumerable<T>`, `IAsyncEnumerable<T>`, `ParallelQuery<T>`, or `ParallelAsyncQuery<T>` are lazy
- Methods returning `void` or `Task` are terminal (eager)
- `Display()` and `Do()` force enumeration

### Ordering
- `ParallelQuery` and `ParallelAsyncQuery` do not preserve order by default
- Use `.AsOrdered()` or `.WithOptions(preserveOrder: true)` if ordering is required

---

## Appendix A: Async Side-Effects in Parallel Pipelines

A common question is why `ForEach(Func<T, Task>)` is available for **ParallelAsyncQuery** but ❌ **NOT** for standard **ParallelQuery**.

### The Limitation of PLINQ (ParallelQuery)
Standard PLINQ is designed for **synchronous** CPU-bound parallelism. It expects delegates to return `void` or a result immediately.
- If you pass an `async` lambda to `ForEach(Action<T>)`, it becomes an `async void` delegate.
- **Consequence:** The pipeline will **not wait** for the asynchronous operation to complete. It will fire the task and immediately move to the next item.
- **Risk:** Exceptions in `async void` methods can crash the entire process, and valid tasks may be cancelled prematurely when the main thread exits.

### The Solution (ParallelAsyncQuery)
`ParallelAsyncQuery` was specifically architected to handle **asynchronous** I/O-bound parallelism.
- It accepts `Func<T, Task>`, properly `awaits` each concurrent task, and manages concurrency limits (degree of parallelism).
- This ensures that the pipeline **waits** for all side-effects (e.g., database writes, API calls) to complete before finishing.

**Rule of Thumb:**
- Use `ParallelQuery` for CPU-intensive work (computations).
- Use `ParallelAsyncQuery` for I/O-intensive work (network/disk operations).
