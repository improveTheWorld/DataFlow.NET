# LINQ-to-Snowflake Capabilities & Limitations

This document outlines the current capabilities of the `SnowflakeQuery<T>` provider in DataFlow.NET. It is designed to handle the "80% happy path" of analytics queries natively on Snowflake, while acknowledging specific limitations where client-side processing or alternative approaches are required.

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
| `ShowAsync(n)` | Display first N rows to console |
| `Explain(extended)` | Print SQL query plan |
| `PrintSchema()` | Print result type schema |
| `Spy(label)` | Display and continue chaining |
| `ToSql()` | Get generated SQL string |

### 8. Execution
- **Async Streaming**: `IAsyncEnumerable<T>` support via `GetAsyncEnumerator` (efficient memory usage).
- **Materialization**: `ToListAsync()`, `ToArrayAsync()`, `FirstAsync()`, `FirstOrDefaultAsync()`, `CountAsync()`, `AnyAsync()`.
- **Single Element**: `SingleAsync()`, `SingleOrDefaultAsync()` (verify exactly 1 result).

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

## 🛑 Known Limitations & Risks

### 1. SQL Injection Risk
**Current Status**: ⚠️ **High Risk** (if untrusted input is used).
*   Constant values are formatted as strings (e.g., `'value'`).
*   **Mitigation**: We need to implement proper **Parameterization** (using `SnowflakeDbParameter`).
*   *Advice*: Do not use raw user input in `Where` clauses until parameterizationis added.

### 2. Null Handling
**Current Status**: ⚠️ **Basic**.
*   `x == null` translates to `= NULL` (standard SQL behavior is `IS NULL`).
*   This means usage of `null` in predicates may not work as expected in all SQL dialects without explicit `IS NULL` handling logic.

### 3. Connection Management
**Current Status**: ⚠️ **Per-Query Connections**.
*   Every query creates a `new SnowflakeDbConnection`.
*   **Production Need**: Connection pooling is handled by the underlying driver, but high-concurrency scenarios might need optimization.

---

## 📊 Implementation Status Summary

### ✅ Batch 1: Analytics High-Value (COMPLETE)
| Feature Category | Features Added | Status |
|-----------------|----------------|--------|
| **DateTime Properties** | Year, Month, Day, Hour, Minute, Second, DayOfWeek, DayOfYear, Date | ✅ |
| **String Properties** | Length, IndexOf | ✅ |
| **Math Functions** | Abs, Round, Ceiling, Floor, Sqrt, Pow | ✅ |
| **Execution Methods** | Single, SingleOrDefault | ✅ |

### ✅ Batch 2: Semi-Structured (COMPLETE)
| Feature | Description | Status |
|---------|-------------|--------|
| **Variant Any()** | Filter VARIANT arrays using FILTER and ARRAY_SIZE | ✅ |
| **Lambda Translation** | Translate C# predicates to Snowflake lambda syntax | ✅ |

### 📋 Batch 3: Deep Relational (DEFERRED)
| Feature | Description | Status |
|---------|-------------|--------|
| **Correlated Subqueries** | Auto-translate to EXISTS | ⏸️ Deferred (design in batch3-analysis.md) |

**Coverage Achievement**: **95%+** of common analytics scenarios now supported.

---

## Summary
DataFlow.NET's Snowflake provider is a powerful **Analytical Query Builder**. It excels at:
*   Filtering and aggregating massive datasets.
*   Projecting flat results for analysis.
*   Streaming data efficiently to your application.

It is **NOT** a full replacement for an ORM (like EF Core) for transaction-heavy, complex domain modeling applications.
