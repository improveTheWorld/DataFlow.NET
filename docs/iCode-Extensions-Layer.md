# iCode.Extensions Layer Documentation

The iCode.Extensions layer provides a comprehensive set of extension methods that enhance the functionality of built-in .NET types, particularly focusing on `IEnumerable<T>` manipulation, string processing, and utility operations.

## Overview

This layer follows the extension method pattern to add functionality to existing types without modifying their original implementation. It provides fluent APIs that enable method chaining and functional programming patterns.

## Core Components

### IEnumerableExtensions

The `IEnumerableExtensions` class provides the most comprehensive set of extension methods for data manipulation and processing.

#### Control Flow Extensions

##### `Until<T>(this IEnumerable<T> items, Func<bool> stopCondition)`
Processes items until a global condition becomes true.

**Parameters:**
- `items`: Source enumerable
- `stopCondition`: Function that returns true when processing should stop

**Returns:** `IEnumerable<T>` - Items processed before the condition was met

**Example:**
```csharp
var numbers = Enumerable.Range(1, 100);
var result = numbers.Until(() => DateTime.Now.Second > 30);
```

##### `Until<T>(this IEnumerable<T> items, Func<T, bool> stopCondition)`
Processes items until an item-specific condition is met.

**Parameters:**
- `items`: Source enumerable
- `stopCondition`: Function that evaluates each item

**Example:**
```csharp
var lines = Read.text("data.txt");
var beforeEnd = lines.Until(line => line.StartsWith("END"));
```

##### `Until<T>(this IEnumerable<T> items, Func<T, int, bool> stopCondition)`
Processes items until a condition involving the item and its index is met.

**Example:**
```csharp
var items = source.Until((item, index) => index >= 10 || item.Contains("STOP"));
```

##### `Until<T>(this IEnumerable<T> items, int lastItemIdx)`
Processes items up to a specific index.

**Example:**
```csharp
var first10 = items.Until(9); // Items 0-9
```

#### Action Extensions

##### `ForEach<T>(this IEnumerable<T> items, Action<T> action)`
Executes an action for each item while maintaining the enumerable chain.

**Example:**
```csharp
var processed = items
    .ForEach(item => Console.WriteLine($"Processing: {item}"))
    .Where(item => item.IsValid())
    .ForEach(item => logger.Log(item));
```

##### `ForEach<T>(this IEnumerable<T> items, Action<T, int> action)`
Executes an action with access to both item and index.

**Example:**
```csharp
items.ForEach((item, index) => 
    Console.WriteLine($"Item {index}: {item}"));
```

##### `Do<T>(this IEnumerable<T> items)`
Forces enumeration of the sequence without returning values.

**Example:**
```csharp
// Execute the entire pipeline
items
    .Select(Transform)
    .ForEach(Process)
    .Do(); // Trigger execution
```

##### `Do<T>(this IEnumerable<T> items, Action action)`
Executes an action for each enumeration step.

#### Aggregation Extensions

##### `Cumul<T>(this IEnumerable<T> sequence, Func<T?, T, T> cumulate)`
Performs cumulative operations on a sequence.

**Example:**
```csharp
var numbers = new[] { 1, 2, 3, 4, 5 };
var sum = numbers.Cumul((acc, curr) => acc + curr); // 15

var strings = new[] { "Hello", " ", "World", "!" };
var combined = strings.Cumul((acc, curr) => acc + curr); // "Hello World!"
```

##### `Cumul<T, TResult>(this IEnumerable<T> sequence, Func<TResult?, T, TResult> cumulate, TResult? initial)`
Performs cumulative operations with an initial value.

**Example:**
```csharp
var result = numbers.Cumul((acc, curr) => acc * curr, 1); // Product
```

##### `Sum<T>(this IEnumerable<T> items)`
Generic sum operation using dynamic typing.

**Example:**
```csharp
var intSum = intList.Sum();     // Works with int
var decimalSum = decimalList.Sum(); // Works with decimal
```

#### Utility Extensions

##### `MergeOrdered<T>(this IEnumerable<T> first, IEnumerable<T> second, Func<T, T, bool> isFirstLessThanOrEqualToSecond)`
Merges two ordered enumerables into a single ordered enumerable.

**Example:**
```csharp
var list1 = new[] { 1, 3, 5, 7 };
var list2 = new[] { 2, 4, 6, 8 };
var merged = list1.MergeOrdered(list2, (a, b) => a <= b);
// Result: [1, 2, 3, 4, 5, 6, 7, 8]
```

##### `Take<T>(this IEnumerable<T> sequence, int start, int count)`
Takes a specific range of items.

**Example:**
```csharp
var middle = items.Take(10, 5); // Items 10-14
```

##### `IsNullOrEmpty<T>(this IEnumerable<T> sequence)`
Checks if enumerable is null or empty.

**Example:**
```csharp
if (!items.IsNullOrEmpty())
{
    ProcessItems(items);
}
```

#### String Building Extensions

##### `BuildString(this IEnumerable<string> items, StringBuilder str = null, string separator = ", ", string before = "{", string after = "}")`
Builds formatted strings from string enumerables.

**Example:**
```csharp
var names = new[] { "Alice", "Bob", "Charlie" };
var formatted = names.BuildString(separator: ", ", before: "[", after: "]");
// Result: "[Alice, Bob, Charlie]"
```

### Cases Extension Pattern

The Cases pattern is one of the most powerful features of the Extensions layer, enabling conditional processing and routing of data through different transformation pipelines.

#### `Cases<T>(this IEnumerable<T> items, params Func<T, bool>[] filters)`
Categorizes items based on predicates.

**Returns:** `IEnumerable<(int category, T item)>` - Items tagged with category indices

**Example:**
```csharp
var numbers = Enumerable.Range(1, 10);
var categorized = numbers.Cases(
    n => n % 2 == 0,  // Even numbers (category 0)
    n => n % 3 == 0,  // Divisible by 3 (category 1)
    n => n > 5        // Greater than 5 (category 2)
    // Items not matching any predicate get category 3
);
```

#### `SelectCase<T, R>(this IEnumerable<(int category, T item)> items, params Func<T, R>[] selectors)`
Applies different transformations based on category.

**Example:**
```csharp
var processed = numbers
    .Cases(
        n => n % 2 == 0,
        n => n % 3 == 0
    )
    .SelectCase(
        n => $"Even: {n}",      // For category 0
        n => $"Div by 3: {n}",  // For category 1
        n => $"Other: {n}"      // For category 2 (default)
    );
```

#### `ForEachCase<T>(this IEnumerable<(int category, T item)> items, params Action<T>[] actions)`
Executes different actions based on category.

**Example:**
```csharp
logLines
    .Cases(
        line => line.Contains("ERROR"),
        line => line.Contains("WARNING")
    )
    .ForEachCase(
        line => errorLogger.Log(line),    // Errors
        line => warningLogger.Log(line),  // Warnings
        line => infoLogger.Log(line)      // Everything else
    );
```

#### `AllCases<T, R>(this IEnumerable<(int category, T item, R newItem)> items)`
Extracts transformed items from categorized enumerable.

**Example:**
```csharp
var results = items
    .Cases(predicate1, predicate2)
    .SelectCase(transform1, transform2, defaultTransform)
    .AllCases(); // Returns IEnumerable<R>
```

### Advanced Cases Patterns

#### Multi-stage Processing
```csharp
Read.text("log.txt")
    .Cases(
        line => line.Contains("ERROR"),
        line => line.Contains("WARNING"),
        line => line.Contains("INFO")
    )
    .SelectCase(
        line => $"[E] {DateTime.Now}: {line}",
        line => $"[W] {DateTime.Now}: {line}",
        line => $"[I] {DateTime.Now}: {line}",
        line => $"[?] {DateTime.Now}: {line}"
    )
    .ForEachCase(
        line => File.AppendAllText("errors.log", line + "\n"),
        line => File.AppendAllText("warnings.log", line + "\n"),
        line => File.AppendAllText("info.log", line + "\n"),
        line => File.AppendAllText("unknown.log", line + "\n")
    )
    .AllCases()
    .WriteText("processed.log");
```

#### Conditional Routing with Statistics
```csharp
int errorCount = 0, warningCount = 0, infoCount = 0;

var processed = logLines
    .Cases(
        line => line.Contains("ERROR"),
        line => line.Contains("WARNING")
    )
    .ForEachCase(
        line => errorCount++,
        line => warningCount++,
        line => infoCount++
    )
    .SelectCase(
        line => line.ToUpper(),
        line => line.ToLower(),
        line => line // Keep as-is for info
    )
    .AllCases();

Console.WriteLine($"Errors: {errorCount}, Warnings: {warningCount}, Info: {infoCount}");
```

### Deep Loop Extensions

#### `Flat<T>(this IEnumerable<IEnumerable<T>> items)`
Flattens nested enumerables into a single sequence.

**Example:**
```csharp
var nestedLists = new[]
{
    new[] { 1, 2, 3 },
    new[] { 4, 5 },
    new[] { 6, 7, 8, 9 }
};
var flattened = nestedLists.Flat(); // [1, 2, 3, 4, 5, 6, 7, 8, 9]
```

#### `Flat<T>(this IEnumerable<IEnumerable<T>> items, T endOfEnumerable)`
Flattens with separator between groups.

**Example:**
```csharp
var separated = nestedLists.Flat(-1); // [1, 2, 3, -1, 4, 5, -1, 6, 7, 8, 9, -1]
```

#### `Flat<T,R>(this IEnumerable<IEnumerable<T>> items, Func<IEnumerable<T>,R> group)`
Applies transformation to each group before flattening.

**Example:**
```csharp
var groupSums = nestedLists.Flat(group => group.Sum()); // [6, 9, 30]
```

### StringExtensions

The `StringExtensions` class provides utility methods for string manipulation and validation.

#### Validation Methods

##### `IsNullOrEmpty(this string text)`
Checks if string is null or empty.

**Example:**
```csharp
if (!input.IsNullOrEmpty())
{
    ProcessInput(input);
}
```

##### `IsNullOrWhiteSpace(this string text)`
Checks if string is null, empty, or contains only whitespace.

##### `IsBetween(this string text, string start, string end)`
Checks if string starts with one delimiter and ends with another.

**Example:**
```csharp
var isQuoted = text.IsBetween("\"", "\"");
var isParenthesized = text.IsBetween("(", ")");
```

#### Content Analysis Methods

##### `StartsWith(this string value, IEnumerable<string> acceptedStarts)`
Checks if string starts with any of the provided prefixes.

**Example:**
```csharp
var isCommand = input.StartsWith(new[] { "/help", "/quit", "/save" });
```

##### `ContainsAny(this string line, IEnumerable<string> tokens)`
Checks if string contains any of the specified tokens.

**Example:**
```csharp
var hasKeywords = text.ContainsAny(new[] { "error", "warning", "exception" });
```

#### Manipulation Methods

##### `ReplaceAt(this string value, int index, int length, string toInsert)`
Replaces substring at specific position.

**Example:**
```csharp
var original = "Hello World";
var modified = original.ReplaceAt(6, 5, "Universe"); // "Hello Universe"
```

##### `LastIdx(this string text)`
Gets the last valid index of the string.

**Example:**
```csharp
var lastChar = text[text.LastIdx()];
```

### Subpart Class

The `Subpart` class provides efficient substring operations without creating new string instances.

#### Creating Subparts

##### `SubPart(this string originalString, int startIndex, int endIndex)`
Creates a substring view without allocating new memory.

**Example:**
```csharp
var original = "Hello, World!";
var subpart = original.SubPart(7, 11); // "World"
```

#### Subpart Operations

##### `Trim(int start, int end)`
Trims characters from both ends.

##### `TrimStart(int steps)` / `TrimEnd(int steps)`
Trims characters from specific ends.

**Example:**
```csharp
var text = "  Hello World  ";
var subpart = text.SubPart(0, text.Length - 1)
    .TrimStart(2)  // Remove leading spaces
    .TrimEnd(2);   // Remove trailing spaces
// Result represents "Hello World"
```

### FileSystemExtensions

Extensions for file system operations with enhanced error handling.

#### `CreateFileWithoutFailure(this FilePath path, string renameSuffix = ".old")`
Creates a file safely, backing up existing files.

**Example:**
```csharp
var path = new FilePath("output.txt");
using var writer = path.CreateFileWithoutFailure(".backup");
```

#### `WriteInFile(this IEnumerable<string> lines, string path, string renamesuffix = ".old", int flusheach = -1)`
Writes enumerable to file with automatic backup.

**Example:**
```csharp
processedLines.WriteInFile("results.txt", ".old", flusheach: 1000);
```

#### `DerivateFileName(this string name, Func<string, string> derivate, bool keepExtension = true)`
Creates derived filenames based on transformation functions.

**Example:**
```csharp
var backupName = "data.txt".DerivateFileName(name => name + "_backup");
// Result: "data_backup.txt"

var timestamped = "log.txt".DerivateFileName(name => $"{name}_{DateTime.Now:yyyyMMdd}");
// Result: "log_20231215.txt"
```

### Spy and Display Extensions

Debug and visualization utilities for development and troubleshooting.

#### `Spy<T>(this IEnumerable<T> items, string tag, Func<T, string> customDisplay, bool timeStamp = false)`
Logs enumerable contents while maintaining the chain.

**Example:**
```csharp
var result = items
    .Where(x => x.IsValid())
    .Spy("After filtering", x => x.ToString(), timeStamp: true)
    .Select(x => x.Transform())
    .Spy("After transformation");
```

#### `Display(this IEnumerable<string> items, string tag = "Displaying")`
Outputs enumerable contents to console.

**Example:**
```csharp
processedData.Display("Final Results");
```

### Dictionary Extensions

#### `AddOrUpdate<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue value)`
Adds or updates dictionary entries.

**Returns:** `bool` - true if added, false if updated

**Example:**
```csharp
var cache = new Dictionary<string, int>();
bool wasNew = cache.AddOrUpdate("key1", 100);
```

#### `GetOrNull<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key)`
Gets value or returns null/default if key doesn't exist.

**Example:**
```csharp
var value = dictionary.GetOrNull("missing_key"); // Returns null instead of throwing
```

## Usage Patterns

### Data Processing Pipeline
```csharp
var result = Read.csv<Transaction>("transactions.csv")
    .Until(t => t.Date < DateTime.Today.AddMonths(-1)) // Last month only
    .Cases(
        t => t.Amount > 1000,     // Large transactions
        t => t.Amount < 0,        // Refunds
        t => t.Category == "Food" // Food purchases
    )
    .SelectCase(
        t => $"LARGE: {t.Description} - ${t.Amount:F2}",
        t => $"REFUND: {t.Description} - ${Math.Abs(t.Amount):F2}",
        t => $"FOOD: {t.Description} - ${t.Amount:F2}",
        t => $"OTHER: {t.Description} - ${t.Amount:F2}"
    )
    .ForEachCase(
        line => auditLog.WriteLine(line),    // Log large transactions
        line => refundLog.WriteLine(line),   // Log refunds
        line => expenseLog.WriteLine(line),  // Log food expenses
        line => { /* No special handling */ }
    )
    .AllCases()
    .BuildString(separator: "\n");
```

### Text Processing with Multiple Outputs
```csharp
var stats = new { ErrorCount = 0, WarningCount = 0, InfoCount = 0 };

Read.text("application.log")
    .Until(line => line.Contains("=== END OF LOG ==="))
    .Cases(
        line => line.ToUpper().Contains("ERROR"),
        line => line.ToUpper().Contains("WARNING")
    )
    .ForEachCase(
        line => { stats.ErrorCount++; errorWriter.WriteLine(line); },
        line => { stats.WarningCount++; warningWriter.WriteLine(line); },
        line => { stats.InfoCount++; infoWriter.WriteLine(line); }
    )
    .SelectCase(
        line => $"[{DateTime.Now:HH:mm:ss}] ERROR: {line}",
        line => $"[{DateTime.Now:HH:mm:ss}] WARNING: {line}",
        line => $"[{DateTime.Now:HH:mm:ss}] INFO: {line}"
    )
    .AllCases()
    .WriteText("processed_log.txt");

Console.WriteLine($"Processed {stats.ErrorCount} errors, {stats.WarningCount} warnings, {stats.InfoCount} info messages");
```

### Complex Data Transformation
```csharp
var processedData = Read.csv<SalesRecord>("sales.csv")
    .Where(record => record.Date >= DateTime.Today.AddDays(-30))
    .Cases(
        r => r.Region == "North",
        r => r.Region == "South",
        r => r.Region == "East",
        r => r.Region == "West"
    )
    .SelectCase(
        r => new { Region = "NORTH", Sales = r.Amount * 1.1m }, // North gets 10% bonus
        r => new { Region = "SOUTH", Sales = r.Amount * 1.05m }, // South gets 5% bonus
        r => new { Region = "EAST", Sales = r.Amount },
        r => new { Region = "WEST", Sales = r.Amount },
        r => new { Region = "UNKNOWN", Sales = r.Amount * 0.9m } // Unknown regions get penalty
    )
    .AllCases()
    .GroupBy(x => x.Region)
    .Select(g => new RegionSummary 
    { 
        Region = g.Key, 
        TotalSales = g.Sum(x => x.Sales),
        AverageSale = g.Average(x => x.Sales),
        Count = g.Count()
    })
    .OrderByDescending(x => x.TotalSales);
```

## Performance Considerations

### Memory Efficiency
- **Lazy Evaluation**: All extensions maintain lazy evaluation
- **Streaming**: Process large datasets without loading into memory
- **Minimal Allocations**: Efficient string and object handling

### Best Practices
1. **Chain Operations**: Combine multiple operations in single pipeline
2. **Avoid Early Materialization**: Don't call `.ToList()` unless necessary
3. **Use Appropriate Extensions**: Choose the right extension for your use case
4. **Handle Nulls**: Check for null values in predicates and transformations
5. **Dispose Resources**: Properly dispose streams and writers

### Common Pitfalls
- Calling `.ToList()` or `.ToArray()` in the middle of a pipeline
- Not handling null values in predicates
- Creating excessive intermediate collections
- Not disposing file streams properly

This completes the comprehensive documentation for the iCode.Extensions layer, covering all major extension methods, patterns, and usage scenarios.
