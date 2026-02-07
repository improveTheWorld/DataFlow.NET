# DataFlow.Net v1.2.1 Final Audit Report

## Summary: 99 tests (batch 1 + batch 2), 1 confirmed bug, 1 API issue

---

## 🔴 Confirmed Bug (1)

### 5-Pass Schema Resolution — Pass 3 (snake_case normalization) Doesn't Work

**What the docs say:**
- [Materialization-Quick-Reference.md L79](file:///c:/CodeSource/DataFlow/src/docs/Materialization-Quick-Reference.md#L79): Pass 3 Normalized → `first_name` → `FirstName` ✅
- [Materialization-Quick-Reference.md L125-127](file:///c:/CodeSource/DataFlow/src/docs/Materialization-Quick-Reference.md#L125): Snowflake/Spark example: `order_id` → `OrderId`, `total_amount` → `TotalAmount`
- [ObjectMaterializer.md L236](file:///c:/CodeSource/DataFlow/src/docs/ObjectMaterializer.md#L236): "normalized (snake_case, camelCase)"

**What actually happens:**
```
ObjectMaterializer.Create<SnakePerson>(
    schema: ["first_name", "birth_year"],
    parameters: ["Charlie", 1990]
);
// Result: FirstName="", BirthYear=0  ← Both empty!
```

**Impact:** Any CSV/Snowflake/Spark data with snake_case columns will silently produce default values — zero data corruption warnings, just empty objects. This particularly affects the Snowflake/Spark use case highlighted in the docs.

> [!CAUTION]
> This is dangerous because it's **silent**. Users expect `first_name` → `FirstName` to "just work" per the 5-pass resolution docs. Instead they get empty objects with no error.

---

## 🟡 API Usability Issue (1)

### ParallelQuery.Sum(decimal/float) — Ambiguous with System.Linq

**What the docs say:**
[Extension-Methods-API-Reference.md L111-118](file:///c:/CodeSource/DataFlow/src/docs/Extension-Methods-API-Reference.md#L111): DataFlow provides `Sum(decimal)` and `Sum(float)` for `ParallelQuery<T>`.

**What actually happens:**
```csharp
// ❌ Won't compile — CS0121 ambiguity
Enumerable.Range(1,100).AsParallel().Select(x => (decimal)x).Sum();

// ✅ Works but ugly
DataFlow.Parallel.ParallelQueryExtensions.Sum(pq);
```

`System.Linq.ParallelEnumerable` already provides `Sum(decimal)` and `Sum(float)`. DataFlow's versions clash, forcing users to use the fully-qualified explicit call. This defeats the purpose of extension methods.

> [!NOTE]
> The `Sum(int)` and `Sum(long)` overloads work fine because .NET's PLINQ uses `Func<T, int>` projection overloads, while DataFlow provides direct `ParallelQuery<int>.Sum()`.

---

## ✅ Retracted Findings

| Originally Reported | Verdict |
|---|---|
| CSV RFC 4180 `""` parsing | ✅ Works. Test error on our part. |
| `Until()` inclusive behavior | ✅ Deliberate design, consistent across all types. [Extension-Methods-API-Reference.md L74](file:///c:/CodeSource/DataFlow/src/docs/Extension-Methods-API-Reference.md#L74) now explicitly documents this. |

---

## ✅ Batch 1 Results (43 tests, v1.2.1)

All 43 tests from the original suite pass on v1.2.1 including previously-reported locale decimal parsing.

## ✅ Batch 2 Results (56 tests, v1.2.1)

| Section | Tests | Pass | Fail |
|---------|:-----:|:----:|:----:|
| ObjectMaterializer | 13 | 12 | 1 |
| UnifiedStream | 6 | 6 | 0 |
| Polling | 3 | 3 | 0 |
| Cases Pattern (Deep) | 4 | 4 | 0 |
| Async LINQ Operators | 8 | 8 | 0 |
| ParallelAsyncQuery | 5 | 5 | 0 |
| Buffering/Channel | 2 | 2 | 0 |
| Debugging Extensions | 3 | 3 | 0 |
| Flatten Variants | 2 | 2 | 0 |
| MergeOrdered | 2 | 2 | 0 |
| Parallel Sum | 2 | 2 | 0 |
| Edge Cases & Stress | 6 | 6 | 0 |
| **Total** | **56** | **55** | **1** |

---

## 📋 Doc Improvement Recommendations (9)

| # | Doc File | Line(s) | Issue |
|---|----------|---------|-------|
| 1 | [Extension-Methods-API-Reference.md](file:///c:/CodeSource/DataFlow/src/docs/Extension-Methods-API-Reference.md#L111) | L111-118 | warn that `Sum(decimal/float)` clashes with `System.Linq.ParallelEnumerable` |
| 2 | [Extension-Methods-API-Reference.md](file:///c:/CodeSource/DataFlow/src/docs/Extension-Methods-API-Reference.md#L132) | L132 | `ToLines` requires separator — missing from signature display |
| 3 | [DataFlow-SUPRA-Pattern.md](file:///c:/CodeSource/DataFlow/src/docs/DataFlow-SUPRA-Pattern.md#L276) | L276 | uses `Delimiter = ';'` — should be `Separator = ";"` per v1.2.1 API |
| 4 | [Materialization-Quick-Reference.md](file:///c:/CodeSource/DataFlow/src/docs/Materialization-Quick-Reference.md#L79) | L79 | Pass 3 claims `first_name` → `FirstName` works — it doesn't |
| 5 | [Materialization-Quick-Reference.md](file:///c:/CodeSource/DataFlow/src/docs/Materialization-Quick-Reference.md#L125) | L125-127 | Snowflake/Spark `order_id` → `OrderId` example won't work at runtime |
| 6 | [ObjectMaterializer.md](file:///c:/CodeSource/DataFlow/src/docs/ObjectMaterializer.md#L236) | L236 | "normalized (snake_case, camelCase)" claim — not functional |
| 7 | [Extension-Methods-API-Reference.md](file:///c:/CodeSource/DataFlow/src/docs/Extension-Methods-API-Reference.md#L74) | L74 | Until inclusive semantics ✅ now documented — good |
| 8 | [ParallelAsyncQuery-API-Reference.md](file:///c:/CodeSource/DataFlow/src/docs/ParallelAsyncQuery-API-Reference.md#L265) | L265 | Take() doesn't stop upstream — honest limitation ✅ |
| 9 | [Extension-Methods-API-Reference.md](file:///c:/CodeSource/DataFlow/src/docs/Extension-Methods-API-Reference.md#L132) | L132 | `ToLines` returns lazy `IEnumerable<string>` (generator), not a materialized string — may surprise users |

---

## Methodology

- External auditor approach: **docs only** — no source code inspection
- All failures cross-referenced against specific doc file + line number
- Used [doc_index.md](file:///C:/Users/bilel/.gemini/antigravity/brain/a34286bc-c886-44c3-83ee-3740d1507663/doc_index.md) as reference index
- Test project: `c:\CodeSource\DataflowAfterrelease1-2-0 tests\DataFlowAuditTest`
- Package under test: `DataFlow.Net.1.2.1.nupkg` from local source
