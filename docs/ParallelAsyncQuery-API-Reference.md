# ParallelAsyncQuery API Reference

ParallelAsyncQuery provides a fluent, LINQ-like API for parallel processing of asynchronous data streams. It enables high-throughput data transformations with configurable concurrency, ordering, and error handling.

## Overview

```csharp
// Basic usage - parallel Select with default settings
var results = await source
    .AsParallel()
    .Select(async item => await ProcessAsync(item))
    .ToList();

// Advanced usage with configuration
var results = await source
    .AsParallel()
    .WithMaxConcurrency(8)
    .WithOrderPreservation(true)
    .ContinueOnError()
    .Select(async item => await ProcessAsync(item))
    .Where(async item => await ValidateAsync(item))
    .ToList();
```

---

## Getting Started

### Creating a Parallel Query

```csharp
// From any IAsyncEnumerable<T>
IAsyncEnumerable<int> source = GetDataAsync();
ParallelAsyncQuery<int> query = source.AsParallel();

// With custom settings
var settings = new ParallelExecutionSettings
{
    MaxConcurrency = 4,
    PreserveOrder = true,
    OperationTimeout = TimeSpan.FromSeconds(30)
};
var query = source.AsParallel(settings);
```

---

## Configuration Methods

### `WithMaxConcurrency(int maxConcurrency)`
Limits the number of concurrent operations.

```csharp
source.AsParallel()
    .WithMaxConcurrency(4)  // At most 4 items processed simultaneously
```

**Default:** `Environment.ProcessorCount`  
**Range:** 1 to 100

---

### `WithOrderPreservation(bool preserve)`
Controls whether output order matches input order.

```csharp
// Preserve original order (slower but deterministic)
source.AsParallel().WithOrderPreservation(true)

// Allow any order (faster)
source.AsParallel().WithOrderPreservation(false)
```

**Default:** `true`

---

### `WithCancellation(CancellationToken token)`
Adds a cancellation token to stop processing.

```csharp
using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
source.AsParallel().WithCancellation(cts.Token)
```

---

### `WithTimeout(TimeSpan timeout)`
Sets timeout per individual operation.

```csharp
source.AsParallel()
    .WithTimeout(TimeSpan.FromSeconds(30))  // Each Select/Where has 30s max
```

**Default:** 5 minutes

---

### `WithBufferSize(int size)`
Controls internal channel buffer size.

```csharp
source.AsParallel().WithBufferSize(100)
```

**Default:** 1000  
**Range:** 10 to 10000

---

### `ContinueOnError()`
Skip failed items instead of throwing.

```csharp
source.AsParallel()
    .ContinueOnError()
    .Select(async x => {
        // If this throws, item is skipped (not failed)
        return await RiskyOperationAsync(x);
    })
```

**Default:** `false` (throws on first error)

---

## Operations

### `Select<TResult>(Func<T, Task<TResult>> selector)`
Transforms each item asynchronously in parallel.

```csharp
var results = await source.AsParallel()
    .Select(async item => await TransformAsync(item))
    .ToList();

// With index
var results = await source.AsParallel()
    .Select(async (item, index) => $"{index}: {await ProcessAsync(item)}")
    .ToList();
```

---

### `Where(Func<T, Task<bool>> predicate)`
Filters items asynchronously in parallel.

```csharp
var valid = await source.AsParallel()
    .Where(async item => await IsValidAsync(item))
    .ToList();
```

---

### `SelectMany<TResult>(Func<T, IAsyncEnumerable<TResult>> selector)`
Flattens nested sequences.

```csharp
var allItems = await source.AsParallel()
    .SelectMany(parent => GetChildrenAsync(parent))
    .ToList();
```

---

### `Take(int count)`
Limits output to first N items.

```csharp
var firstTen = await source.AsParallel()
    .WithOrderPreservation(true)
    .Take(10)
    .ToList();
```

---

## Execution Modes

### Default Mode
Parallel processing with automatic buffering.

### Sequential Mode
Processes items one at a time (useful for debugging).

```csharp
var settings = new ParallelExecutionSettings
{
    ExecutionMode = ParallelExecutionMode.Sequential
};
source.AsParallel(settings)
```

### Force Parallel Mode
Always use parallel paths even for small datasets.

```csharp
ExecutionMode = ParallelExecutionMode.ForceParallel
```

---

## Complete Example

```csharp
public async Task<List<ProcessedOrder>> ProcessOrdersAsync(
    IAsyncEnumerable<Order> orders,
    CancellationToken cancellationToken)
{
    return await orders
        .AsParallel()
        .WithMaxConcurrency(10)
        .WithOrderPreservation(true)
        .WithTimeout(TimeSpan.FromSeconds(30))
        .ContinueOnError()  // Skip failed orders
        
        // Validate orders in parallel
        .Where(async order => await _validator.ValidateAsync(order))
        
        // Enrich with customer data
        .Select(async order => {
            var customer = await _customerService.GetAsync(order.CustomerId);
            return new EnrichedOrder(order, customer);
        })
        
        // Process payment
        .Select(async enriched => {
            var result = await _paymentService.ProcessAsync(enriched);
            return new ProcessedOrder(enriched, result);
        })
        
        .ToList(cancellationToken);
}
```

---

## Performance Guidelines

| Scenario | Recommended Settings |
|----------|---------------------|
| I/O-bound (API calls, DB) | `MaxConcurrency = 20-50` |
| CPU-bound | `MaxConcurrency = ProcessorCount` |
| Mixed workload | `MaxConcurrency = 4-8` |
| Ordered output needed | `PreserveOrder = true` |
| Maximum throughput | `PreserveOrder = false` |
| Long operations | Increase `OperationTimeout` |

---

> [!IMPORTANT]
> ## Known Limitations
> 
> ### 1. Cancellation Token Propagation
> When cancelled, the operation may complete silently instead of throwing `OperationCanceledException`. 
> 
> **Workaround:** Check the token after enumeration:
> ```csharp
> await foreach (var item in query.WithCancellation(cts.Token)) { }
> cts.Token.ThrowIfCancellationRequested();  // Add this check
> ```
> 
> ### 2. Take() Doesn't Stop Upstream Processing
> Using `.Take(10)` on a parallel query still processes ALL upstream items, then returns only 10. This is due to the parallel nature - there's no backpressure to stop the producer.
> 
> ### 3. Chained Operations Multiply Concurrency
> Each chained operation (Select → Where → Select) has its own concurrency pool. Three operations with `MaxConcurrency=4` could result in up to 12 concurrent tasks.
> 
> ### 4. Memory Under Extreme Load
> When processing millions of items with high concurrency and order preservation, the reordering buffer may consume significant memory if items arrive very out-of-order.

---

## See Also

- [Stream Merging](Stream-Merging.md) - Combining multiple async streams
- [SUPRA Pattern](DataFlow-SUPRA-Pattern.md) - Overall architecture pattern
- [Extension Methods API](Extension-Methods-API-Reference.md) - Additional LINQ-like extensions
