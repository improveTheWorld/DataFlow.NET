# DataFlow.NET - The SUPRA Pattern

> **Architecture guide for the DataFlow.NET data processing philosophy**

---

## The SUPRA Pattern

**SUPRA** is the philosophy behind DataFlow.NET. It's an acronym that describes the five stages of every data pipeline:

```
S.U.P.R.A
│ │ │ │ │
│ │ │ │ └── Apply     (Output: write, display, aggregate)
│ │ │ └──── Route     (Branch: Cases/SelectCase)
│ │ └────── Process   (Transform: lazy, pure, no buffering)
│ └──────── Unify     (Merge multiple sources into one stream)
└────────── Sink      (Entry: absorb & buffer incoming data)
```

| Stage | Layer | Role | Buffering? |
|-------|-------|------|------------|
| **S**ink | Entry | Absorb external data, buffer if needed | ✅ Only here |
| **U**nify | Entry | Merge multiple sources into one | ❌ No |
| **P**rocess | Transform | Lazy transformations, one item at a time | ❌ Never |
| **R**oute | Transform | Cases/SelectCase branching | ❌ Never |
| **A**pply | Output | Write, display, reduce to value | ❌ No |

> **"Sink the chaos. Let the rest flow pure."**

---

## Why SUPRA Matters

Most data processing code looks like this:

```csharp
// ❌ The typical approach: Ad-hoc, memory-hungry, hard to reason about
var data = File.ReadAllLines("orders.csv");         // Load all into memory
var parsed = data.Skip(1).Select(ParseOrder);       // Parse all at once
var filtered = parsed.Where(x => x.Amount > 100);   // Still holding everything
var grouped = filtered.GroupBy(x => x.Category);    // More allocations
// ... processing continues, memory grows
```

The SUPRA pattern proposes a different way:

```csharp
// ✅ The SUPRA approach: Sink → Unify → Process → Route → Apply
await Read.CsvAsync<Order>("orders.csv")            // SINK: Stream in
    .Where(x => x.Amount > 100)                     // PROCESS: Lazy filter
    .Cases(x => x.Category == "VIP")                // ROUTE: Branch
    .SelectCase(vip => Process(vip), std => Process(std))
    .AllCases()
    .WriteCsvAsync("output.csv");                   // APPLY: Stream out
```

**The difference:** Memory stays constant. Items flow one at a time. The pipeline is declarative.

---

## The Three Laws of SUPRA

> Follow these principles and your data pipelines will be composable, memory-efficient, and testable.

### Law 1: Sink First (Buffer at Entry Only)

```
✅ SINK stage   →  May buffer (absorb external chaos)
❌ PROCESS/ROUTE → NEVER buffer. Always lazy.
✅ APPLY stage  →  Writes as items arrive
```

**Why?** Buffering mid-pipeline causes:
- Unpredictable memory growth
- Backpressure propagation problems
- Harder debugging

### Law 2: Everything Becomes a Stream

All data — file, API, database, Kafka — becomes `IEnumerable<T>` or `IAsyncEnumerable<T>`.

| Source Type | Traditional | SUPRA |
|-------------|-------------|-------|
| CSV file | `string[]` | `IAsyncEnumerable<T>` |
| REST API | `List<T>` | `IAsyncEnumerable<T>` |
| Kafka topic | `Consumer<T>` | `IAsyncEnumerable<T>` |
| Database | `IQueryable<T>` | `IAsyncEnumerable<T>` |

**One interface. Infinite interoperability.**

### Law 3: Process Purely, Apply Finally

Write what you want, not how to do it:

```csharp
// Declarative: WHAT should happen
var pipeline = source                                    // SINK
    .Where(x => x.IsValid)                               // PROCESS
    .Select(x => Transform(x))                           // PROCESS
    .Cases(x => x.Priority > 5)                          // ROUTE
    .SelectCase(high => ProcessHigh(high), low => ProcessLow(low))
    .AllCases();

// APPLY: Execution happens only when consumed
await pipeline.WriteCsvAsync("output.csv");
```

---

## The SUPRA Layers

SUPRA maps to three implementation layers in DataFlow.NET:

```
┌─────────────────────────────────────────────────────────────────┐
│           ENTRY LAYER (SINK + UNIFY)                            │
│    Absorb external data → IEnumerable / IAsyncEnumerable        │
│    • SINK: Readers, Polling, Buffering — absorb chaos here      │
│    • UNIFY: Merge multiple sources into one stream              │
└─────────────────────────────────────────────────────────────────┘
                              ↓
                 One unified stream interface
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│         TRANSFORM LAYER (PROCESS + ROUTE)                       │
│      Lazy, one-item-at-a-time — NO buffering, NO side effects   │
│    • PROCESS: Where, Select, Take, Until (pure transforms)      │
│    • ROUTE: Cases/SelectCase (conditional branching)            │
└─────────────────────────────────────────────────────────────────┘
                              ↓
                   Transformed stream
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│              OUTPUT LAYER (APPLY)                               │
│             Write, display, or reduce to value                  │
│    • Writers (CSV, JSON, YAML)                                  │
│    • Display (console debugging)                                │
│    • Reduce (ToList, Count, Aggregate)                          │
└─────────────────────────────────────────────────────────────────┘

       ⚙️ MONITORING: Spy() can be inserted between any two steps
```

---

## 2. Layer 1: Entry Barrier

### 2.1 Core Principle

> **All data enters as IEnumerable<T> or IAsyncEnumerable<T>**

No matter the source — file, API, Kafka, database — the data becomes a **lazy stream** of items.

### 2.2 Data Acquisition Patterns

| Pattern | Buffering Needed? | DataFlow Component |
|---------|------------------|-------------------|
| **Pull/Polling** | ❌ No | `Poll()` methods |
| **Push/Subscription** | ✅ Yes | `BufferAsync()` / `WithBoundedBuffer()` |
| **File Reading** | ❌ No (line-by-line) | `Read.Csv()`, `Read.Json()` |
| **Multiple Sources** | ❌ No | `AsyncEnumerable.Merge()` |

### 2.3 Polling Pattern

**When to use:** External system has a "get latest" API (sensors, queues, APIs).

**File:** `AsyncPollingExtensions.cs`

```csharp
// Simple polling: call function every 500ms
IAsyncEnumerable<int> readings = (() => sensor.GetReading())
    .Poll(TimeSpan.FromMilliseconds(500), cancellationToken);

// With stop condition: poll until elapsed > 30s
IAsyncEnumerable<string> messages = queue.TryDequeue
    .Poll(
        TimeSpan.FromMilliseconds(100),
        (item, elapsed) => elapsed > TimeSpan.FromSeconds(30),
        cancellationToken
    );
```

**Key methods:**
- `Poll(Func<T>, interval)` — Simple function polling
- `Poll(TryPollAction<T>, interval, stopCondition)` — TryGet pattern

### 2.4 Buffering Pattern (Subscriptions)

**When to use:** Data arrives via push (events, WebSockets, Kafka).

**File:** `EnumerableAsyncExtensions.cs`

```csharp
// Convert sync enumerable to async with cooperative yielding
IAsyncEnumerable<Order> asyncOrders = syncOrders.Async();

// Buffer with backpressure (for push sources)
IAsyncEnumerable<Event> buffered = eventStream
    .WithBoundedBuffer(capacity: 1024, fullMode: BoundedChannelFullMode.Wait);

// Throttle output rate
IAsyncEnumerable<Item> throttled = items.Throttle(TimeSpan.FromMilliseconds(100));
```

**Key methods:**
- `Async()` — Sync → Async with cooperative yielding
- `BufferAsync()` — Buffer sync source with optional background thread
- `WithBoundedBuffer()` — Channel-based backpressure for async sources
- `Throttle()` — Rate-limit output

### 2.5 Merging Multiple Sources

**When to use:** Same data type from multiple sources (e.g., logs from 3 servers).

**File:** `AsyncEnumerable.cs`

```csharp
// Merge multiple async sources into one stream
var merged = new UnifiedStream<LogEntry>()
    .Add(server1Logs)
    .Add(server2Logs)
    .Add(server3Logs.Async())  // Convert sync to async first
    .WithOptions(new UnifyOptions {
        Fairness = UnifyFairness.RoundRobin,
        ErrorMode = UnifyErrorMode.ContinueOnError
    });

await foreach (var log in merged)
{
    // Process logs from any server
}
```

### 2.6 File Reading

**Files:** `Read.Csv.cs`, `Read.Json.cs`, `Read.Yaml.cs`

```csharp
// CSV file → IAsyncEnumerable<Order>
var orders = Read.CsvAsync<Order>("orders.csv");

// JSON Lines file
var events = Read.JsonLinesAsync<Event>("events.jsonl");

// With options
var data = Read.CsvAsync<Record>("data.csv", new CsvReadOptions {
    HasHeader = true,
    Delimiter = ';',
    ErrorAction = ReaderErrorAction.Skip
});
```

---

## 3. Layer 2: Transformation

### 3.1 Core Principle

> **No buffering. Lazy. One item at a time.**

Every transformation method returns a new lazy stream. Items flow through only when consumed.

### 3.2 Standard LINQ-like Methods

```csharp
var result = source
    .Where(x => x.Amount > 100)      // Filter
    .Select(x => new { x.Id, x.Name }) // Transform
    .Take(100)                        // Limit
    .Until(x => x.Id == "STOP");      // Stop condition
```

### 3.3 The Cases/SelectCase Pattern

**Purpose:** Route items to different processing paths based on conditions.

**File:** `EnumerableCasesExtension.cs`

```csharp
// Categorize items, then apply different transformations
var processed = orders
    .Cases(
        o => o.Amount > 10000,          // Category 0: High value
        o => o.Country != "Domestic"    // Category 1: International
    )                                   // Category 2: Default (supra)
    .SelectCase(
        high => ProcessHighValue(high),
        intl => ProcessInternational(intl),
        normal => ProcessNormal(normal)
    )
    .AllCases();  // Merge all results back
```

**How it works:**
1. `Cases()` assigns each item a category index (0, 1, 2, ...)
2. `SelectCase()` applies the matching transformation
3. `AllCases()` collects results from all categories

**The "Supra" category:** Items matching no predicate go to the last (default) category.

### 3.4 Parallel Processing

**Files:** `ParallelQueryExtensions.cs`, `ParallelAsyncQueryExtensions.cs`

```csharp
// Parallel LINQ (sync)
var results = source.AsParallel()
    .Where(x => ExpensiveCheck(x))
    .Select(x => Transform(x));

// Parallel async
var asyncResults = asyncSource
    .ParallelSelect(x => TransformAsync(x), maxConcurrency: 4);
```

**Key insight:** The API stays the same. Whether sync, async, or parallel, the pattern is identical.

---

## 4. Layer 3: Output

### 4.1 Writing to Files

**File:** `Writers.cs`

```csharp
// Write to CSV
await processedData.WriteCsvAsync("output.csv");

// Write to JSON Lines
await results.WriteJsonLinesAsync("results.jsonl");

// With options
await data.WriteCsvAsync("data.csv", new CsvWriteOptions {
    Delimiter = ';',
    IncludeHeader = true
});
```

### 4.2 Display (Debugging)

**File:** `EnumerableDebuggingExtension.cs`

```csharp
// Display all items (eager, terminal)
results.Select(x => x.ToString()).Display("Results");

// Output:
// Results : ---------{
// Item1
// Item2
// Item3
// -------}
```

### 4.3 Reduce to Value

```csharp
// Standard LINQ aggregations
var count = await source.CountAsync();
var sum = await source.SumAsync(x => x.Amount);
var list = await source.ToListAsync();
```

---

## 5. Monitoring: The Spy Method

### 5.1 Purpose

> Insert `Spy()` between any two transformations to observe data flow without changing it.

**File:** `EnumerableDebuggingExtension.cs`

### 5.2 Usage

```csharp
var result = source
    .Where(x => x.IsActive)
    .Spy("After filter", x => $"{x.Id}: {x.Name}")  // Watch data here
    .Select(x => Transform(x))
    .Spy("After transform")                         // And here
    .ToList();
```

### 5.3 Characteristics

| Feature | Description |
|---------|-------------|
| **Lazy** | Only runs when items flow through |
| **Pass-through** | Yields original items unchanged |
| **Timestamped** | Optional timing info |
| **Customizable** | Custom formatter, separators |

---

## 6. Complete Example

```csharp
// LAYER 1: Entry
var orders = Read.CsvAsync<Order>("orders.csv");
var liveOrders = api.GetOrders.Poll(TimeSpan.FromSeconds(5), token);
var allOrders = new UnifiedStream<Order>()
    .Add(orders)
    .Add(liveOrders);

// LAYER 2: Transformation
var processed = allOrders
    .Where(o => o.Status == "Active")
    .Spy("Active orders")
    .Cases(
        o => o.Amount > 10000,
        o => o.Priority == "Rush"
    )
    .SelectCase(
        high => new ProcessedOrder(high, "VIP"),
        rush => new ProcessedOrder(rush, "Expedited"),
        normal => new ProcessedOrder(normal, "Standard")
    )
    .AllCases()
    .Spy("After categorization");

// LAYER 3: Output
await processed.WriteCsvAsync("processed_orders.csv");
```

---

## 7. Component Reference

| Layer | Component | File | Purpose |
|-------|-----------|------|---------|
| Entry | Polling | `AsyncPollingExtensions.cs` | Pull-based data acquisition |
| Entry | Buffering | `EnumerableAsyncExtensions.cs` | Sync→Async, backpressure |
| Entry | Merging | `AsyncEnumerable.cs` | Multi-source unification |
| Entry | Reading | `Read.*.cs` | File parsing |
| Transform | Cases | `EnumerableCasesExtension.cs` | Conditional routing |
| Transform | LINQ | `EnumerableExtensions.cs` | Filter, transform, control |
| Transform | Parallel | `ParallelQueryExtensions.cs` | Concurrent processing |
| Output | Writing | `Writers.cs` | File output |
| Output | Display | `EnumerableDebuggingExtension.cs` | Console output |
| Monitor | Spy | `EnumerableDebuggingExtension.cs` | In-pipeline inspection |

---

## 8. The DataFlow Standard

### 8.1 Core Principles

| # | Principle | Description |
|---|-----------|-------------|
| 1 | **Buffer at entry, never in the middle** | Only Layer 1 buffers (if subscription-based) |
| 2 | **Lazy everywhere** | No computation until consumption |
| 3 | **One item at a time** | Memory-constant processing |
| 4 | **Unified API** | Sync, async, parallel all use the same patterns |
| 5 | **Spy doesn't change data** | Observe without side effects |
| 6 | **Errors bubble up** | Handle at entry (skip/retry) or let them propagate |
| 7 | **Declare what, not how** | Pipeline describes intent, execution is automatic |

### 8.2 When to Use DataFlow

| Use Case | Fit |
|----------|-----|
| Processing large files | ✅ Perfect (streaming) |
| ETL pipelines | ✅ Perfect |
| Real-time data processing | ✅ Great (async streams) |
| Conditional routing | ✅ Cases pattern |
| Multi-source aggregation | ✅ Merge |
| In-memory collections | ⚠️ Overkill (use LINQ) |
| Single-item operations | ❌ Not designed for this |

### 8.3 How DataFlow Compares

| Framework | Model | Buffering | .NET Native |
|-----------|-------|-----------|-------------|
| **DataFlow.NET** | 3-layer streaming | Entry only | ✅ Yes |
| Reactive Extensions (Rx) | Push-based | Everywhere | ✅ Yes |
| Akka Streams | Back-pressure stages | Explicit | ❌ JVM port |
| Apache Flink | Dataflow | State backends | ❌ Java |
| Standard LINQ | In-memory | None | ✅ Yes |

### 8.4 The DataFlow Guarantee

If you follow the three laws:

1. **Memory stays constant** — regardless of data size
2. **Pipelines are composable** — plug any source into any transform
3. **Debugging is simple** — insert `Spy()` anywhere to observe
4. **Code is readable** — declarative, not procedural

---

## 9. What's Next?

### For Developers
- Start with `Read.Csv()` and simple transformations
- Learn the Cases pattern for conditional logic
- Graduate to async streams and merging

### For Architects
- Adopt the 3-layer model as a team standard
- Use DataFlow for all data-intensive services
- Establish naming conventions (Source → Transform → Sink)

### For Contributors
- Extend DataFlow with new connectors
- Add observability integrations
- Help document best practices

---

*DataFlow.NET: The standardized approach to data manipulation in .NET.*
