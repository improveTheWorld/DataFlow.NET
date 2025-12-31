# DataFlow.NET - The SUPRA Pattern

> **Architecture guide for the DataFlow.NET data processing philosophy**

---

## Table of Contents

1. [The SUPRA Pattern](#1-the-supra-pattern)
2. [Why SUPRA Matters](#2-why-supra-matters)
3. [Three Simple Rules](#3-three-simple-rules)
4. [The SUPRA Layers](#4-the-supra-layers)
5. [Implementation Details](#5-implementation-details)
6. [The DataFlow Standard](#6-the-dataflow-standard)
7. [What's Next?](#7-whats-next)

---

## 1. The SUPRA Pattern

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

## 2. Why SUPRA Matters

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
await Read.Csv<Order>("orders.csv")            // SINK: Stream in
    .Where(x => x.Amount > 100)                     // PROCESS: Lazy filter
    .Cases(x => x.Category == "VIP")                // ROUTE: Branch
    .SelectCase(vip => Process(vip), std => Process(std))
    .AllCases()
    .WriteCsv("output.csv");                   // APPLY: Stream out
```

**The difference:** Memory stays constant. Items flow one at a time. The pipeline is declarative.

---

## 3. Three Simple Rules

> Follow these principles and your data pipelines will be composable, memory-efficient, and testable.

### Rule 1: Sink First

**Buffer and normalize at the edge, never in the middle.**

```
✅ SINK stage   →  May buffer (absorb external chaos)
❌ PROCESS/ROUTE → NEVER buffer. Always lazy.
✅ APPLY stage  →  Writes as items arrive
```

**Why?** Buffering mid-pipeline causes:
- Unpredictable memory growth
- Backpressure propagation problems
- Harder debugging

### Rule 2: Flow Lazy

**Items stream one by one. Constant memory.**

All data — file, API, database, Kafka — becomes `IEnumerable<T>` or `IAsyncEnumerable<T>`.

| Source Type | Traditional | SUPRA |
|-------------|-------------|-------|
| CSV file | `string[]` | `IAsyncEnumerable<T>` |
| REST API | `List<T>` | `IAsyncEnumerable<T>` |
| Kafka topic | `Consumer<T>` | `IAsyncEnumerable<T>` |
| Database | `IQueryable<T>` | `IAsyncEnumerable<T>` |

**One interface. Infinite interoperability.**

### Rule 3: Route Declaratively

**No more `if/else` spaghetti.**

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
await pipeline.WriteCsv("output.csv");
```

---

## 4. The SUPRA Layers

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

## 5. Implementation Details

Now let's explore how each SUPRA layer is implemented in DataFlow.NET.

### Entry Layer (Sink + Unify)

#### Core Principle

> **All data enters as IEnumerable<T> or IAsyncEnumerable<T>**

No matter the source — file, API, Kafka, database — the data becomes a **lazy stream** of items.

#### Data Acquisition Patterns

| Pattern | Buffering Needed? | DataFlow Component |
|---------|------------------|-------------------|
| **Pull/Polling** | ❌ No | `Poll()` methods |
| **Push/Subscription** | ✅ Yes | `BufferAsync()` / `WithBoundedBuffer()` |
| **File Reading** | ❌ No (line-by-line) | `Read.Csv()`, `Read.Json()` |
| **Multiple Sources** | ❌ No | `AsyncEnumerable.Merge()` |

#### Polling Pattern

**When to use:** External system has a "get latest" API (sensors, queues, APIs).

> 📖 **Full documentation:** [Polling and Buffering](Polling-Buffering.md)

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

#### Buffering Pattern (Subscriptions)

**When to use:** Data arrives via push (events, WebSockets, Kafka).

> 📖 **Full documentation:** [Polling and Buffering](Polling-Buffering.md)

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

#### Merging Multiple Sources

**When to use:** Same data type from multiple sources (e.g., logs from 3 servers).

> 📖 **Full documentation:** [Stream Merging](Stream-Merging.md)

**File:** `AsyncEnumerable.cs`

```csharp
// Merge multiple async sources into one stream
var merged = new UnifiedStream<LogEntry>(new UnifyOptions {
        Fairness = UnifyFairness.RoundRobin,
        ErrorMode = UnifyErrorMode.ContinueOnError
    })
    .Unify(server1Logs, "server1")
    .Unify(server2Logs, "server2")
    .Unify(server3Logs.Async(), "server3");  // Convert sync to async first

await foreach (var log in merged)
{
    // Process logs from any server
}
```

#### File Reading

**Files:** `Read.Csv.cs`, `Read.Json.cs`, `Read.Yaml.cs`

```csharp
// CSV file → IAsyncEnumerable<Order>
var orders = Read.Csv<Order>("orders.csv");

// JSON file → IAsyncEnumerable<Event>
var events = Read.Json<Event>("events.json");

// With options
var data = Read.Csv<Record>("data.csv", new CsvReadOptions {
    HasHeader = true,
    Delimiter = ';',
    ErrorAction = ReaderErrorAction.Skip
});
```

---

### Transform Layer (Process + Route)

#### Core Principle

> **No buffering. Lazy. One item at a time.**

Every transformation method returns a new lazy stream. Items flow through only when consumed.

#### Standard LINQ-like Methods

```csharp
var result = source
    .Where(x => x.Amount > 100)      // Filter
    .Select(x => new { x.Id, x.Name }) // Transform
    .Take(100)                        // Limit
    .Until(x => x.Id == "STOP");      // Stop condition
```

#### The Cases/SelectCase Pattern

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

#### Parallel Processing

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

### Output Layer (Apply)

#### Writing to Files

**File:** `Writers.cs`

```csharp
// Write to CSV
await processedData.WriteCsv("output.csv");

// Write to JSON
await results.WriteJson("results.json");

// With options
await data.WriteCsv("data.csv", new CsvWriteOptions {
    Delimiter = ';',
    IncludeHeader = true
});
```

#### Display (Debugging)

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

#### Reduce to Value

```csharp
// Standard LINQ aggregations
var count = await source.CountAsync();
var sum = await source.SumAsync(x => x.Amount);
var list = await source.ToListAsync();
```

---

### Monitoring: The Spy Method

#### Purpose

> Insert `Spy()` between any two transformations to observe data flow without changing it.

**File:** `EnumerableDebuggingExtension.cs`

#### Usage

```csharp
var result = source
    .Where(x => x.IsActive)
    .Spy("After filter", x => $"{x.Id}: {x.Name}")  // Watch data here
    .Select(x => Transform(x))
    .Spy("After transform")                         // And here
    .ToList();
```

#### Characteristics

| Feature | Description |
|---------|-------------|
| **Lazy** | Only runs when items flow through |
| **Pass-through** | Yields original items unchanged |
| **Timestamped** | Optional timing info |
| **Customizable** | Custom formatter, separators |

---

### Complete Example

```csharp
// LAYER 1: Entry
var orders = Read.Csv<Order>("orders.csv");
var liveOrders = api.GetOrders.Poll(TimeSpan.FromSeconds(5), token);
var allOrders = new UnifiedStream<Order>()
    .Unify(orders, "file")
    .Unify(liveOrders, "api");

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
await processed.WriteCsv("processed_orders.csv");
```

---

### Component Reference

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

---

## 6. The DataFlow Standard

### Core Principles

| # | Principle | Description |
|---|-----------|-------------|
| 1 | **Buffer at entry, never in the middle** | Only Layer 1 buffers (if subscription-based) |
| 2 | **Lazy everywhere** | No computation until consumption |
| 3 | **One item at a time** | Memory-constant processing |
| 4 | **Unified API** | Sync, async, parallel all use the same patterns |
| 5 | **Spy doesn't change data** | Observe without side effects |
| 6 | **Errors bubble up** | Handle at entry (skip/retry) or let them propagate |
| 7 | **Declare what, not how** | Pipeline describes intent, execution is automatic |

### When to Use DataFlow

| Use Case | Fit |
|----------|-----|
| Processing large files | ✅ Perfect (streaming) |
| ETL pipelines | ✅ Perfect |
| Real-time data processing | ✅ Great (async streams) |
| Conditional routing | ✅ Cases pattern |
| Multi-source aggregation | ✅ Merge |
| In-memory collections | ⚠️ Overkill (use LINQ) |
| Single-item operations | ❌ Not designed for this |

### How DataFlow Compares

| Framework | Model | Buffering | .NET Native |
|-----------|-------|-----------|-------------|
| **DataFlow.NET** | 3-layer streaming | Entry only | ✅ Yes |
| Reactive Extensions (Rx) | Push-based | Everywhere | ✅ Yes |
| Akka Streams | Back-pressure stages | Explicit | ❌ JVM port |
| Apache Flink | Dataflow | State backends | ❌ Java |
| Standard LINQ | In-memory | None | ✅ Yes |

### The DataFlow Guarantee

If you follow the three rules:

1. **Memory stays constant** — regardless of data size
2. **Pipelines are composable** — plug any source into any transform
3. **Debugging is simple** — insert `Spy()` anywhere to observe
4. **Code is readable** — declarative, not procedural

---

## 7. What's Next?

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
