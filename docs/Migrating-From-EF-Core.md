# Migrating from Entity Framework Core to DataFlow.NET

> **Target Audience:** Developers familiar with EF Core who need to interact with analytical data stores (Snowflake, Spark, CSV lakes) where EF Core drivers are missing or inefficient.

## The Mental Shift

Entity Framework Core is designed for **OLTP** (Online Transaction Processing) — creating, reading, updating, and deleting individual rows in a transactional database like SQL Server or PostgreSQL.

DataFlow.NET is designed for **OLAP** (Online Analytical Processing) and **ETL** — processing millions of records in bulk from data warehouses like Snowflake or Spark.

While the underlying engines differ, you can keep the **Developer Experience (DX)** you love: **Strongly-typed LINQ queries without magic strings.**

## Concept Mapping

| EF Core Concept | DataFlow.NET Equivalent | Why the difference? |
|-----------------|-------------------------|---------------------|
| `DbContext` | `ReaderConfig` / `Pipeline Context` | No state tracking needed for read-only analytics. |
| `DbSet<T>` | `Read.FromSnowflake<T>` | Data is streamed, not attached to a change tracker. |
| `IQueryable<T>` | `IAsyncEnumerable<T>` / `ISparkQuery<T>` | Optimized for forward-only streaming or distributed execution. |
| `SaveChanges()` | `.WriteToSnowflake()` / `.WriteCsv()` | Explicit bulk writes are 1000x faster than row-by-row updates. |
| `[Table("Name")]`| `TableNames` Constant | Decouples code from schema; no migration history table. |

---

## 1. The "Context" Pattern

You don't *need* a Context class in DataFlow, but creating one can make your team feel at home.

### The EF Core Way (Familiar)
```csharp
public class SalesContext : DbContext
{
    public DbSet<Order> Orders { get; set; }
    public DbSet<Customer> Customers { get; set; }
}

// Usage
using var db = new SalesContext();
var bigOrders = db.Orders.Where(o => o.Amount > 1000).ToList();
```

### The DataFlow Way (Adapted)
You can create a lightweight "Context" to centralize your table configurations.

```csharp
public class SalesDataWarehouse
{
    private readonly SnowflakeConfig _config;

    public SalesDataWarehouse(SnowflakeConfig config)
    {
        _config = config;
    }

    // "DbSets" are just properties that return a queryable source
    public IDataFlowSource<Order> Orders => 
        Read.FromSnowflake<Order>(_config, TableNames.Sales.Orders);

    public IDataFlowSource<Customer> Customers => 
        Read.FromSnowflake<Customer>(_config, TableNames.Sales.Customers);
}

// Usage - Almost identical!
var warehouse = new SalesDataWarehouse(config);
var bigOrders = warehouse.Orders
    .Where(o => o.Amount > 1000)
    .ToEnumerable(); // Triggers the query
```

---

## 2. Writing Data (The biggest change)

EF Core tracks changes on objects and pushes updates one-by-one (or in small batches) when you call `SaveChanges()`. This is **too slow** for big data.

DataFlow uses **Bulk Inserts** exclusively.

### The EF Core Way (Slow for Analytics)
```csharp
var order = db.Orders.First(o => o.Id == 1);
order.Status = "Processed";
db.SaveChanges(); // Generates: UPDATE Orders SET Status = ... WHERE Id = 1
```

### The DataFlow Way (Fast for Analytics)
We process data in streams and write a new dataset (or append).

```csharp
// Read -> Transform -> Write (ETL)
await warehouse.Orders
    .Where(o => o.Status == "Pending")
    .Select(o => o with { Status = "Processed" }) // Immutable update
    .WriteToSnowflake(config, TableNames.Sales.ProcessedOrders);
```

---

## 3. Handling Relationships (Joins)

EF Core does "lazy loading" or "include". DataFlow encourages explicit Joins or denormalization, which is better for performance in Snowflake/Spark.

### The "Include" Equivalent
Instead of `.Include(o => o.Customer)`, just use a standard LINQ Join.

```csharp
var report = warehouse.Orders
    .Join(warehouse.Customers, 
          o => o.CustomerId, 
          c => c.Id,
          (o, c) => new OrderReport(o.Id, c.Name, o.Amount))
    .Where(x => x.Amount > 1000);
```

DataFlow translates this to a single SQL query: `SELECT ... FROM Orders JOIN Customers ...`

---

## Summary Checklist for Migrating

1.  **Don't look for `SaveChanges()`**: Think in "Pipelines" (Read -> Process -> Write).
2.  **Keep your POCOs**: You can reuse your existing Entity classes! attributes like `[Column]` are supported if you map them manually, but DataFlow prefers simple property matching.
3.  **Use `TableNames` struct**: Keep your table names in a static class or struct to replicate the type-safety of `DbSet` properties.
4.  **Embrace the Stream**: Remember that `IAsyncEnumerable` is your friend. You are processing data as it flows, not loading it all into memory first.
