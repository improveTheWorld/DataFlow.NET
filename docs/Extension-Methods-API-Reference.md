# DataFlow.NET Extension Methods API Reference

> **Version:** 1.0  
> **Last Updated:** December 20, 2025  
> **Documentation Status:** ✅ Complete

This document provides a comprehensive comparison of all extension methods available across the four DataFlow extension libraries. All methods are 100% API-consistent across paradigms unless otherwise noted.

---

## Library Overview

| Library | Target Type | Paradigm | Namespace |
|---------|-------------|----------|-----------|
| **DataFlow.Extensions.EnumerableExtensions** | `IEnumerable<T>` | Sync Sequential | `DataFlow.Extensions` |
| **DataFlow.Extensions.AsyncEnumerableExtensions** | `IAsyncEnumerable<T>` | Async Sequential | `DataFlow.Extensions` |
| **DataFlow.Extensions.ParallelQueryExtensions** | `ParallelQuery<T>` | Sync Parallel (PLINQ) | `DataFlow.Extensions` |
| **DataFlow.Extensions.ParallelAsyncQueryExtensions** | `ParallelAsyncQuery<T>` | Async Parallel | `DataFlow.Extensions` |

---

## 1. Cases Pattern — ✅ 100% Consistent

The core categorization pattern for conditional branching in streaming pipelines.

### Cases Methods

| Method | IEnumerable | IAsyncEnumerable | ParallelQuery | ParallelAsyncQuery |
|--------|:-----------:|:----------------:|:-------------:|:------------------:|
| `Cases<C,T>(categories[])` | ✅ | ✅ | ✅ | ✅ |
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
| `ForEachCase<T,R>(Action[])` with newItem | ✅ | ✅ | ✅ | ✅ |
| `ForEachCase<T,R>(Action<R>[])` with newItem | ✅ | ✅ | ✅ | ✅ |
| `ForEachCase<T,R>(Action<R,int>[])` with newItem | ✅ | ✅ | ✅ | ✅ |

### UnCase/AllCases Methods

| Method | IEnumerable | IAsyncEnumerable | ParallelQuery | ParallelAsyncQuery |
|--------|:-----------:|:----------------:|:-------------:|:------------------:|
| `UnCase<T>` | ✅ | ✅ | ✅ | ✅ |
| `UnCase<T,Y>` | ✅ | ✅ | ✅ | ✅ |
| `AllCases<T,R>` | ✅ | ✅ | ✅ | ✅ |
| `AllCases(string separator)` | ❌ | ✅ | ❌ | ❌ |

> **Note:** The string separator overload of `AllCases` is async-only (advanced string aggregation use case).

---

## 2. Core Extensions

### Merging & Slicing

| Method | IEnumerable | IAsyncEnumerable | ParallelQuery | ParallelAsyncQuery | Notes |
|--------|:-----------:|:----------------:|:-------------:|:------------------:|-------|
| `MergeOrdered<T>` | ✅ | ✅ | ❌ | ❌ | Sequential merge - use `.AsEnumerable().MergeOrdered()` for parallel |
| `Take(start, count)` | ✅ | ✅ | ✅ | ❌ | Convenience wrapper for Skip+Take |

### Conditional Termination

| Method | IEnumerable | IAsyncEnumerable | ParallelQuery | ParallelAsyncQuery | Notes |
|--------|:-----------:|:----------------:|:-------------:|:------------------:|-------|
| `Until(Func<bool>)` | ✅ | ✅ | ❌ | ❌ | Conflicts with parallel semantics |
| `Until(Func<T,bool>)` | ✅ | ✅ | ❌ | ❌ | Conflicts with parallel semantics |
| `Until(Func<T,int,bool>)` | ✅ | ✅ | ❌ | ❌ | Conflicts with parallel semantics |
| `Until(int lastIdx)` | ✅ | ✅ | ❌ | ❌ | Conflicts with parallel semantics |

> **Design Decision:** `Until` methods are intentionally omitted from parallel variants because early termination conflicts with parallel execution semantics. Use `.AsEnumerable()` / `.AsAsyncEnumerable()` first if needed.

### Side-Effect Pipeline (ForEach/Do)

| Method | IEnumerable | IAsyncEnumerable | ParallelQuery | ParallelAsyncQuery | Notes |
|--------|:-----------:|:----------------:|:-------------:|:------------------:|-------|
| `ForEach(Action<T>)` | ✅ | ✅ | ✅ | ✅ | Lazy, pass-through |
| `ForEach(Action<T,int>)` | ✅ | ✅ | ✅ | ✅ | Lazy, with index |
| `ForEach(Func<T,Task>)` | N/A | N/A | N/A | ✅ | Async action |
| `ForEach(Func<T,int,Task>)` | N/A | N/A | N/A | ✅ | Async with index |
| `Do()` | ✅ | ✅ | ✅ | ✅ | Terminal, no action |
| `Do(Action<T>)` | ✅ | ✅ | N/A | ✅ | Terminal + action |
| `Do(Action<T,int>)` | ✅ | ✅ | N/A | ✅ | Terminal + indexed action |

> **Pattern:** Use `ForEach(action)` for lazy side-effects in the middle of a pipeline. Use `Do()` or `Do(action)` to force execution at the end.

### String Building

| Method | IEnumerable | IAsyncEnumerable | ParallelQuery | ParallelAsyncQuery |
|--------|:-----------:|:----------------:|:-------------:|:------------------:|
| `BuildString(StringBuilder?, separator, before, after)` | ✅ | ✅ | ✅ | ✅ |
| `BuildString(separator, before, after)` | ✅ | ✅ | ✅ | N/A |

### Utility Methods

| Method | IEnumerable | IAsyncEnumerable | ParallelQuery | ParallelAsyncQuery | Notes |
|--------|:-----------:|:----------------:|:-------------:|:------------------:|-------|
| `IsNullOrEmpty<T>` | ✅ | ✅ | ✅ | ❌ | Terminal check |

### Aggregation (Parallel-Specific)

| Method | IEnumerable | IAsyncEnumerable | ParallelQuery | ParallelAsyncQuery | Notes |
|--------|:-----------:|:----------------:|:-------------:|:------------------:|-------|
| `Sum(int)` | N/A | N/A | ✅ | ✅ | Thread-safe via Interlocked |
| `Sum(long)` | N/A | N/A | ✅ | ✅ | Thread-safe via Interlocked |
| `Sum(float)` | N/A | N/A | ✅ | N/A | Uses lock for thread-safety |
| `Sum(decimal)` | N/A | N/A | ✅ | N/A | Uses lock for thread-safety |

---

## 3. Debugging Extensions

| Method | IEnumerable | IAsyncEnumerable | ParallelQuery | ParallelAsyncQuery |
|--------|:-----------:|:----------------:|:-------------:|:------------------:|
| `Spy(tag)` | ✅ | ✅ | ✅ | ✅ |
| `Spy(tag, customFormatter)` | ✅ | ✅ | ✅ | ✅ |
| `Display(tag)` | ✅ | ✅ | ✅ | ❌ |
| `ToLines(separator)` | ✅ | ✅ | ❌ | ❌ |

---

## 4. Flattening Extensions

| Method | IEnumerable | IAsyncEnumerable | ParallelQuery | ParallelAsyncQuery | Notes |
|--------|:-----------:|:----------------:|:-------------:|:------------------:|-------|
| `Flatten<T>()` | ✅ | ✅ (3 variants) | ❌ | ❌ | Already flat |
| `Flatten<T>(separator)` | ✅ | ✅ (3 variants) | ❌ | ❌ | Already flat |

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
| `TryGetNext(out T)` | ✅ `bool` | ✅ `Task<(bool,T?)>` | Advance + get |
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
| `Throttle(intervalMs)` | Rate-limited async emission (ms overload) |

---

## 7. Async-Only LINQ Extensions

These methods exist only in `AsyncEnumerableDebuggingExtensions`:

| Method | Description |
|--------|-------------|
| `SelectMany<T,R>(selector)` | Async flattening projection |
| `SelectMany<T,C,R>(collectionSelector, resultSelector)` | Async flattening with result selector |
| `Distinct<T>(comparer?)` | Distinct elements with optional comparer |
| `Concat<T>(second)` | Concatenate two async sequences |
| `Append<T>(element)` | Append single element |
| `Prepend<T>(element)` | Prepend single element |
| `Aggregate<T>(func)` | Reduce to single value |

---

## Summary Matrix

| Category | IEnumerable | IAsyncEnumerable | ParallelQuery | ParallelAsyncQuery |
|----------|:-----------:|:----------------:|:-------------:|:------------------:|
| **Cases Pattern** | ✅ 100% | ✅ 100% | ✅ 100% | ✅ 100% |
| **Core Extensions** | ✅ Full | ✅ Full | ⚠️ No Until | ⚠️ No Until/Take |
| **Debugging** | ✅ Full | ✅ Full | ⚠️ Limited | ⚠️ Limited |
| **Flattening** | ✅ 2 | ✅ 6 | N/A | N/A |
| **Enumerator** | ✅ 2 | ✅ 2 | N/A | N/A |
| **Aggregation** | N/A | N/A | ✅ 4 | ✅ 2 |

---

## Usage Notes

### Thread Safety
- All parallel extension delegates may execute concurrently
- **Must ensure captured state is thread-safe**
- Console output in `Spy` is serialized via internal locks

### Lazy Execution
- All methods returning `IEnumerable<T>`, `IAsyncEnumerable<T>`, `ParallelQuery<T>`, or `ParallelAsyncQuery<T>` are **lazy**
- Methods returning `void` or `Task` are **terminal** (eager)
- `Display()` is eager (forces enumeration)
- `Do()` and `Do(action)` are eager (forces enumeration + optional action)

### Ordering
- `ParallelQuery` and `ParallelAsyncQuery` do not preserve order by default
- Use `.AsOrdered()` or `.WithOptions(preserveOrder: true)` if ordering matters
