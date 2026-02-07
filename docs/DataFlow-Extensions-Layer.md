# DataFlow.Extensions Layer Documentation

The DataFlow.Extensions layer provides extension methods for data manipulation, encompassing both `IEnumerable<T>` and `IAsyncEnumerable<T>` with unified patterns.

## Table of Contents

1. [Extension Projects](#extension-projects)
2. [Cases/SelectCase Pattern](#casesselectcase-pattern)
3. [Control Flow Extensions](#control-flow-extensions)
4. [Aggregation Extensions](#aggregation-extensions)
5. [Flattening Extensions](#flattening-extensions)
6. [String Extensions](#string-extensions)
7. [Debugging Extensions](#debugging-extensions)
8. [Async-Specific Extensions](#async-specific-extensions)
9. [Dictionary Extensions](#dictionary-extensions)
10. [File System Extensions](#file-system-extensions)

---

| Project | Description |
|---------|-------------|
| `EnumerableExtensions` | Core sync extensions (Cases, Until, ForEach, etc.) |
| `AsyncEnumerableExtensions` | Async variants of core extensions |
| `ArrayExtensions` | Array manipulation utilities |
| `DictionaryExtensions` | Dictionary helpers |
| `FileSystemExtensions` | File path and IO utilities |
| `StringExtensions` | String manipulation |
| `RegexTokenizerExtensions` | Regex pattern matching |

---

## Cases/SelectCase Pattern

The Cases pattern is the core processing feature enabling conditional routing through transformation pipelines.

### Core Methods

| Method | Input | Output |
|--------|-------|--------|
| `Cases()` | `IEnumerable<T>` | `IEnumerable<(int, T)>` |
| `SelectCase()` | `(int, T)` | `(int, T, R)` |
| `ForEachCase()` | `(int, T, R)` | `(int, T, R)` |
| `AllCases()` | `(int, T, R)` | `IEnumerable<R>` |

### Basic Usage

```csharp
var results = numbers
    .Cases(
        n => n % 2 == 0,  // Category 0: Even
        n => n % 3 == 0   // Category 1: Divisible by 3
        // Category 2: Everything else (supra)
    )
    .SelectCase(
        n => $"Even: {n}",
        n => $"Div3: {n}",
        n => $"Other: {n}"  // Supra category
    )
    .AllCases();
```

### With Side Effects

```csharp
await Read.Csv<LogEntry>("logs.csv")
    .Cases(
        log => log.Level == "ERROR",
        log => log.Level == "WARNING"
    )
    .ForEachCase(
        error => errorLogger.Log(error),
        warning => warningLogger.Log(warning),
        info => infoLogger.Log(info)
    )
    .SelectCase(
        error => $"[E] {error.Message}",
        warning => $"[W] {warning.Message}",
        info => $"[I] {info.Message}"
    )
    .AllCases()
    .WriteText("processed.log");
```

### Async Cases Pattern

All Cases methods have async variants:

```csharp
await liveLogStream
    .Cases(log => log.Level == "ERROR", log => log.Level == "WARNING")
    .SelectCase(ProcessError, ProcessWarning, ProcessInfo)
    .ForEachCase(
        async error => await alertSystem.SendAsync(error),
        async warning => await logger.WarnAsync(warning),
        async info => { }
    )
    .AllCases()
    .WriteText("output.txt");
```

---

## Control Flow Extensions

### Until

Stop enumeration when a condition is met. **`Until` is inclusive** — the item that triggers the stop condition is included in the output (like `do-while` vs `while`). This differs from LINQ's `TakeWhile`, which is exclusive.

```csharp
// Stop when condition is true — the matching item IS included
var beforeEnd = lines.Until(line => line.StartsWith("END"));
// → includes the "END..." line itself

// Take items up to and including index 9 (= first 10 items)
var first10 = items.Until(9);

// Index-based condition
var result = items.Until((item, index) => index >= 10 || item.Contains("STOP"));
```

> **`Until` vs `TakeWhile`:**  
> `items.TakeWhile(x => x < 5)` → `[1, 2, 3, 4]` (exclusive, stops before 5)  
> `items.Until(x => x >= 5)` → `[1, 2, 3, 4, 5]` (inclusive, includes the 5)

### ForEach

Execute side-effects while maintaining the chain:

```csharp
var processed = items
    .ForEach(item => Console.WriteLine($"Processing: {item}"))
    .Where(item => item.IsValid())
    .ForEach(item => logger.Log(item));
```

### Do

Force enumeration without returning values:

```csharp
items
    .Select(Transform)
    .ForEach(Process)
    .Do(); // Trigger execution
```

---

## Aggregation Extensions

### Cumul

Cumulative operations on sequences:

```csharp
var numbers = new[] { 1, 2, 3, 4, 5 };
var sum = numbers.Cumul((acc, curr) => acc + curr); // 15

var strings = new[] { "Hello", " ", "World" };
var combined = strings.Cumul((acc, curr) => acc + curr); // "Hello World"
```

### MergeOrdered

Merge two sorted sequences:

```csharp
var list1 = new[] { 1, 3, 5, 7 };
var list2 = new[] { 2, 4, 6, 8 };
var merged = list1.MergeOrdered(list2, (a, b) => a <= b);
// Result: [1, 2, 3, 4, 5, 6, 7, 8]
```

---

## Flattening Extensions

### Flat

Flatten nested enumerables:

```csharp
var nested = new[] {
    new[] { 1, 2, 3 },
    new[] { 4, 5 },
    new[] { 6, 7, 8 }
};

// Simple flatten
var flat = nested.Flat(); // [1, 2, 3, 4, 5, 6, 7, 8]

// With separator
var separated = nested.Flat(-1); // [1, 2, 3, -1, 4, 5, -1, 6, 7, 8, -1]

// With transformation
var sums = nested.Flat(group => group.Sum()); // [6, 9, 21]
```

---

## String Extensions

### Validation

```csharp
if (!text.IsNullOrEmpty())
    ProcessText(text);

var isQuoted = text.IsBetween("\"", "\"");
var isCommand = input.StartsWith(new[] { "/help", "/quit", "/save" });
var hasKeywords = text.ContainsAny(new[] { "error", "warning" });
```

### Manipulation

```csharp
// Replace at position
var modified = "Hello World".ReplaceAt(6, 5, "Universe"); // "Hello Universe"

// Get last index
var lastChar = text[text.LastIdx()];
```

---

## Debugging Extensions

### Spy

Inspect items flowing through a pipeline:

```csharp
var result = items
    .Where(x => x.IsValid())
    .Spy("After filtering", x => x.ToString(), timeStamp: true)
    .Select(x => x.Transform())
    .Spy("After transformation");
```

### Display

Output to console:

```csharp
processedData.Display("Final Results");
```

---

## Async-Specific Extensions

### Async Conversion

```csharp
// Convert sync to async with yielding
var asyncData = syncData.Async(yieldThresholdMs: 15);

// With buffering
var buffered = syncData.BufferAsync(
    yieldThresholdMs: 15,
    runOnBackgroundThread: true
);
```

### Bounded Buffering

```csharp
var bufferedStream = source.WithBoundedBuffer(
    capacity: 500,
    fullMode: BoundedChannelFullMode.Wait
);
```

---

## Dictionary Extensions

```csharp
// Add or update
bool wasNew = cache.AddOrUpdate("key", 100);

// Get or null (no exception)
var value = dictionary.GetOrNull("missing_key");
```

---

## File System Extensions

```csharp
// Create file with backup
using var writer = path.CreateFileWithoutFailure(".backup");

// Write with auto-backup and flush interval
lines.WriteInFile("results.txt", ".old", flusheach: 1000);

// Derive filename
var backupName = "data.txt".DerivateFileName(name => name + "_backup");
// Result: "data_backup.txt"
```

---

## Performance Best Practices

1. **Chain operations**: Combine multiple operations in single pipeline
2. **Avoid early materialization**: Don't call `.ToList()` unless necessary
3. **Use async variants**: For I/O-bound operations
4. **Handle nulls**: Check for null values in predicates
5. **Use Supra Category**: Let unmatched items flow through gracefully

---

## See Also

- [Cases-Pattern.md](Cases-Pattern.md) — Full Cases pattern documentation
- [Stream-Merging.md](Stream-Merging.md) — Multi-source streaming
- [DataFlow-Data-Layer.md](DataFlow-Data-Layer.md) — Read/Write operations
