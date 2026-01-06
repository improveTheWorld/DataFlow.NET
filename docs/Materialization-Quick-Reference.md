# Materialization Quick Reference

**Design classes/records for CSV, JSON, YAML, Snowflake, and Spark**

When using DataFlow readers like `Read.Csv<T>()`, `Read.Json<T>()`, or `Read.Yaml<T>()`, you must define a **target type** (`T`) to receive the data. The reader automatically maps source fields to your type's properties or constructor parameters. This guide covers the rules each reader expects.

---

## ✅ What Works

| Pattern | CSV | JSON | YAML | Snowflake | Spark |
|---------|:---:|:----:|:----:|:---------:|:-----:|
| Mutable class `{ get; set; }` | ✅ | ✅ | ✅ | ✅ | ✅ |
| Positional record `(int Id, string Name)` | ✅ | ✅ | ❌ | ✅ | ✅ |
| Init-only `{ get; init; }` | ✅ | ✅ | ❌ | ✅ | ✅ |
| Private setter `{ get; private set; }` | ✅ | ❌ | ❌ | ✅ | ✅ |
| Public fields | ✅ | ❌ | ❌ | ✅ | ✅ |

> [!IMPORTANT]
> **CSV** uses custom parser that fully uses ObjectMaterializer capacities.
> **JSON** uses System.Text.Json (requires public setters, no fields).
> **YAML** uses YamlDotNet (requires mutable properties).
> **Snowflake** uses ObjectMaterializer (most flexible).
> **Spark** uses ObjectMaterializer (most flexible).

---

## ❌ What Fails

| Pattern | Result |
|---------|--------|
| Read-only properties `{ get; }` | Properties stay at default |
| No parameterless constructor (without matching ctor) | Exception |

---

## 🎯 Recommended Patterns

### Best: Mutable Class
```csharp
public class Person
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public decimal Salary { get; set; }
}
```
Works with **all readers** (CSV, JSON, YAML, Snowflake, Spark).

### Good: Positional Record
```csharp
public record Order(int Id, string Product, decimal Amount);
```
Works with CSV, JSON, Snowflake, Spark. **NOT YAML**.

### Good: Record with Properties
```csharp
public record Employee
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
}
```
Works with **all readers**.

---

## 🔗 Column Matching Rules

| Priority | Rule | Example |
|:--------:|------|---------|
| 1 | Exact (case-insensitive) | `name` → `Name` ✅ |
| 2 | snake_case → PascalCase | `user_name` → `UserName` ✅ |
| 3 | camelCase → PascalCase | `firstName` → `FirstName` ✅ |
| 4 | Fuzzy (≤2 edits) | `Nmae` → `Name` ✅ |

**Extra columns** → Ignored  
**Missing columns** → Default value

---

## 💡 Quick Examples

### CSV
```csharp
// ✅ Works
public class Row { public int Id { get; set; } public string Value { get; set; } = ""; }
public record Row(int Id, string Value);

// ❌ Fails (read-only)
public class Row { public int Id { get; } }
```

### JSON
```csharp
// ✅ Works - both patterns
public class Doc { public string Name { get; set; } = ""; }
public record Doc(string Name);
```

### YAML ⚠️
```csharp
// ✅ Works - mutable only
public class Config { public string DbUrl { get; set; } = ""; }

// ❌ Fails - positional record
public record Config(string DbUrl);
```

### Snowflake / Spark
```csharp
// ✅ Works - snake_case columns match PascalCase properties
public class Order { public int OrderId { get; set; } public decimal TotalAmount { get; set; } }
// Matches: order_id, total_amount

// Spark only: explicit [Column] attribute
[Column("custom_col")]
public string PropertyName { get; set; }
```

---

## 📋 Checklist

Before using `Read.Csv<T>()`, `Read.Json<T>()`, etc.:

- [ ] Has parameterless constructor (or matching primary constructor)
- [ ] Properties have setters (`{ get; set; }` or `{ get; init; }` for CSV/Snowflake/Spark)
- [ ] For YAML: use mutable classes, NOT positional records
- [ ] For JSON: use public setters only (no private setters or fields)
- [ ] Property names roughly match column/key names

---

## See Also

- [DataFlow-Data-Reading-Infrastructure.md](DataFlow-Data-Reading-Infrastructure.md) - Reader architecture overview
- [ObjectMaterializer.md](ObjectMaterializer.md) - Full API reference
- [LINQ-to-Snowflake-Capabilities.md](LINQ-to-Snowflake-Capabilities.md) - Snowflake query features
- [LINQ-to-Spark.md](LINQ-to-Spark.md) - Spark query features
