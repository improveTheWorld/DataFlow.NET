# LINQ-to-Snowflake Capabilities & Limitations

This document outlines the current capabilities of the `SnowflakeQuery<T>` provider in DataFlow.NET. It is designed to handle the "80% happy path" of analytics queries natively on Snowflake, while acknowledging specific limitations where client-side processing or alternative approaches are required.

## Table of Contents

1. [Supported Features](#-supported-features-native-execution)
   - [Core Query Operations](#1-core-query-operations)
   - [Grouping & Aggregation](#2-grouping--aggregation)
   - [Joins](#3-joins)
   - [Expression Translation](#4-expression-translation)
   - [Window Functions](#5-window-functions)
   - [Set Operations](#6-set-operations)
   - [Debug & Diagnostics](#7-debug--diagnostics)
   - [Execution](#8-execution)
   - [Cases Pattern](#9-cases-pattern-multi-destination-routing)
2. [Unsupported Features](#️-unsupported-features-remaining-gaps)
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
| `WithRowNumber(o => o.Id)` | `ROW_NUMBER() OVER (ORDER BY id)` |
| `WithRank(o => o.Dept, o => o.Salary)` | `RANK() OVER (PARTITION BY dept ORDER BY salary)` |
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
| `Explain(extended)` | Print SQL query plan |
| `PrintSchema()` | Print result type schema |
| `Spy(label)` | Display and continue chaining |
| `ToSql()` | Get generated SQL string |

### 8. Execution
- **Async Streaming**: `IAsyncEnumerable<T>` support via `GetAsyncEnumerator` (efficient memory usage).
- **Materialization**: `ToList()`, `ToArray()`, `First()`, `FirstOrDefault()`, `Count()`, `Any()`.
- **Single Element**: `Single()`, `SingleOrDefault()` (verify exactly 1 result).
- **Server-to-Client Transition**: `Pull()` switches to client-side streaming while maintaining lazy row-by-row evaluation.

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

### 2. Distributed Operations (Spark-Specific)
*   ⚡ **Cache/Persist**: Snowflake handles caching internally—not user-controlled.
*   ⚡ **Repartition/Coalesce**: Not applicable—Snowflake manages distribution.

### 3. Expression Limitations
*   ❌ **Custom Method Calls**: `.Where(o => MyHelper.Validate(o))` (Cannot translate C# methods to SQL).

---

## Summary

DataFlow.NET's Snowflake provider is a **production-ready Analytical Query Builder**. It excels at:
*   Filtering and aggregating massive datasets.
*   Projecting flat results for analysis.
*   Streaming data efficiently to your application.
*   **Write operations**: `WriteTable(s)` and `MergeTable(s)` for unified and multi-destination writes.
*   **95%+** coverage of common analytics scenarios.

> **Note:** Snowflake is an analytics data warehouse, not a transactional database. EF Core does not support Snowflake. If your application needs complex entity relationships, change tracking, and migrations, use a traditional OLTP database (SQL Server, PostgreSQL) with Entity Framework Core. For Snowflake analytics workloads, DataFlow.Snowflake is the only LINQ solution available.

---

## See Also

- [LINQ-to-Snowflake Guide](LINQ-to-Snowflake.md) — Complete usage documentation
- [LINQ-to-Spark](LINQ-to-Spark.md) — SparkQuery provider documentation
- [Cases Pattern](Cases-Pattern.md) — Cases/SelectCase pattern
- [Licensing](../../DataFlow.Enterprise/docs/Licensing.md) — Product-specific licensing details
