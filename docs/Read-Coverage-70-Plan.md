# Read Layer Coverage: Lessons Learned & Plan for 70%

> **Current:** 65.21% | **Target:** 70% | **Gap:** ~5% (~110 lines)

---

## What Worked

| Approach | Tests | Impact |
|----------|-------|--------|
| Stream-based tests (MemoryStream) | 20+ | Medium |
| Options-based (metrics tracking) | 10+ | Medium |
| Edge cases (quotes, CRLF, unicode) | 15+ | Low |

**Winning Pattern:**
```csharp
using var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv));
var opts = new CsvReadOptions { HasHeader = true };
var items = Read.CsvSync<T>(ms, opts).ToList();
Assert.NotNull(opts.Metrics.CompletedUtc);
```

---

## What Didn't Work

**Problem:** 45+ tests added, only +0.54% coverage gained.

**Root Causes:**
1. Same code paths hit repeatedly with different data
2. Sync/Async variants share core implementation
3. Error handling paths need specific failure conditions
4. Buffer management paths need chunked streams

---

## Uncovered Code Analysis

### 1. CsvRfc4180Parser.cs (~35% uncovered)

**Target Functions:**
- `GuardRailError()` - Exception formatting
- `FlushPending()` - Incomplete record handling  
- `Process()` lines 300-400 - Error state machine
- `EmitRecord()` errorPath - Schema validation failures

**Mock Needed:**
```csharp
// Create stream that returns partial data
public class PartialReadStream : Stream {
    public override int Read(byte[] b, int o, int c) 
        => Math.Min(c, 5); // Force small reads
}
```

### 2. Read.Json.cs (~40% uncovered)

**Target Functions:**
- `ProcessBuffer()` 600-750 - Incomplete JSON handling
- `AdjustBufferForNextRead()` - Buffer management
- `TryDetermineRoot()` - Root detection edge cases

**Mock Needed:**
```csharp
// Chunked JSON stream - splits data at buffer boundaries
public class ChunkedJsonStream : Stream {
    // Returns only N bytes per Read() call
}
```

### 3. Read.Yaml.cs (~30% uncovered)

**Target Functions:**
- `IsAllowed()` - Type restriction checks
- `YamlSync()` 280-350 - Security error handling
- `CancellableTextReader` - Read cancellation

---

## Plan for V1.1

### Phase 1: Mock Infrastructure (2h)

```csharp
// TestHelpers/MockStreams.cs
public static class MockStreams {
    // Returns data in fixed-size chunks
    public static Stream Chunked(string data, int chunkSize);
    
    // Throws at specific byte position
    public static Stream FailsAt(string data, int position, Exception ex);
    
    // Cancellation-aware stream
    public static Stream Cancellable(string data, CancellationToken ct);
}
```

### Phase 2: Targeted Tests (4h)

| Category | Tests | Expected Coverage |
|----------|-------|-------------------|
| Guard Rail Violations | 5 | +2% |
| JSON Buffer Boundaries | 8 | +3% |
| Corrupted Data Recovery | 6 | +2% |
| YAML Type Restrictions | 4 | +1% |

### Phase 3: Verification (1h)

Run coverage, fix failures, verify 70%+ reached.

---

## Files to Create

| File | Purpose |
|------|---------|
| `TestHelpers/MockStreams.cs` | Stream mocking utilities |
| `Csv/CsvErrorRecoveryTests.cs` | Guard rail violation tests |
| `Json/JsonBufferTests.cs` | Buffer boundary tests |
| `Yaml/YamlSecurityTests.cs` | Type restriction tests |

---

## Priority Order

1. **JSON Buffer Processing** - Highest uncovered, needs chunked streams
2. **CSV Guard Rails** - Easy with MaxFieldLength option
3. **Error Recovery** - Needs malformed data injection
4. **YAML Security** - Type restrictions, custom tags

---

## Key Insight

The Read layer prioritizes **production safety** over testability:
- Defensive error handling = many edge case paths
- Buffer optimization for streaming ≠ chunked testing
- Error formatting includes context (file paths, lines) that varies

**Recommendation:** Add internal test hooks in V1.1:
```csharp
#if DEBUG
internal static void SimulateBufferBoundary(int size);
#endif
```

---

## Estimated Total Effort: **7 hours**
