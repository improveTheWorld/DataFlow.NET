# Polling and Buffering Patterns

> **This document covers DataFlow.NET's data acquisition patterns for transforming external data sources into `IAsyncEnumerable<T>` streams.**

---

## Table of Contents

1. [Overview](#1-overview)
2. [Polling Pattern](#2-polling-pattern)
3. [Buffering Pattern](#3-buffering-pattern)
4. [Throttling](#4-throttling)
5. [API Reference](#5-api-reference)

---

## 1. Overview

DataFlow.NET provides two complementary patterns for data acquisition:

| Pattern | Purpose | When to Use |
|---------|---------|-------------|
| **Polling** | Pull data periodically from external sources | APIs, sensors, queues with "get" interface |
| **Buffering** | Convert sync sources to async with backpressure | Push sources, blocking producers |

Both patterns produce `IAsyncEnumerable<T>` streams that integrate seamlessly with the SUPRA pipeline.

**Files:**
- `AsyncPollingExtensions.cs` — Polling methods
- `EnumerableAsyncExtensions.cs` — Buffering and sync-to-async conversion

---

## 2. Polling Pattern

### Simple Polling (Infinite)

Poll a function at regular intervals until cancelled:

```csharp
// Poll sensor every 500ms → produces IAsyncEnumerable<int>
IAsyncEnumerable<int> readings = (() => sensor.GetReading())
    .Poll(TimeSpan.FromMilliseconds(500), cancellationToken);

// Now use with standard async enumeration
await foreach (var reading in readings)
{
    Console.WriteLine($"Sensor: {reading}");
}
```

### Polling with Stop Condition

Poll until a condition is met:

```csharp
// Poll queue until empty or 30 seconds elapsed → produces IAsyncEnumerable<Message>
IAsyncEnumerable<Message> messages = queue.TryDequeue
    .Poll(
        TimeSpan.FromMilliseconds(100),
        (item, elapsed) => elapsed > TimeSpan.FromSeconds(30),
        cancellationToken
    );

// Compatible with all SUPRA pipeline operations
await foreach (var msg in messages)
{
    ProcessMessage(msg);
}
```

### TryPoll Pattern

For sources that follow the `TryGet` pattern (returns `false` when empty):

```csharp
// Delegate type: bool TryPollAction<T>(out T item)
AsyncPollingExtensions.TryPollAction<string> tryPop = stack.TryPop;

// Returns IAsyncEnumerable<string> - ready for SUPRA pipeline
IAsyncEnumerable<string> items = tryPop.Poll(TimeSpan.FromMilliseconds(50), token);

await foreach (var item in items)
{
    Console.WriteLine(item);
}
// Stops automatically when stack is empty
```

### Polling Behavior

| Aspect | Behavior |
|--------|----------|
| **Default values** | Skipped (not yielded) |
| **TryPoll returns false** | Stream terminates |
| **Stop condition returns true** | Final item yielded, then terminates |
| **Exception in poll function** | Propagates to consumer, terminates stream |

---

## 3. Buffering Pattern

### Sync to Async Conversion

Convert `IEnumerable<T>` to `IAsyncEnumerable<T>` with cooperative yielding:

```csharp
// IEnumerable<T> → IAsyncEnumerable<T> with cooperative yielding
IAsyncEnumerable<Order> asyncData = syncData.Async(yieldThresholdMs: 15);

// Now compatible with all SUPRA pipeline operations
await foreach (var item in asyncData)
{
    // UI stays responsive - yields control every 15ms
}
```

The `yieldThresholdMs` parameter controls how often `Task.Yield()` is called:
- **15ms (default)**: Ideal for UI responsiveness (under 60fps frame budget)
- **long.MaxValue**: Disable yielding for maximum throughput

### Channel-Based Buffering

For high-throughput or blocking producers, use channel-based backpressure:

```csharp
// Buffer sync source on background thread → IAsyncEnumerable<T>
IAsyncEnumerable<Data> buffered = syncData.BufferAsync(
    yieldThresholdMs: 15,
    runOnBackgroundThread: true
);

// Custom channel options
IAsyncEnumerable<Data> customBuffered = syncData.BufferAsync(
    yieldThresholdMs: 15,
    runOnBackgroundThread: true,
    options: new BoundedChannelOptions(1024)
    {
        FullMode = BoundedChannelFullMode.Wait,
        SingleWriter = true,
        SingleReader = true
    }
);
```

### Async Stream Buffering

Add bounded buffer to existing `IAsyncEnumerable<T>`:

```csharp
// Add buffer to existing async stream → still IAsyncEnumerable<T>
IAsyncEnumerable<Event> bufferedAsync = asyncSource.WithBoundedBuffer(
    capacity: 500,
    fullMode: BoundedChannelFullMode.Wait
);

// Advanced: full channel options
IAsyncEnumerable<Event> customAsync = asyncSource.WithBoundedBuffer(
    new BoundedChannelOptions(1000)
    {
        FullMode = BoundedChannelFullMode.DropOldest,
        SingleReader = true,
        AllowSynchronousContinuations = true
    }
);
```

### Backpressure Modes

| Mode | Behavior |
|------|----------|
| `Wait` | Producer blocks when buffer full (classic backpressure) |
| `DropNewest` | Discard newest item when full |
| `DropOldest` | Discard oldest item when full |
| `DropWrite` | Reject write silently when full |

---

## 4. Throttling

Rate-limit output from a synchronous source:

```csharp
// IEnumerable<T> → IAsyncEnumerable<T> with rate limiting
IAsyncEnumerable<Item> throttled = items.Throttle(TimeSpan.FromMilliseconds(100));

// Or use milliseconds directly
IAsyncEnumerable<Item> throttled2 = items.Throttle(100.0);

await foreach (var item in throttled)
{
    // Items arrive at controlled rate
}
```

---

## 5. API Reference

### Polling Extensions

```csharp
// Infinite polling (until cancelled)
public static IAsyncEnumerable<T> Poll<T>(
    this Func<T> pollAction,
    TimeSpan pollingInterval,
    CancellationToken cancellationToken = default);

// Polling with stop condition
public static IAsyncEnumerable<T> Poll<T>(
    this Func<T> pollAction,
    TimeSpan pollingInterval,
    Func<T, TimeSpan, bool> stopCondition,
    CancellationToken cancellationToken = default);

// TryPoll pattern (terminates on false)
public static IAsyncEnumerable<T> Poll<T>(
    this TryPollAction<T> tryPollAction,
    TimeSpan pollingInterval,
    CancellationToken cancellationToken = default);

// TryPoll with stop condition
public static IAsyncEnumerable<T> Poll<T>(
    this TryPollAction<T> tryPollAction,
    TimeSpan pollingInterval,
    Func<T, TimeSpan, bool> stopCondition,
    CancellationToken cancellationToken = default);

// Delegate type
public delegate bool TryPollAction<T>(out T item);
```

### Buffering Extensions

```csharp
// Sync to async with cooperative yielding
public static IAsyncEnumerable<T> Async<T>(
    this IEnumerable<T> items,
    long yieldThresholdMs = 15,
    CancellationToken cancellationToken = default);

// Sync to async with optional background thread
public static IAsyncEnumerable<T> BufferAsync<T>(
    this IEnumerable<T> source,
    long yieldThresholdMs = 15,
    bool runOnBackgroundThread = false,
    BoundedChannelOptions? options = null,
    CancellationToken cancellationToken = default);

// Add bounded buffer to async stream
public static IAsyncEnumerable<T> WithBoundedBuffer<T>(
    this IAsyncEnumerable<T> source,
    BoundedChannelOptions? options = null,
    CancellationToken ct = default);

// Convenience overload
public static IAsyncEnumerable<T> WithBoundedBuffer<T>(
    this IAsyncEnumerable<T> source,
    int capacity,
    BoundedChannelFullMode fullMode = BoundedChannelFullMode.Wait,
    CancellationToken ct = default);
```

### Throttling Extensions

```csharp
public static IAsyncEnumerable<T> Throttle<T>(
    this IEnumerable<T> source,
    TimeSpan interval,
    CancellationToken cancellationToken = default);

public static IAsyncEnumerable<T> Throttle<T>(
    this IEnumerable<T> source,
    double intervalInMs,
    CancellationToken cancellationToken = default);
```

---

## See Also

- [Stream Merging](Stream-Merging.md) — Multi-source unification with `UnifiedStream<T>`
- [Cases Pattern](Cases-Pattern.md) — Cases/SelectCase pattern
- [SUPRA Pattern](DataFlow-SUPRA-Pattern.md) — Overall architecture
