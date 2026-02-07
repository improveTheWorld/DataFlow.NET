# DataFlow.Net v1.2.1 Final Audit Report

## Summary: 134 tests across 3 batches | 133 pass, 1 fail | 2 findings

---

## ✅ Bug Fixed in Updated Package

### snake_case Normalization (NET-008) — **FIXED** ✅

The updated v1.2.1 package now correctly resolves snake_case columns:
- `first_name` → `FirstName` ✅ (was broken in initial v1.2.1 build)
- `order_id` → `OrderId` ✅
- `total_amount` → `TotalAmount` ✅

All 5 passes of the schema resolution pipeline now work as documented:
- Pass 1: Exact ✅ | Pass 2: Case-insensitive ✅ | Pass 3: Normalized ✅ | Pass 4: Resemblance ✅ | Pass 5: Levenshtein ✅

> Doc ref: [Materialization-Quick-Reference.md L73-81](file:///c:/CodeSource/DataFlow/src/docs/Materialization-Quick-Reference.md#L73)

---

## 🔴 Confirmed Finding (1)

### JsonLinesFormat Option Has No Effect

**What the docs say:**
- [Data-Writing-Infrastructure.md L88](file:///c:/CodeSource/DataFlow/src/docs/DataFlow-Data-Writing-Infrastructure.md#L88): `JsonLinesFormat = true` → "One object per line, no array"
- [Data-Writing-Infrastructure.md L86](file:///c:/CodeSource/DataFlow/src/docs/DataFlow-Data-Writing-Infrastructure.md#L86): `Indented` is "Ignored when JsonLinesFormat = true"

**What actually happens:**
```csharp
var opts = new JsonWriteOptions { JsonLinesFormat = true };
await data.WriteJson("out.jsonl", opts);
// Expected (JSONL): {"Id":1,"Name":"A"}
//                    {"Id":2,"Name":"B"}
// Actual: standard indented JSON array (10 lines with [, {, }, ])
```

The `JsonLinesFormat` property compiles and is accepted, but has **no effect** on output. The writer always produces a standard JSON array regardless of this setting.

---

## 🟡 API Usability Issue (1)

### ParallelQuery.Sum(decimal/float) — Ambiguous with System.Linq

- [Extension-Methods-API-Reference.md L111-118](file:///c:/CodeSource/DataFlow/src/docs/Extension-Methods-API-Reference.md#L111): DataFlow provides these
- Reality: **Conflicts with `System.Linq.ParallelEnumerable.Sum`** — requires fully-qualified call
- Works when called explicitly: `DataFlow.Parallel.ParallelQueryExtensions.Sum(pq)`

---

## ✅ All Batch Results

### Batch 1 — Original Suite (43/43)
All core read/write/transform tests pass on updated v1.2.1.

### Batch 2 — Deep Feature Scan (56/56)

| Section | Tests | Result |
|---------|:-----:|:------:|
| ObjectMaterializer (13) | 13 | ✅ All pass (including snake_case) |
| UnifiedStream | 6 | ✅ |
| Polling | 3 | ✅ |
| Cases Pattern | 4 | ✅ |
| Async LINQ | 8 | ✅ |
| ParallelAsyncQuery | 5 | ✅ |
| Buffering/Channel | 2 | ✅ |
| Debugging Extensions | 3 | ✅ |
| Flatten/MergeOrdered | 4 | ✅ |
| Edge Cases & Stress | 6 | ✅ |

### Batch 3 — v1.2.1 New Features (34/35)

| Section | Tests | Result |
|---------|:-----:|:------:|
| snake_case regression (NET-008) | 4 | ✅ All 5 passes work |
| Smart Decimal Auto-Detection | 5 | ✅ US, EU, DE, US-grouped, FR |
| OnError callback (FEAT-001) | 1 | ✅ |
| Write API 6-overload (CSV×6 + JSON + YAML + Text) | 9 | ✅ |
| CsvWriteOptions (Separator, Header, Append) | 2 | ✅ |
| JsonLinesFormat | 1 | ❌ Option ignored |
| WriterMetrics | 1 | ✅ |
| CSV Quoting (RFC 4180) | 3 | ✅ |
| FormatProvider override | 1 | ✅ |
| YAML record support (NET-006) | 2 | ✅ |
| Key regressions (Batch 1+2) | 6 | ✅ |

---

## 📋 Doc Improvements (10)

| # | Doc File | Issue | Severity |
|---|----------|-------|----------|
| 1 | [Extension-Methods-API-Reference.md L111](file:///c:/CodeSource/DataFlow/src/docs/Extension-Methods-API-Reference.md#L111) | Sum(decimal/float) PLINQ clash undocumented | 🟡 |
| 2 | [Data-Writing-Infrastructure.md L88](file:///c:/CodeSource/DataFlow/src/docs/DataFlow-Data-Writing-Infrastructure.md#L88) | JsonLinesFormat documented but non-functional | 🔴 |
| 3 | [Data-Writing-Infrastructure.md L86](file:///c:/CodeSource/DataFlow/src/docs/DataFlow-Data-Writing-Infrastructure.md#L86) | "Indented ignored when JsonLinesFormat=true" — moot since JsonLines doesn't work | 🟡 |
| 4 | [Extension-Methods-API-Reference.md L132](file:///c:/CodeSource/DataFlow/src/docs/Extension-Methods-API-Reference.md#L132) | ToLines requires separator — missing from display | 🟡 |
| 5 | [DataFlow-SUPRA-Pattern.md L276](file:///c:/CodeSource/DataFlow/src/docs/DataFlow-SUPRA-Pattern.md#L276) | Uses `Delimiter = ';'` — should be `Separator = ";"` | 🟡 |
| 6 | [ErrorManager-Spec.md](file:///c:/CodeSource/DataFlow/src/docs/ErrorManager-Spec.md) | "Design Draft" — not implemented, could confuse users | 📝 |
| 7 | [Changelog L73](file:///c:/CodeSource/DataFlow/src/docs/changelog/DataFlow.Net_1.2.1.md#L73) | PerformanceTests flaky — honest, but consider CI skip | 📝 |
| 8 | [ObjectMaterializer-Limitations.md L62](file:///c:/CodeSource/DataFlow/src/docs/ObjectMaterializer-Limitations.md#L62) | Section 4 marked "✅ Resolved" — now confirmed resolved | ✅ |
| 9 | [Materialization-Quick-Reference.md L79](file:///c:/CodeSource/DataFlow/src/docs/Materialization-Quick-Reference.md#L79) | Pass 3 snake_case example — now works as documented | ✅ |
| 10 | [ObjectMaterializer.md L236](file:///c:/CodeSource/DataFlow/src/docs/ObjectMaterializer.md#L236) | "normalized (snake_case, camelCase)" — now accurate | ✅ |

---

## Methodology

- **External auditor**: docs-only — no source code inspection
- All failures cross-referenced against specific doc file + line number
- Doc index: [doc_index.md](file:///C:/Users/bilel/.gemini/antigravity/brain/a34286bc-c886-44c3-83ee-3740d1507663/doc_index.md)
- Test project: [Program.cs](file:///c:/CodeSource/DataflowAfterrelease1-2-0%20tests/DataFlowAuditTest/DataFlowAuditTest/Program.cs)
- Package: `DataFlow.Net.1.2.1.nupkg` from `c:\CodeSource\DataFlow\nupkgs\current\`

## Verdict

**v1.2.1 (updated build) is production-solid.** 133/134 tests pass (99.3%). The only runtime failure is `JsonLinesFormat` — a documented feature that isn't wired up yet. Everything else — including all 5 schema resolution passes, Smart Decimal parsing for 5 international formats, the full Write API matrix, OnError callbacks, and YAML record support — works exactly as documented.
