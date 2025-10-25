
## Linq-to-Spark Layer
that enables .NET developers to write idiomatic C# code that executes on Apache Spark clusters.

It's a **C# LINQ-to-Spark translator** that lets .NET developers write **idiomatic C# code** that **executes on a real Spark cluster**.
It implements a **full expression tree translation layer** that:

1. ✅ **Translates C# LINQ expressions** → **Spark DataFrame operations**
2. ✅ **Garantees Type-safe LINQ methods** executed by a Distributed Spark power
3. ✅ **Provides a fluent, type-safe API** that feels C# native, 
4. ✅ **Executes on real Apache Spark** (distributed processing, fault tolerance, petabyte scale))
5. ✅ **Bridges the .NET/JVM gap** using Microsoft.Spark


---

## Architecture Breakdown

### The Translation Pipeline

```
┌─────────────────────────────────────────────────────────────────┐
│                    C# Developer Code                             │
├─────────────────────────────────────────────────────────────────┤
│                                                                  │
│  var query = spark.Read.Csv<Order>("orders.csv")                │
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
- ✅ Bidirectional mapping (C# → Spark, Spark → C#)

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

### 6. **Cases Pattern** (Distributed Conditional Processing)

```csharp
await spark.Read.Csv<Order>("orders.csv")
    .Cases(
        o => o.Amount > 10000,    // Case 0: High value
        o => o.IsInternational    // Case 1: International
    )
    .SelectCase(
        o => ProcessHighValue(o),
        o => ProcessInternational(o),
        o => ProcessStandard(o)
    )
    .ForEachCase(
        highValue => highValue.Write().Parquet("high_value_orders"),
        international => international.Write().Parquet("international_orders"),
        standard => standard.Write().Parquet("standard_orders")
    );
```

**What This Does:**
- ✅ Translates to Spark's `CASE WHEN` expressions
- ✅ Distributes conditional logic across cluster
- ✅ Enables multi-output writes (different sinks per category)
- ✅ Maintains type safety throughout

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

---

## What This Enables

### ✅ **Full Spark Power with C# Ergonomics**

```csharp
// C# Developer writes this
var result = spark.Read.Parquet<Customer>("hdfs://customers")
    .Where(c => c.Country == "USA" && c.Age > 18)
    .Join(
        spark.Read.Parquet<Order>("hdfs://orders"),
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
// Write C# LINQ
var result = spark.Read.Parquet<Order>("hdfs://orders")
    .Where(o => o.Amount > 1000)
    .GroupBy(o => o.CustomerId)
    .Select(g => new { g.Key, Total = g.Sum(o => o.Amount) })
    .OrderByDescending(x => x.Total);

// Executes on Spark cluster (distributed)
result.Write().Parquet("hdfs://output");
```
