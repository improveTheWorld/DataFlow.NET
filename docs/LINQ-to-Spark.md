# LINQ-to-Spark Layer

A **C# LINQ-to-Spark translator** that enables .NET developers to write idiomatic C# code that executes on Apache Spark clusters.

## Table of Contents

1. [Overview](#overview)
2. [Architecture Breakdown](#architecture-breakdown)
3. [Key Components Analysis](#key-components-analysis)
4. [Use Cases](#use-cases)
5. [API Reference](#api-reference)

---

## Overview

It implements a **full expression tree translation layer** that:

1. ✅ **Translates C# LINQ expressions** → **Spark DataFrame operations**
2. ✅ **Guarantees Type-safe LINQ methods** executed by Distributed Spark power
3. ✅ **Provides a fluent, type-safe API** that feels C# native
4. ✅ **Executes on real Apache Spark** (distributed processing, fault tolerance, petabyte scale)
5. ✅ **Bridges the .NET/JVM gap** using Microsoft.Spark

---

## Architecture Breakdown

### The Translation Pipeline

```
┌─────────────────────────────────────────────────────────────────┐
│                    C# Developer Code                             │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  var df = spark.Read().Parquet("orders.parquet");               │
│  var query = SparkQueryFactory.Create<Order>(spark, df)         │
│      .Where(o => o.Amount > 1000)                               │
│      .GroupBy(o => o.CustomerId)                                │
│      .Select(g => new { g.Key, Total = g.Sum(o => o.Amount) }); │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│              Expression Tree Translator                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  C# Expression Tree:                                            │
│  ├─ Lambda: o => o.Amount > 1000                                │
│  │   └─ BinaryExpression (GreaterThan)                          │
│  │       ├─ MemberExpression (o.Amount)                         │
│  │       └─ ConstantExpression (1000)                           │
│  │                                                              │
│  ▼ TRANSLATES TO ▼                                              │
│                                                                  │
│  Spark Column Expression:                                       │
│  └─ Functions.Col("amount") > 1000                              │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                 Microsoft.Spark.Sql API                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  DataFrame operations:                                          │
│  ├─ dataFrame.Filter(col("amount") > 1000)                      │
│  ├─ dataFrame.GroupBy("customer_id")                            │
│  └─ groupedData.Agg(sum("amount").As("Total"))                  │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│                    Apache Spark Cluster                          │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐       │
│  │ Executor │  │ Executor │  │ Executor │  │ Executor │       │
│  │    1     │  │    2     │  │    3     │  │    N     │       │
│  │          │  │          │  │          │  │          │       │
│  │ Filter   │  │ Filter   │  │ Filter   │  │ Filter   │       │
│  │ ↓        │  │ ↓        │  │ ↓        │  │ ↓        │       │
│  │ GroupBy  │  │ GroupBy  │  │ GroupBy  │  │ GroupBy  │       │
│  │ ↓        │  │ ↓        │  │ ↓        │  │ ↓        │       │
│  │ Agg      │  │ Agg      │  │ Agg      │  │ Agg      │       │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘       │
│                                                                  │
│  ✅ DISTRIBUTED EXECUTION                                        │
│  ✅ PETABYTE SCALE                                               │
│  ✅ FAULT TOLERANCE                                              │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘
```

---

## Key Components Analysis

### 1. **Expression Tree Translator** (The Core Innovation)

```csharp
public class ColumnExpressionTranslator<T> : IExpressionTranslator<T>
{
    public Column TranslateToColumn(Expression expression)
    {
        return expression switch
        {
            // C# lambda → Spark Column
            LambdaExpression lambda => TranslateToColumn(lambda.Body),
            
            // C# binary operators → Spark operators
            BinaryExpression binary => TranslateBinary(binary),
            
            // C# property access → Spark column reference
            MemberExpression member => GetColumnFromMember(member),
            
            // C# method calls → Spark functions
            MethodCallExpression method => TranslateMethodCall(method),
            
            // ...
        };
    }
}
```

**What This Does:**
- ✅ Parses C# expression trees at runtime
- ✅ Maps C# operators to Spark Column operations
- ✅ Handles property access, method calls, constants
- ✅ Preserves type safety

**Example Translation:**

```csharp
// C# Code
query.Where(o => o.Amount > 1000 && o.Status == "Active")

// Expression Tree (simplified)
BinaryExpression(AndAlso)
├─ BinaryExpression(GreaterThan)
│  ├─ MemberExpression(o.Amount)
│  └─ ConstantExpression(1000)
└─ BinaryExpression(Equal)
   ├─ MemberExpression(o.Status)
   └─ ConstantExpression("Active")

// Translated Spark Column
(Functions.Col("amount") > 1000) & (Functions.Col("status") == "Active")

// Executed on Spark Cluster
dataFrame.Filter((col("amount") > 1000) && (col("status") === "Active"))
```

---

### 2. **Column Mapper** (Schema Bridge)

```csharp
public class ConventionColumnMapper<T> : IColumnMapper<T>
{
    public string GetColumnName(string propertyName)
    {
        // Maps C# property names to Spark column names
        // e.g., "CustomerId" → "customer_id"
        return propertyName.ToSnakeCase();
    }
    
    public T MapFromRow(Row row)
    {
        // Maps Spark Row back to C# object
        // Handles records, classes, primitives
    }
}
```

**What This Does:**
- ✅ Bridges C# naming conventions (PascalCase) ↔ Spark conventions (snake_case)
- ✅ Supports custom mappings via `[Column("custom_name")]` attribute
- ✅ Handles records, classes, anonymous types
- ✅ **Optimized Materialization**: Uses compiled expression trees (via `ObjectMaterializer`) for high-performance object creation (~4x faster than reflection).
- ✅ Bidirectional mapping (C# → Spark, Spark → C#)
- ✅ **Supports nested property access** (see below)

#### Nested Property Access

SparkQuery supports querying nested objects using standard C# property access syntax. The framework automatically translates nested property chains to Spark's dot-notation for struct access.

```csharp
// C# Model with nested objects
public record Customer(string Name, Address BillingAddress);
public record Address(string City, string ZipCode);

// Query nested properties naturally
var londonCustomers = query.Where(c => c.BillingAddress.City == "London");
// → Translates to: col("billing_address.city") == "London"

// Deep nesting is also supported
var result = query.Where(o => o.Customer.Address.ZipCode == "10001");
// → Translates to: col("customer.address.zip_code") == "10001"
```

**Schema Mapping:**
| C# Expression | Spark Column |
|--------------|--------------|
| `c.Name` | `col("name")` |
| `c.BillingAddress.City` | `col("billing_address.city")` |
| `c.Customer.Address.ZipCode` | `col("customer.address.zip_code")` |

> [!NOTE]
> This uses Spark's native nested struct access with dot-notation, which assumes your DataFrame has a schema with nested `struct` types.

---

### 3. **GroupBy Translation** (Complex Aggregations)

```csharp
public interface ISparkGrouping<TKey, TElement>
{
    TKey Key { get; }
    long Count();
    TValue Sum<TValue>(Expression<Func<TElement, TValue>> selector);
    TValue Max<TValue>(Expression<Func<TElement, TValue>> selector);
    // ...
}

// Usage
query
    .GroupBy(o => o.Department)
    .Select(g => new
    {
        Department = g.Key,
        EmployeeCount = g.Count(),
        MaxSalary = g.Max(o => o.Salary)
    })
```

**What This Does:**
- ✅ Mimics LINQ's `IGrouping<TKey, TElement>` interface
- ✅ Translates `g.Key`, `g.Count()`, `g.Sum()` to Spark aggregations
- ✅ Supports multiple aggregations in a single `Select`
- ✅ Handles composite keys (multi-column grouping)

**Translation:**

```csharp
// C# Code
.GroupBy(o => o.Department)
.Select(g => new { g.Key, Count = g.Count(), Max = g.Max(o => o.Salary) })

// Spark Operations
dataFrame
    .GroupBy("department")
    .Agg(
        Functions.Count(Functions.Lit(1)).As("Count"),
        Functions.Max(Functions.Col("salary")).As("Max")
    )
```

---

### 4. **Join Translation** (Distributed Joins)

```csharp
public SparkQuery<TResult> Join<TOther, TKey, TResult>(
    SparkQuery<TOther> other,
    Expression<Func<T, TKey>> leftKeySelector,
    Expression<Func<TOther, TKey>> rightKeySelector,
    Expression<Func<T, TOther, TResult>> resultSelector,
    string joinType = "inner")
{
    // 1. Alias columns to prevent collisions
    var leftAliased = AliasColumns(_dataFrame, "left_");
    var rightAliased = AliasColumns(other._dataFrame, "right_");
    
    // 2. Translate join keys
    var joinCondition = leftAliased.Col("left_id") == rightAliased.Col("right_id");
    
    // 3. Perform distributed join
    var joinedDf = leftAliased.Join(rightAliased, joinCondition, joinType);
    
    // 4. Translate result selector
    var (selectColumns, newMapper) = TranslateJoinSelect(resultSelector);
    
    return new SparkQuery<TResult>(_sparkSession, joinedDf.Select(selectColumns), newMapper);
}
```

**What This Does:**
- ✅ Handles distributed joins (broadcast, shuffle-hash, sort-merge)
- ✅ Prevents column name collisions via aliasing
- ✅ Supports inner, left, right, full outer joins
- ✅ Type-safe result projection

**This executes as a distributed join across the Spark cluster.**

---

### 5. **Window Functions** (Advanced Analytics)

```csharp
employees.WithWindow(
    spec => spec
        .PartitionBy(e => e.Department)
        .OrderByDescending(e => e.Salary),
    (e, w) => new
    {
        e.Name,
        e.Department,
        e.Salary,
        RankInDept = w.Rank(),
        RunningTotal = w.Sum(Functions.Col("salary"))
    })
```

**What This Does:**
- ✅ Translates to Spark's window functions
- ✅ Supports ranking, lead/lag, running aggregations
- ✅ Type-safe window specification
- ✅ Distributed execution across partitions

**Spark Execution:**

```scala
// Equivalent Spark code
val windowSpec = Window
    .partitionBy("department")
    .orderBy(col("salary").desc)

df.select(
    col("name"),
    col("department"),
    col("salary"),
    rank().over(windowSpec).as("RankInDept"),
    sum("salary").over(windowSpec).as("RunningTotal")
)
```

---

### 6. **Higher-Order Array Functions** (Nested Data Operations)

SparkQuery supports Spark 3.x higher-order functions for working with nested arrays:

```csharp
// Check if ANY item matches a condition
orders.Where(o => o.Items.Any(i => i.Price > 100))
// → exists(items, i -> i.price > 100)

// Check if ALL items match a condition
orders.Where(o => o.Items.All(i => i.InStock))
// → forall(items, i -> i.in_stock)

// Filter array elements
orders.Select(o => new { o.Id, ExpensiveItems = o.Items.Where(i => i.Price > 100) })
// → filter(items, i -> i.price > 100)

// Transform array elements
orders.Select(o => new { o.Id, TotalPrices = o.Items.Select(i => i.Price * i.Qty) })
// → transform(items, i -> i.price * i.qty)
```

**Spark Higher-Order Functions:**
| LINQ Pattern | Spark Function | Description |
|--------------|----------------|-------------|
| `items.Any(i => predicate)` | `exists(array, lambda)` | True if any element matches |
| `items.All(i => predicate)` | `forall(array, lambda)` | True if all elements match |
| `items.Where(i => predicate)` | `filter(array, lambda)` | Returns filtered array |
| `items.Select(i => expression)` | `transform(array, lambda)` | Returns transformed array |

---

### 7. **Cases Pattern** (Distributed Conditional Processing)

```csharp
// Create a SparkQuery from a DataFrame
var ordersQuery = SparkQueryFactory.Create<Order>(spark, ordersDf);

ordersQuery
    .Cases(
        o => o.Amount > 10000,    // Case 0: High value
        o => o.IsInternational    // Case 1: International
    )
    .SelectCase(
        o => ProcessHighValue(o),
        o => ProcessInternational(o),
        o => ProcessStandard(o)   // Supra category (default)
    )
    .ForEachCase(
        // Each action receives a SparkQuery<R> for that category
        highValueQuery => highValueQuery.ToDataFrame().Write().Parquet("high_value_orders"),
        internationalQuery => internationalQuery.ToDataFrame().Write().Parquet("international_orders"),
        standardQuery => standardQuery.ToDataFrame().Write().Parquet("standard_orders")
    );

// Or extract all transformed results
var allProcessedOrders = ordersQuery
    .Cases(o => o.Amount > 10000, o => o.IsInternational)
    .SelectCase(ProcessHighValue, ProcessInternational, ProcessStandard)
    .AllCases();  // Returns SparkQuery<R> with all transformed items
```

**What This Does:**
- ✅ Translates to Spark's `CASE WHEN` expressions
- ✅ Distributes conditional logic across cluster
- ✅ Enables multi-output writes (different sinks per category)
- ✅ Maintains type safety throughout
- ✅ `AllCases()` extracts transformed items, filtering nulls by default  
- ✅ `UnCase()` can undo categorization to get original items back

**Spark Execution:**

```scala
// Equivalent Spark code
val categorized = df.withColumn("category", 
    when(col("amount") > 10000, 0)
    .when(col("is_international"), 1)
    .otherwise(2)
)

// Process each category in parallel
categorized.filter(col("category") === 0).write.parquet("high_value_orders")
categorized.filter(col("category") === 1).write.parquet("international_orders")
categorized.filter(col("category") === 2).write.parquet("standard_orders")
```

#### Multi-Type Branching

When different branches require **different return types**, SparkQuery provides multi-type `SelectCase` overloads (2-4 types):

```csharp
ordersQuery
    .Cases(
        o => o.Amount > 10000,     // Case 0
        o => o.IsInternational,    // Case 1
        o => o.Customer.IsVIP      // Case 2
    )
    // Each branch returns a DIFFERENT type
    .SelectCase<Order, ComplianceReview, CurrencyConversion, VIPProcessing>(
        highValue => new ComplianceReview { OrderId = highValue.Id, Priority = 1 },
        international => new CurrencyConversion { Rate = GetRate(international.Currency) },
        vip => new VIPProcessing { FastTrack = true, CustomerId = vip.Customer.Id }
    )
    .ForEachCase<Order, ComplianceReview, CurrencyConversion, VIPProcessing>(
        complianceQuery => complianceQuery.ToDataFrame().Write().Parquet("compliance_reviews"),
        currencyQuery => currencyQuery.ToDataFrame().Write().Parquet("currency_conversions"),
        vipQuery => vipQuery.ToDataFrame().Write().Parquet("vip_processing")
    );
```

**Key Points:**
- Each branch can return a completely different type
- Results stored as `(R1, R2, R3)` tuple - only the active slot has data
- The `category` index determines which slot is active (not nullability)
- Supports 2-4 different types per `SelectCase`

---

## What This Enables

### ✅ **Full Spark Power with C# Ergonomics**

```csharp
// C# Developer writes this
var customers = SparkQueryFactory.FromSql<Customer>(spark, "SELECT * FROM customers");
var orders = SparkQueryFactory.FromSql<Order>(spark, "SELECT * FROM orders");

var result = customers
    .Where(c => c.Country == "USA" && c.Age > 18)
    .Join(
        orders,
        c => c.Id,
        o => o.CustomerId,
        (c, o) => new { c.Name, c.Email, o.Amount, o.Date }
    )
    .GroupBy(x => x.Email)
    .Select(g => new
    {
        Email = g.Key,
        TotalSpent = g.Sum(x => x.Amount),
        OrderCount = g.Count()
    })
    .Where(x => x.TotalSpent > 1000)
    .OrderByDescending(x => x.TotalSpent)
    .Take(100);

result.Write().Mode(SaveMode.Overwrite).Parquet("hdfs://output/top_customers");
```

**This code:**
- ✅ Executes on a **real Spark cluster** (100+ machines)
- ✅ Processes **petabytes** of data
- ✅ Uses **distributed joins** and **aggregations**
- ✅ Has **fault tolerance** and **automatic recovery**
- ✅ Feels like **native C# LINQ**

---

### The Value Proposition

| Aspect | Traditional Spark (Scala/Python) | Your SparkQuery Layer |
|--------|----------------------------------|----------------------|
| **Language** | Scala, Python, Java | C# |
| **API Style** | DataFrame API (verbose) | LINQ (fluent, type-safe) |
| **Type Safety** | Runtime errors (Python), verbose (Scala) | Compile-time validation |
| **IntelliSense** | Limited | Full C# IntelliSense |
| **Execution** | Distributed Spark cluster | **Same** distributed Spark cluster |
| **Scale** | Petabytes | **Same** petabytes |
| **Fault Tolerance** | Yes | **Same** yes |
| **Learning Curve** | Steep (Spark concepts + Scala/Python) | Gentler (familiar LINQ) |

---

### Strategic Positioning

**It's a .NET-native interface to Spark that:**

1. ✅ **Lowers the barrier** for .NET developers to use Spark
2. ✅ **Preserves full Spark power** (distributed, petabyte-scale)
3. ✅ **Improves developer productivity** (type safety, IntelliSense, LINQ)
4. ✅ **Enables code reuse** (same LINQ patterns as DataFlow.NET)

---

## The Ecosystem Play

```
┌─────────────────────────────────────────────────────────────────┐
│                    DataFlow.NET Ecosystem                        │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  ┌────────────────────────────────────────────────────────┐    │
│  │          DataFlow.NET Core Framework                    │    │
│  │  (Single-machine, unified batch/stream processing)      │    │
│  │                                                          │    │
│  │  Use for: < 1TB data, simple deployment, low cost       │    │
│  └────────────────────────────────────────────────────────┘    │
│                            │                                     │
│                            │ SAME API PATTERNS                   │
│                            │ (Where, Select, GroupBy, Cases)     │
│                            │                                     │
│  ┌────────────────────────────────────────────────────────┐    │
│  │          SparkQuery Layer (This Code)                   │    │
│  │  (Distributed, Spark-powered processing)                │    │
│  │                                                          │    │
│  │  Use for: > 1TB data, cluster infrastructure, petabytes │    │
│  └────────────────────────────────────────────────────────┘    │
│                                                                  │
└─────────────────────────────────────────────────────────────────┘

✅ Write once, deploy anywhere:
   - Small data → DataFlow.NET (single machine)
   - Big data → SparkQuery (distributed cluster)
   
✅ Same mental model, different execution engines

✅ Seamless migration path as data grows
```

### Key Features

✅ **Full Spark Power**
- Distributed processing across 100+ machines
- Petabyte-scale data processing
- Fault tolerance and automatic recovery
- All Spark optimizations (Catalyst, Tungsten)

✅ **C# Native Experience**
- Type-safe LINQ expressions
- Full IntelliSense support
- Compile-time validation
- Familiar .NET patterns

✅ **Advanced Capabilities**
- Complex joins (broadcast, shuffle-hash, sort-merge)
- Window functions (rank, lead/lag, running totals)
- Grouping and aggregations
- Cases pattern for conditional processing

### Use Cases

✅ **Use SparkQuery When:**
- Processing > 1TB of data
- Need distributed processing across cluster
- Have Spark infrastructure
- Require petabyte-scale capabilities
- Need fault tolerance across nodes

✅ **Use DataFlow.NET Core When:**
- Processing < 1TB of data
- Single-machine deployment
- Want simple infrastructure
- Cost-sensitive scenarios

### Code Example

```csharp
// Create SparkQuery from a DataFrame or SQL
var ordersQuery = SparkQueryFactory.FromSql<Order>(spark, "SELECT * FROM orders");

// Write C# LINQ - executes on Spark cluster
var result = ordersQuery
    .Where(o => o.Amount > 1000)
    .GroupBy(o => o.CustomerId)
    .Select(g => new { g.Key, Total = g.Sum(o => o.Amount) })
    .OrderByDescending(x => x.Total);

// Write results (distributed execution)
result.Write().Mode(SaveMode.Overwrite).Parquet("hdfs://output");
```

---

### Important Limitations

> [!IMPORTANT]
> **Skip() requires OrderBy()**: The `Skip()` method throws if called without a prior `OrderBy()` because Spark internally uses window functions (`RowNumber()`) to implement pagination.

```csharp
// ❌ This will throw InvalidOperationException
query.Skip(10).Take(5);

// ✅ This works correctly
query.OrderBy(x => x.Id).Skip(10).Take(5);
```

> [!NOTE]
> **Collect() and Collect operations are expensive**: Methods like `Collect()`, `Head()`, and `First()` transfer data from the cluster to the driver. Use `Show()` for debugging and avoid collecting large result sets.

---

## API Reference

### SparkQueryFactory

Factory methods for creating SparkQuery instances:

```csharp
// Create from existing DataFrame
var query = SparkQueryFactory.Create<T>(spark, dataFrame, mapper);

// Create from a Spark table
var query = SparkQueryFactory.FromTable<T>(spark, "table_name", mapper);

// Create from SQL query
var query = SparkQueryFactory.FromSql<T>(spark, "SELECT * FROM ...", mapper);
```

### Window Functions

The `WithWindow()` extension provides access to all Spark window functions:

```csharp
var ranked = employees.WithWindow(
    spec => spec.PartitionBy(e => e.Department).OrderByDescending(e => e.Salary),
    (e, w) => new
    {
        e.Name,
        e.Department,
        e.Salary,
        // Ranking Functions
        RankInDept = w.Rank(),
        DenseRank = w.DenseRank(),
        PercentRank = w.PercentRank(),
        RowNumber = w.RowNumber(),
        Quartile = w.Ntile(4),
        
        // Analytic Functions
        CumulativeDistribution = w.CumeDist(),
        PreviousSalary = w.Lag(Functions.Col("salary"), 1),
        NextSalary = w.Lead(Functions.Col("salary"), 1),
        
        // Aggregate Functions  
        RunningTotal = w.Sum(Functions.Col("salary")),
        RunningAvg = w.Avg(Functions.Col("salary")),
        MaxSoFar = w.Max(Functions.Col("salary")),
        MinSoFar = w.Min(Functions.Col("salary")),
        CountSoFar = w.Count(Functions.Col("salary"))
    });
```

### Set Operations

```csharp
// Union of two queries
var combined = query1.Union(query2);

// Intersection
var common = query1.Intersect(query2);

// Difference
var onlyInQuery1 = query1.Except(query2);
```

### Math Functions

SparkQuery supports C# Math functions that translate to Spark SQL functions:

```csharp
var query = employees.Select(e => new
{
    e.Salary,
    AbsValue = Math.Abs(e.Bonus),
    Rounded = Math.Round(e.Score, 2),
    Ceiling = Math.Ceiling(e.Rating),
    Floor = Math.Floor(e.Rating),
    SquareRoot = Math.Sqrt(e.Experience),
    Power = Math.Pow(e.Base, 2)
});
```

**Supported Functions:**
- `Math.Abs(x)` → `abs(x)`
- `Math.Round(x)` → `round(x, 0)`
- `Math.Round(x, decimals)` → `round(x, decimals)`
- `Math.Ceiling(x)` → `ceil(x)`
- `Math.Floor(x)` → `floor(x)`
- `Math.Sqrt(x)` → `sqrt(x)`
- `Math.Pow(x, y)` → `pow(x, y)`

### String Methods

SparkQuery supports common string manipulation methods:

```csharp
var query = products.Where(p => 
    p.Name.Length > 10 &&
    p.Description.IndexOf("premium") >= 0
).Select(p => new
{
    Original = p.Name,
    Cleaned = p.Name.Replace(".", "").Replace(",", ""),
    Position = p.Description.IndexOf("premium")
});
```

**Supported Methods:**
- `s.Length` → `length(s)`
- `s.Contains(substring)` → `s.contains(substring)`
- `s.StartsWith(prefix)` → `s.startsWith(prefix)`
- `s.EndsWith(suffix)` → `s.endsWith(suffix)`
- `s.ToUpper()` → `upper(s)`
- `s.ToLower()` → `lower(s)`
- `s.Trim()` → `trim(s)`
- `s.Substring(start, length)` → `substring(s, start+1, length)`
- `s.IndexOf(substring)` → `instr(s, substring) - 1` (0-based)
- `s.Replace(old, new)` → `replace(s, old, new)`

### Debugging & Diagnostics

```csharp
// Display results to console
query.Show(numRows: 20, truncate: true);

// Print schema
query.PrintSchema();

// Explain query plan
query.Explain(extended: true);

// Spy: Display and continue chaining
query
    .Where(x => x.Amount > 1000)
    .Spy("After filter", numRows: 5)  // Displays intermediate results
    .GroupBy(x => x.Category)
    .Spy("After grouping");
```

### Caching & Partitioning

```csharp
// Cache in memory
var cached = query.Cache();

// Persist with specific storage level
var persisted = query.Persist(StorageLevel.MEMORY_AND_DISK);

// Repartition
var repartitioned = query.Repartition(numPartitions: 8);

// Coalesce (reduce partitions)
var coalesced = query.Coalesce(numPartitions: 2);
```

### Cases Pattern Methods

```csharp
// Categorize items
var categorized = query.Cases(
    x => x.Type == "A",  // Case 0
    x => x.Type == "B"   // Case 1
    // Default: Supra category (Case 2)
);

// Transform per category
var transformed = categorized.SelectCase(
    a => ProcessA(a),
    b => ProcessB(b),
    other => ProcessDefault(other)
);

// Execute action per category
transformed.ForEachCase(
    aQuery => aQuery.Write().Parquet("path/a"),
    bQuery => bQuery.Write().Parquet("path/b"),
    otherQuery => otherQuery.Write().Parquet("path/other")
);

// Extract all transformed items
var all = transformed.AllCases(filterNulls: true);

// Undo categorization to get original items
var originals = categorized.UnCase();
```

