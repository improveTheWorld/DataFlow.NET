# DataFlow.Net v1.2.0 Final Audit Report

## Executive Summary

| Metric | Value |
|--------|-------|
| **Total Tests** | 88 |
| **Passed** | 77 (88%) |
| **Issues Found** | 16 |

---

## 🔴 Critical Bugs (4)

| Bug | Description |
|-----|-------------|
| **CSV Quoted Fields** | RFC 4180 `""` escaping fails inside quoted fields |
| **Locale Decimals** | `1,5` parsed as 2 fields (comma-decimal locales) |
| **JSON null→decimal** | Throws on null numeric values |
| **Until() Off-by-One** | `Until(x > 5)` returns 6 items, not 5 |

---

## 🟡 API/Documentation Gaps (6)

| Discrepancy | Status |
|-------------|--------|
| `MergeOrdered()` | Not accessible with documented signature |
| `TakeWhile()` | Not available on IAsyncEnumerable |
| `Spy()` | Only works with `IAsyncEnumerable<string>` |
| `BuildString()` | Returns `StringBuilder`, not `string` |
| `WriteJson(path, options)` | Path overload only accepts CancellationToken |
| README versioning | 1.1.0 vs 1.2.0 inconsistency |

---

## ✅ Verified Working (77 tests)

### Round 1: Core API (18 tests)
CSV/JSON/YAML/Text reading, Cases Pattern, ForEach/Do, LINQ operators, Parallel processing, Polling, ObjectMaterializer, Display

### Round 2: Stress Tests (24 tests)
| Category | Passed |
|----------|--------|
| CsvQuoteMode options | ✅ Both modes work |
| ErrorAction (Skip/Stop) | ✅ Metrics + TerminatedEarly |
| Guard Rails | ✅ MaxColumnsPerRow, MaxRawRecordLength |
| Schema Inference | ✅ Int32, Decimal, Boolean, DateTime |
| JSON Options | ✅ AllowSingleObject, ValidateElements |
| Extension Methods | ✅ Buffer, Flatten, BuildString, Throttle, WithBoundedBuffer |
| Parallel Cases | ✅ Thread-safe |
| Progress Reporting | ✅ 10 updates / 10K rows |

### Round 3: Extreme Tests (30 tests)
| Category | Passed |
|----------|--------|
| **YAML Security** | |
| DisallowAliases | ✅ Blocked aliases (3 errors) |
| DisallowCustomTags | ✅ Blocked custom tags |
| MaxDepth | ✅ Triggered at limit |
| MaxTotalDocuments | ✅ Limited to 5 docs |
| MaxNodeScalarLength | ✅ Blocked 5K char scalar |
| **JSON Guard Rails** | |
| MaxElements | ✅ Limited to 10 elements |
| MaxStringLength | ✅ Blocked 5K char string |
| **Write APIs** | |
| Custom separator | ✅ `;` separator works |
| WriteHeader=false | ✅ Header omitted |
| RFC 4180 quoting | ✅ Commas and quotes escaped |
| Append mode | ✅ 3 lines after append |
| WriteYaml | ✅ Works |
| **Boundary Conditions** | |
| 1000-char field names | ✅ Preserved |
| Control characters | ✅ 3 rows processed |
| LF vs CRLF | ✅ Both handled |
| 200-column CSV | ✅ Schema correct |
| SkipWhile | ✅ Works correctly |
| Aggregate | ✅ Sum = 15 |
| First/FirstOrDefault | ✅ Works |
| Any with predicate | ✅ Works |

---

## Recommendations

### Must Fix
1. CSV quoted field parsing (RFC 4180)
2. `Until()` off-by-one behavior
3. JSON null→decimal handling

### Documentation Updates
1. Fix `MergeOrdered` signature or remove from docs
2. Add `Spy()` string-only limitation
3. Clarify `BuildString()` return type
4. Fix `WriteJson` path API signature docs
5. Fix README version

---

*Audit: Feb 6, 2026 | Package: DataFlow.Net 1.2.0 | .NET 8.0*
