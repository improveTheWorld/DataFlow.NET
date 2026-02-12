# LINQ-to-Snowflake Capabilities & Limitations

This document outlines the current capabilities of the `SnowflakeQuery<T>` provider in DataFlow.NET. It is designed to handle the "80% happy path" of analytics queries natively on Snowflake, while acknowledging specific limitations where client-side processing or alternative approaches are required.

## Table of Contents

1. [Supported Features](#supported-features-native-execution)
   - [Core Query Operations](#1-core-query-operations)
   - [Grouping & Aggregation](#2-grouping--aggregation)
   - [Joins](#3-joins)
   - [Expression Translation](#4-expression-translation)
   - [Window Functions](#5-window-functions)
   - [Set Operations](#6-set-operations)
   - [Debug & Diagnostics](#7-debug--diagnostics)
   - [Execution](#8-execution)
   - [Cases Pattern](#9-cases-pattern-multi-destination-routing)
2. [Unsupported Features](#unsupported-features-remaining-gaps)
3. [Summary](#summary)
4. [See Also](#see-also)

---

## ✅ Supported Features (Native Execution)

The following operations are translated directly to Snowflake SQL and executed server-side.

### 1. Core Query Operations
| LINQ Method | SQL Translation | Example |
|-------------|-----------------|---------|
| `Where(predicate)` | `WHERE ...` | `.Where(o => o.Amount > 100)` |
| `Select(selector)` | `SELECT ...` | `.Select(o => new { o.Id, o.Name })` |
| `OrderBy(key)` | `ORDER BY ...` | `.OrderBy(o => o.Date)` |
| `OrderByDescending` | `ORDER BY ... DESC` | `.OrderByDescending(o => o.Amount)` |
| `ThenBy` / `Descending` | `, ...` (chained sort) | `.OrderBy(...).ThenBy(o => o.Id)` |
| `Take(n)` | `LIMIT n` | `.Take(50)` |
| `Skip(n)` | `OFFSET n` | `.Skip(10)` |
| `Distinct()` | `SELECT DISTINCT` | `.Select(o => o.Category).Distinct()` |

### 2. Grouping & Aggregation
| LINQ Method | SQL Translation | Notes |
|-------------|-----------------|-------|
| `GroupBy(key)` | `GROUP BY key` | Supports single & composite keys |
| `.Select(g => g.Count())` | `COUNT(*)` | |
| `.Select(g => g.Sum(x))` | `SUM(x)` | |
| `.Select(g => g.Max(x))` | `MAX(x)` | |
| `.Select(g => g.Min(x))` | `MIN(x)` | |
| `.Select(g => g.Average(x))` | `AVG(x)` | |

### 3. Joins
| LINQ Method | SQL Translation | Notes |
|-------------|-----------------|-------|
| `Join(...)` | `INNER JOIN ... ON ...` | Supports multi-table joins via chaining |

### 4. Expression Translation
| C# Expression | SQL Translation |
|---------------|-----------------|
| `==`, `!=` | `=`, `<>` |
| `>`, `>=`, `<`, `<=` | `>`, `>=`, `<`, `<=` |
| `&&`, `||` | `AND`, `OR` |
| `!boolProp` | `NOT (column = TRUE)` |
| `string.Contains(s)` | `LIKE '%s%'` |
| `string.StartsWith(s)` | `LIKE 's%'` |
| `string.EndsWith(s)` | `LIKE '%s'` |
| `string.IndexOf(s)` | `POSITION(s, col) - 1` |
| `string.Length` | `LENGTH(col)` |
| `collection.Contains(x)` | `column IN (1, 2, 3)` |
| `Math.Abs(x)` | `ABS(x)` |
| `Math.Round(x)` | `ROUND(x)` |
| `Math.Ceiling(x)` | `CEIL(x)` |
| `Math.Floor(x)` | `FLOOR(x)` |
| `Math.Sqrt(x)` | `SQRT(x)` |
| `Math.Pow(x, y)` | `POW(x, y)` |
| `date.Year` | `YEAR(date)` |
| `date.Month` | `MONTH(date)` |
| `date.Day` | `DAY(date)` |
| `date.Hour` | `HOUR(date)` |
| `date.Minute` | `MINUTE(date)` |
| `date.Second` | `SECOND(date)` |
| `date.DayOfWeek` | `DAYOFWEEK(date)` |
| `date.DayOfYear` | `DAYOFYEAR(date)` |
| `obj.Prop.Nested` | `obj:prop:nested` | (VARIANT support) |

### 5. Window Functions
| Method | SQL Translation |
|--------|-----------------|
| `WithRowNumber(partitionBy, orderBy)` | `ROW_NUMBER() OVER (PARTITION BY ... ORDER BY ...)` |
| `WithRank(partitionBy, orderBy)` | `RANK() OVER (PARTITION BY ... ORDER BY ...)` |
| `w.Lag("col", 1)` | `LAG(col, 1) OVER (...)` |
| `w.Lead("col", 1)` | `LEAD(col, 1) OVER (...)` |
| `w.Sum("col")` | `SUM(col) OVER (...)` |

### 6. Set Operations
| Method | SQL Translation |
|--------|-----------------|
| `query1.Union(query2)` | `UNION ALL` |
| `query1.UnionDistinct(query2)` | `UNION` |
| `query1.Intersect(query2)` | `INTERSECT` |
| `query1.Except(query2)` | `EXCEPT` |

### 7. Debug & Diagnostics
| Method | Description |
|--------|-------------|
| `Show(n)` | Display first N rows to console |
| `Explain()` | Print SQL query plan (synchronous — do not `await`) |
| `PrintSchema()` | Print result type schema |
| `Spy(label)` | Display and continue chaining |
| `ToSql()` | Get generated SQL string |

### 8. Execution
- **Async Streaming**: `IAsyncEnumerable<T>` support via `GetAsyncEnumerator` (efficient memory usage).
- **Materialization**: `ToList()`, `ToArray()`, `First()`, `FirstOrDefault()`, `Count()`, `Any()`.
- **Single Element**: `Single()`, `SingleOrDefault()` (verify exactly 1 result).
- **Aggregate pattern**: For filtered aggregates, use `.Where(pred).Count()` rather than `.Count(pred)` — the chained pattern avoids overload ambiguity.
- **Auto-UDF**: Custom C# methods in `Where()`/`Select()` auto-translate to Snowflake UDF calls — static, instance, lambda, and entity-parameter patterns all supported.
- **ForEach (Deferred)**: `ForEach(action)` deploys server-side logic to Snowflake. Execution deferred until `Count()`/`ToList()`/`ToArray()`. Static fields auto-synced back after execution.
- **Pull() (Escape Hatch)**: `Pull()` switches to client-side streaming for edge cases where server-side execution is not desired (e.g., accessing LINQ operators not available on `SnowflakeQuery<T>`).

### 9. Cases Pattern (Multi-Destination Routing)
| Method | Description |
|--------|-------------|
| `Cases(predicates...)` | Categorize rows by conditions (SQL CASE WHEN) |
| `SelectCase(selectors...)` | Transform each category (server-side projection) |
| `WriteTables(tables...)` | Write each category to different table |
| `MergeTables((table, key)...)` | Merge each category with different match key |

---

## ⚠️ Unsupported Features (Remaining Gaps)

These features are **not currently supported**:

### 1. Complex Relationships & Navigation
*   ❌ **Navigation Properties**: `o.Customer.Orders` (No `Include()` support like EF Core).
*   ❌ **Deep Graph Materialization**: Automatically hydrating a full object graph from a flat join.
*   ✅ **VARIANT Array Operations** (Higher-Order Functions):
    - `o.Items.Any(i => i.Price > 100)` → `ARRAY_SIZE(FILTER(items, i -> i:price > 100)) > 0`
    - `o.Items.All(i => i.Active)` → `ARRAY_SIZE(FILTER(items, i -> NOT i:active)) = 0`
    - `o.Items.Where(i => i.Active)` → `FILTER(items, i -> i:active)`
    - `o.Items.Select(i => i.Price * 2)` → `TRANSFORM(items, i -> i:price * 2)`
*   ❌ **Correlated Subqueries**: `.Where(o => otherQuery.Any(x => x.Id == o.Id))` (Requires `EXISTS`).

### 3. Expression Translation (Advanced)
*   ✅ **Custom Method Calls (Auto-UDF)**: Static methods, instance methods, lambda/`Func<>` delegates, and entity-parameter methods in `Where()` and `Select()` are auto-translated to UDF function calls.
    - `.Where(o => IsHighValue(o.Amount))` → `WHERE auto_helpers_ishighvalue(amount)`
    - `.Where(o => o.IsActive && IsHighValue(o.Amount))` → `WHERE is_active AND auto_helpers_ishighvalue(amount)` — mixed expressions decompose naturally
    - `.Where(o => CustomValidator(o))` → `WHERE auto_class_customvalidator(amount, status)` — entity parameters auto-decomposed into individual columns
    - Instance: `.Where(o => validator.IsValid(o.Amount))` → `WHERE auto_ordervalidator_isvalid(amount)`
    - Lambda: `Func<decimal, bool> f = x => x > 1000;` `.Where(o => f(o.Amount))` → `WHERE auto_lambda_f(amount)`
    - ⚠️ UDFs prevent Snowflake predicate pushdown — the Roslyn analyzer (DFSN004) warns at build time
    - ❌ **UDF result alias not queryable after Select**: Projecting a UDF result as a named field in `.Select()` and then referencing that alias in **any subsequent chained operator** (`.Where()`, `.OrderBy()`, `.GroupBy()`, etc.) is **not supported**. SQL cannot reference a column alias defined in the same query level — it requires wrapping in a subquery first.
      ```csharp
      // ❌ NOT SUPPORTED — any chaining on the alias produces invalid SQL
      orders
          .Select(o => new { IsSmall = IsSmallOrder(o.Amount) })
          .Where(x => x.IsSmall);       // ERROR: invalid identifier 'IS_SMALL'
          // .OrderBy(x => x.IsSmall)    // Same error
          // .GroupBy(x => x.IsSmall)    // Same error

      // ✅ WORKAROUND — use Pull() to switch to client-side
      orders
          .Select(o => new { o.Id, IsSmall = IsSmallOrder(o.Amount) })
          .Pull()                    // switch to client-side
          .Where(x => x.IsSmall);   // filtered in memory
      ```

### 4. Roslyn Analyzer Diagnostics
| Rule | Severity | Description |
|------|----------|---------|
| **DFSN004** | ⚠️ Warning | Custom method in `Where()` — prevents predicate pushdown |
| **DFSN005** | ⚠️ Warning | Instance method — supported, but carries closure overhead |
| **DFSN006** | ⚠️ Warning | Multiple UDFs in same `Where` — compounding performance impact |

## Summary

DataFlow.NET's Snowflake provider is a **production-ready Analytical Query Builder**. It excels at:
*   Filtering and aggregating massive datasets.
*   Projecting flat results for analysis.
*   Streaming data efficiently to your application.
*   **Write operations**: 
    - `query.WriteTable("TABLE").CreateIfMissing().Overwrite()` — Server-side write
    - `data.WriteTable(context, "TABLE").CreateIfMissing().Overwrite()` — Client-to-server push
*   **Full C# support**: Custom methods auto-translate to server-side function calls — no `Pull()` needed.
*   **95%+** coverage of common analytics scenarios.

> **Note:** Snowflake is an analytics data warehouse, not a transactional database. EF Core does not support Snowflake. If your application needs complex entity relationships, change tracking, and migrations, use a traditional OLTP database (SQL Server, PostgreSQL) with Entity Framework Core. For Snowflake analytics workloads, DataFlow.Snowflake is the only LINQ solution available.

---

## See Also

- [LINQ-to-Snowflake Guide](LINQ-to-Snowflake.md) — Complete usage documentation
- [LINQ-to-Spark](LINQ-to-Spark.md) — SparkQuery provider documentation
- [Cases Pattern](Cases-Pattern.md) — Cases/SelectCase pattern
- [Licensing](../../DataFlow.Enterprise/docs/Licensing.md) — Product-specific licensing details
