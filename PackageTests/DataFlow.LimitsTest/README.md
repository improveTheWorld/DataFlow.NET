# DataFlow.Net Audit Test Suite

## Overview

Comprehensive test suite for auditing DataFlow.Net v1.0.1 package functionality, limits, and documentation accuracy.

**Results:** 101 tests | 86 PASS | 15 LIMIT | 0 FAIL

---

## Setup Requirements

### Prerequisites
- .NET 9.0 SDK
- Windows/Linux/macOS

### Installation

```bash
# Clone or copy test project
cd DataFlowLimitsTest

# Restore packages
dotnet restore

# Install YamlDotNet (required for YAML tests)
dotnet add package YamlDotNet --version 16.0.0

# Build
dotnet build

# Run all tests
dotnet run
```

---

## Test Categories (20 sections, 101 tests)

### 1. Streaming Reader Tests (5 tests)
| Test | Purpose | Expected |
|------|---------|----------|
| Empty CSV | Handle 0-row files | 0 items |
| Normal CSV | Basic 10-row file | 10 items |
| Large CSV | 10K rows | All rows |
| Special Chars CSV | Unicode: 田中, Müller | Preserved |
| Multiple Files | Enum pattern | 30 items |

### 2. Cases Pattern Tests (5 tests)
| Test | Purpose | Expected |
|------|---------|----------|
| Single Match | First predicate wins | Correct |
| First Match | Order matters | First matched |
| No Match | Default case | Default item |
| Complex | Multiple categories | 10 items |
| Empty | Zero items | Empty |

### 3. Parallel Processing Tests (13 tests)
| Test | Purpose | Expected |
|------|---------|----------|
| MaxConcurrency(4) | Limit parallelism | Works |
| MaxConcurrency(150) | Above limit | ⚠️ Accepted |
| BufferSize(5) | Below minimum | ⚠️ Accepted |
| Timeout(0) | Zero timeout | ⚠️ Accepted |
| ContinueOnError | Error resilience | Works |
| Cancellation | Token respect | Works |

### 4. LINQ Extensions Tests (9 tests)
| Test | Purpose | Expected |
|------|---------|----------|
| Take(5) | Limit items | 5 items |
| Take(-1) | Negative count | ⚠️ Returns 0 |
| Until(true) | Immediate stop | ⚠️ Returns 1 |
| Spy | Debug logging | Works |
| BuildString | String building | StringBuilder |
| IsNullOrEmpty | Null check | Works |
| ParallelAsyncQuery | Parallel enum | Works |

### 5. Stress Tests (3 tests)
| Test | Purpose | Expected |
|------|---------|----------|
| 1M Items | Memory efficiency | <1MB delta |
| Deep Pipeline | 20 chained ops | 5 items |
| High Concurrency | 50 parallel | 100 results |

### 6. Sync API Tests (4 tests)
| Test | Purpose | Expected |
|------|---------|----------|
| Read.CsvSync | Sync CSV | Works |
| Read.JsonSync | Sync JSON | Works |
| Read.YamlSync | Sync YAML | Works* |
| Read.TextSync | Sync text | Works |

*Requires YamlDotNet package

### 7. String Parsing Tests (3 tests)
| Test | Purpose | Expected |
|------|---------|----------|
| string.AsCsv<T> | Parse CSV string | Works |
| string.AsJson<T> | Parse JSON string | Works |
| string.AsYaml<T> | Parse YAML string | Works* |

### 8. Data Writing Tests (5 tests)
| Test | Purpose | Expected |
|------|---------|----------|
| WriteCsv | Async CSV write | Works |
| WriteJson | Async JSON write | Works |
| WriteYaml | Async YAML write | Works* |
| WriteText | Async text write | Works |
| WriteCsvSync | Sync CSV write | Works |

### 9. Stream Merging Tests (4 tests)
| Test | Purpose | Expected |
|------|---------|----------|
| UnifiedStream Basic | 3 sources | 9 items |
| Empty | 0 sources | Empty |
| Single | 1 source | 3 items |
| Many | 10 sources | 100 items |

### 10. Buffering Tests (3 tests)
| Test | Purpose | Expected |
|------|---------|----------|
| Async() | Sync→async | Works |
| WithBoundedBuffer | Bounded | Works |
| BufferAsync | Background | Works |

### 11. Error Handling Tests (5 tests)
| Test | Purpose | Expected |
|------|---------|----------|
| ErrorAction.Skip | Skip errors | 5 valid |
| ErrorAction.Stop | Stop on error | 4 rows |
| ErrorAction.Throw | Throw exception | Exception |
| Progress | IProgress<T> | Reports |
| Metrics | ReaderMetrics | Stats |

### 12. Throttling Tests (2 tests)
| Test | Purpose | Expected |
|------|---------|----------|
| Throttle(50ms) | Rate limit | ~300ms for 5 |
| Throttle(0) | Zero delay | Works |

### 13. CSV Options Tests (3 tests)
| Test | Purpose | Expected |
|------|---------|----------|
| Separator ';' | Custom delim | 2 rows |
| No Header | Schema-based | 2 rows |
| MaxColumns | Guard rail | Rejected |

### 14. Buffer Boundary Tests (6 tests)
| Test | Purpose | Expected |
|------|---------|----------|
| Capacity=0 | Zero buffer | ⚠️ Accepted |
| Capacity=1 | Minimum | 5 items |
| Capacity=10000 | Large | 3 items |
| DropOldest | Backpressure | 2 items |
| DropNewest | Backpressure | 2 items |
| Background | Thread pool | 100 items |

### 15. Merge Edge Cases (6 tests)
| Test | Purpose | Expected |
|------|---------|----------|
| ContinueOnError | Error mode | 6 items |
| FailFast | Error mode | 6 items |
| FirstAvailable | Fairness | Works |
| RoundRobin | Interleave | 1,10,2,20... |
| Filter | Per-source | Even only |
| Unlisten | Remove source | 3 items |

### 16. JSON Options Tests (4 tests)
| Test | Purpose | Expected |
|------|---------|----------|
| MaxElements=10 | Limit array | 10 items |
| MaxStringLength=100 | Reject long | 0 items |
| SingleObject | Single record | 1 item |
| ValidateElements | Custom filter | 2 items |

### 17. LINQ Additional Tests (7 tests)
| Test | Purpose | Expected |
|------|---------|----------|
| MergeOrdered | Merge sorted | 1,2,3,4... |
| Flatten | Nested arrays | 9 items |
| Flatten Nested | Lists | 4 items |
| Display | Debug output | Works |
| ToLines | Line split | ⚠️ Missing |
| Until(x,idx) | Index stop | ⚠️ Off-by-one |
| Take(start,count) | Skip+Take | 4,5,6,7 |

### 18. Parallel Aggregation Tests (3 tests)
| Test | Purpose | Expected |
|------|---------|----------|
| Sum(int) | Integer sum | 5050 |
| Sum(long) | Long sum | 5050 |
| Sum(float) | Float sum | 55 |

### 19. Polling Tests (3 tests)
| Test | Purpose | Expected |
|------|---------|----------|
| Poll Basic | Timed poll | 4+ polls |
| StopCondition | Value stop | 5 values |
| Cancellation | Token cancel | 3+ items |

### 20. YAML Security Tests (8 tests)
| Test | Purpose | Expected |
|------|---------|----------|
| MaxDepth=5 | Depth limit | Rejected |
| MaxScalar=100 | Length limit | Rejected |
| DisallowAliases | Block &/*ref | Rejected |
| DisallowCustomTags | Block !Type | Rejected |
| RestrictTypes | Type safety | ⚠️ Case-sensitive |
| MultiDocument | Multiple docs | 3 docs |
| SequenceMode | Array root | 3 items |
| MaxTotalDocs=5 | Doc limit | 5 docs |

---

## Known Issues Found

### Bugs (3)
1. **YamlDotNet not bundled** - Must add manually
2. **Until() off-by-one** - Returns 1 extra item
3. **Buffer capacity=0 accepted** - Should throw

### Documentation Errors (12)
- `ToListAsync()` - missing
- `string.AsJsonSync<T>()` - missing
- `ToLines()` - missing
- `Spy<T>()` - string only
- `BuildString()` - returns StringBuilder
- Limits not enforced (MaxConcurrency, BufferSize, Timeout)
- Parallel `Sum` - ambiguous with PLINQ

---

## Test Data Files

Auto-generated in `testdata/` subdirectory:
- `empty.csv` - Empty file
- `orders.csv` - 10 orders
- `malformed.csv` - Malformed data
- `special_chars.csv` - Unicode test

---

## Reproducing Results

```bash
# Full test run
dotnet run

# Expected output format:
# ✅ PASS: 86
# ⚠️ LIMIT: 15  
# ❌ FAIL: 0
# Total: 101
```

---

## Project Structure

```
DataFlowLimitsTest/
├── Program.cs          # All test code
├── DataFlowLimitsTest.csproj
└── testdata/           # Auto-generated test files
    ├── empty.csv
    ├── orders.csv
    └── ...
```

---

## Dependencies

```xml
<PackageReference Include="DataFlow.Net" Version="1.0.1" />
<PackageReference Include="YamlDotNet" Version="16.0.0" />
```

---

## Version Info

- **Package Tested:** DataFlow.Net 1.0.1
- **Test Date:** 2026-01-04
- **Platform:** Windows, .NET 9.0
