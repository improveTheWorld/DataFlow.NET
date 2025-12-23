# DataFlow.Framework Layer Documentation

The DataFlow.Framework layer provides core infrastructure components: async stream merging, defensive programming utilities, regular expression helpers, and syntax parsing.

## Table of Contents

1. [Framework Projects](#framework-projects)
2. [AsyncEnumerable<T> — Stream Merging](#asyncenumerablet--stream-merging)
3. [Guard Class — Defensive Programming](#guard-class--defensive-programming)
4. [RegexTokenizer — Pattern Matching](#regextokenizer--pattern-matching)
5. [EnumerableWithNote<T, P> — Context Preservation](#enumerablewithnotetp--context-preservation)
6. [DataPublisher<T> — Pub/Sub Pattern](#datapublishert--pubsub-pattern)
7. [Syntaxi — Grammar Parsing](#syntaxi--grammar-parsing)

---

| Project | Description |
|---------|-------------|
| `AsyncEnumerable` | Multi-source stream merging |
| `Guard` | Defensive programming utilities |
| `RegexTokenizer` | Pattern matching and tokenization |
| `EnumerablePlus` | Context-aware enumerables |
| `WatchedValue` | Observable value wrappers |
| `SparkQuery` | LINQ-to-Spark translation |
| `DataFlow` | Pub/sub pattern implementation |
| `Syntaxi` | Grammar and token parsing |

---

## AsyncEnumerable<T> — Stream Merging

The unified multi-source stream merger for `IAsyncEnumerable<T>`.

### Configuration

```csharp
public enum UnifyErrorMode
{
    FailFast,          // Any source error ends the stream (default)
    ContinueOnError    // Drop failing source, continue with others
}

public enum UnifyFairness
{
    FirstAvailable,    // Yield whichever source produces next (default)
    RoundRobin         // Best-effort fairness across sources
}

public sealed class UnifyOptions
{
    public UnifyErrorMode ErrorMode { get; init; }
    public UnifyFairness Fairness { get; init; }
}
```

### Usage

```csharp
var unified = new AsyncEnumerable<LogEntry>(new UnifyOptions
{
    ErrorMode = UnifyErrorMode.ContinueOnError,
    Fairness = UnifyFairness.RoundRobin
})
.Unify(webServerLogs, "web")
.Unify(databaseLogs, "db", log => log.Level >= LogLevel.Warning)
.Unify(authServiceLogs, "auth");

// Process unified stream
await foreach (var log in unified)
{
    Console.WriteLine($"[{log.Source}] {log.Message}");
}
```

### Key Methods

| Method | Description |
|--------|-------------|
| `Unify(source, name, predicate?)` | Add source (before enumeration) |
| `Unlisten(name)` | Remove source (before enumeration) |
| `GetAsyncEnumerator()` | Start enumeration (freezes sources) |

> [!NOTE]
> For full streaming documentation, see [Stream-Merging.md](Stream-Merging.md).

---

## Guard Class — Defensive Programming

Argument validation utilities for fail-fast error detection.

### Methods

```csharp
// Null checks
Guard.AgainstNullArgument(nameof(data), data);
Guard.AgainstNullArgumentProperty(nameof(user), nameof(user.Email), user.Email);
Guard.AgainstNullArgumentIfNullable(nameof(value), value);

// Range checks
Guard.AgainstOutOfRange(nameof(index), index, 0, array.Length - 1);

// Contract validation
Guard.AgainstContractNotRespected(nameof(start), nameof(end));
```

### Example

```csharp
public class DataProcessor
{
    public async Task ProcessAsync(string path, ProcessingOptions options)
    {
        Guard.AgainstNullArgument(nameof(path), path);
        Guard.AgainstNullArgument(nameof(options), options);
        Guard.AgainstNullArgumentProperty(nameof(options), nameof(options.OutputPath), options.OutputPath);
        Guard.AgainstOutOfRange(nameof(options.BatchSize), options.BatchSize, 1, 10000);
        
        // Safe to use arguments here
    }
}
```

---

## RegexTokenizer — Pattern Matching

Simplified regular expression pattern building and text tokenization.

### Regex Constants

```csharp
using static DataFlow.Framework.Regex;

// Single character patterns
CHAR          // .     Any character
SPACE         // \s    Single whitespace
ALPHNUM       // \w    Alphanumeric
NUM           // \d    Single digit
ALPHA         // [a-zA-Z]

// Quantified patterns
ANY_CHARS     // .*    Any characters
SPACES        // \s+   One or more spaces
MAYBE_SPACES  // \s*   Zero or more spaces
ALPHNUMS      // \w+   One or more alphanumeric
NUMS          // \d+   One or more digits
ALPHAS        // [a-zA-Z]+
```

### Pattern Building

```csharp
// Named capture groups
string pattern = NUMS.As("timestamp") + SPACES + ALPHAS.As("level");
// Result: "(?<timestamp>\d+)\s+(?<level>[a-zA-Z]+)"

// Quantifiers
NUMS.Many(2, 4)   // \d{2,4}
ALPHAS.Any()      // [a-zA-Z]*
WORD.MayBe()      // (\s*\w+\s*)?

// Alternation
OneOf("ERROR", "WARNING", "INFO")  // "ERROR|WARNING|INFO"
```

### RegexTokenizer Usage

```csharp
var patterns = new RegexTokenizer(
    $"HTTP {NUMS.As("status")} {ANY_CHARS.As("url")}",
    $"User {ALPHNUMS.As("user")} logged {OneOf("in", "out").As("action")}",
    $"Error: {ANY_CHARS.As("error")}"
);

await Read.Text("mixed.log")
    .SelectMany(line => patterns.Map(line))
    .Cases("status", "user", "error", RegexTokenizer.UNMATCHED.LINE)
    .SelectCase(
        status => $"HTTP: {status}",
        user => $"User: {user}",
        error => $"Error: {error}",
        line => $"Unrecognized: {line}"
    )
    .AllCases()
    .WriteText("categorized.log");
```

---

## EnumerableWithNote<T, P> — Context Preservation

Extends enumerables with metadata that flows through the pipeline.

```csharp
var contextualItems = items.Plus(new ProcessingContext 
{ 
    StartTime = DateTime.Now,
    BatchId = Guid.NewGuid()
});

var result = contextualItems
    .Where(item => ShouldProcess(item, contextualItems._Plus))
    .Select(item => Transform(item, contextualItems._Plus))
    .Minus(ctx => ctx.LogCompletion()); // Execute cleanup
```

---

## DataPublisher<T> — Pub/Sub Pattern

Publisher-subscriber pattern for distributing data to multiple consumers.

```csharp
var publisher = new DataPublisher<LogEntry>();

// Subscribe with optional filtering
publisher.AddWriter(allLogsChannel.Writer);
publisher.AddWriter(errorChannel.Writer, log => log.Level == LogLevel.Error);
publisher.AddWriter(recentChannel.Writer, log => DateTime.Now - log.Time < TimeSpan.FromMinutes(5));

// Publish to all matching subscribers
await publisher.PublishDataAsync(new LogEntry { Level = LogLevel.Error, Message = "..." });
```

---

## Syntaxi — Grammar Parsing

Basic token-based grammar parsing for structured text.

### Token Processing

```csharp
public interface ITokenEater
{
    TokenDigestion AcceptToken(string token);
    void Activate();
}

public enum TokenDigestion
{
    None,       // Token not matched
    Digested,   // Token processed
    Completed,  // Current expectation completed
    Propagate   // Propagate to other processors
}
```

### Grammar Rules

```csharp
var rules = new Rule[]
{
    new Rule("STATEMENT", "BEGIN", "COMMANDS", "END"),
    new Rule("COMMANDS", "COMMAND", "COMMANDS"),
    new Rule("COMMAND", "PRINT", "STRING")
};

var grammar = GrammarElem.Builder.Build(rules);

foreach (var token in tokens)
{
    var result = grammar.AcceptToken(token);
}
```

---

## See Also

- [Stream-Merging.md](Stream-Merging.md) — Full AsyncEnumerable documentation
- [Unified-Processing.md](Unified-Processing.md) — Cases/SelectCase pattern
- [LINQ-to-Spark.md](LINQ-to-Spark.md) — SparkQuery documentation
