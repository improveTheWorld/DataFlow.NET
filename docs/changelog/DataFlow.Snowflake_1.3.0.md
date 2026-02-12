# Changelog — DataFlow.Snowflake

All notable changes to the DataFlow.Snowflake package.

## [1.3.0] — 2026-02-12 — ROCK Edition — "Write C#, Run Snowflake"

> **Requires**: DataFlow.Net 1.2.1+

Custom C# methods now auto-translate to Snowflake UDFs at runtime.
No SQL, no escape hatches — just write C# and it runs server-side.

### ✨ Auto-UDF — Zero-Friction Server-Side C#

Static, instance, lambda, closure, and entity-parameter methods in
`Where()` / `Select()` / `OrderBy()` / `GroupBy()` auto-deploy as
Snowflake Java UDFs at connect time via IL-to-Java translation.

| Pattern | Example |
|---------|---------|
| **Static method** | `Where(o => IsHighValue(o.Amount))` |
| **Instance method** | `var v = new Validator(1000); Where(o => v.IsValid(o.Amount))` |
| **Entity parameter** | `Where(o => IsVipOrder(o))` — decomposes `o` into columns |
| **Multi-param** | `Where(o => IsInRange(o.Amount, 1000, 5000))` |
| **Mixed expression** | `Where(o => o.IsActive && IsHighValue(o.Amount))` |
| **Nested calls** | `Where(o => IsHighValueCorp(o.Amount, o.Name))` — calls another UDF internally |

- **UdfRegistry**: Scans assemblies at connect time, translates all eligible methods, batch-deploys `CREATE OR REPLACE FUNCTION` statements
- **Mixed expression decomposition**: `o.IsActive && IsHighValue(o.Amount)` → native SQL `AND` + auto-generated UDF call — zero developer friction
- **All LINQ positions**: Where, Select, OrderBy, OrderByDescending, ThenBy, ThenByDescending, GroupBy
- **Pipeline-safe**: UDFs work with Join, Union, Intersect, Except, WriteTable, Skip, Take, Distinct

```csharp
// Auto-UDF: just call your C# method — it compiles to a Snowflake UDF automatically
var highValue = await context.Read.Table<Order>("ORDERS")
    .Where(o => o.IsActive && MyHelpers.IsHighValue(o.Amount))
    .Select(o => new { o.Id, Tier = MyHelpers.GetTier(o.Amount) })
    .OrderBy(o => MyHelpers.GetTier(o.Amount))
    .ToList();
```

### ✨ ForEach — Server-Side Iteration

- **`query.ForEach(action)`** — deferred server-side iteration via Snowflake stored procedures
- **Static field sync-back**: Accumulator fields in the delegate are written back after procedure execution
- **Entity-parameter support**: Methods that take the full entity auto-decompose into column arguments
- **Instance method + closure support**: Captured fields passed as procedure arguments

```csharp
// ForEach: server-side iteration with accumulator sync-back
await context.Read.Table<Order>("ORDERS")
    .ForEach(MyProcessor.ProcessOrder)
    .Count();  // triggers execution
```

### 🔒 Package Isolation

- **Snowflake.Data driver internalized**: `Snowflake.Data` types (e.g. `SnowflakeDbConnection`) no longer leak into consumer IntelliSense — the driver is an implementation detail

### 🧪 Testing — 418 Integration Tests

| Suite | Tests | Coverage |
|-------|-------|----------|
| Core audit | 234 | Full LINQ pipeline, expressions, set ops, materialization |
| Auto-UDF stress | 88 | Static, instance, lambda, entity, nested, edge cases, concurrent |
| Where & Select UDF | 93 | Multi-param, mixed expressions, pipeline combinations |
| Flaky (excluded) | 3 | Concurrent race conditions (non-deterministic) |

### 🐛 Bug Fixes

| Bug | Issue | Fix |
|-----|-------|-----|
| **SF-010** | Licensing DLL not bundled | Switched to `TargetsForTfmSpecificBuildOutput` |
| **SF-011** | `Show()` named `ShowAsync()` | Renamed to `Show` |
| **SF-005** | VARIANT lambda method calls | Added `TranslateVariantStringMethod` |
| **SF-006** | SelectMany / LATERAL FLATTEN | Added `SelectMany<TResult>()` |
| **SF-007** | GroupJoin / LEFT JOIN + GROUP BY | Added `GroupJoin` |
| **SF-009** | DateTime arithmetic | Added `DATEADD` translation |
| **SF-013** | Bind variable lost after Select() | Added `ImportParameters()` |
| **SF-014** | `List<T>.Contains()` → IN | Added instance `Contains` handler |

---

## [1.2.1] — 2026-02-04

> **Requires**: DataFlow.Net 1.2.0+

### 🐛 Bug Fixes

#### Expression Translation (Spark Parity)
| Bug | Issue | Fix |
|-----|-------|-----|
| **REG003** | Ternary expressions (`?:`) | `ConditionalExpression` → `CASE WHEN ... END` |
| **REG004** | Object initializers (`new DTO{}`) | `MemberInitExpression` now supported |
| **REG009** | String concatenation (`+`) | String `Add` → `CONCAT()` function |
| **REG014** | Null coalescing (`??`) | `Coalesce` → `COALESCE()` function |

#### Type Coercion
- **Int64→Int32**: Snowflake NUMBER columns (returned as Int64) now coerced to C# Int32 properties
- **Anonymous types**: Fixed coercion for read-only properties in anonymous types

#### SELECT Projection Fix
- **Constant Inlining**: Constants in SELECT now inlined as SQL literals instead of bind variables (`:p0`)

### ✨ Features Verified

#### Nested Objects (VARIANT)
Full support for Snowflake VARIANT columns with native C# property access.

### 🧪 Quality
- **122 tests passed, 0 failed, 0 skipped** (package integration)

---

## [1.2.0] — 2026-01-30

### 🚀 Breaking Changes
- **Removed `Snowflake.Table<T>(options, tableName)`**: Use `Snowflake.Connect(...).Read.Table<T>(tableName)` instead
- **Removed Legacy Write APIs**

### 🐛 Bug Fixes
- **Package Bundling Fixed**: `DataFlow.Licensing.dll` now correctly included
- **BUG-018 Primitive Projection Fixed**: `.Select(o => o.IsActive)` now works
- **BUG-019 Distinct Chaining Fixed**: `.Select(...).Distinct()` preserved correctly

---

## [1.1.0] — 2026-01-28

### ✨ New Features
- Initial release: LINQ-native Snowflake integration
- Context API: `Snowflake.Connect(...)`
- O(1) memory writes via PUT + COPY INTO
- Cases pattern for multi-destination routing
- Pull() guardrail for explicit server/client boundary
