# Changelog — DataFlow.Snowflake

All notable changes to the DataFlow.Snowflake package.

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

```csharp
// All now work correctly:
.Select(o => o.Amount > 1000 ? "High" : "Low")    // REG003: Ternary
.Select(o => new DTO { Name = o.Name })           // REG004: Object init
.Select(o => o.FirstName + " " + o.LastName)     // REG009: String concat
.Select(o => o.Name ?? "Unknown")                 // REG014: Null coalescing
```

#### Type Coercion
- **Int64→Int32**: Snowflake NUMBER columns (returned as Int64) now coerced to C# Int32 properties
- **Anonymous types**: Fixed coercion for read-only properties in anonymous types
  ```csharp
  // Now works correctly:
  .Select(o => new { o.Id, o.Amount })  // Anonymous type with Int32 Id
  ```

#### SELECT Projection Fix
- **Constant Inlining**: Constants in SELECT now inlined as SQL literals instead of bind variables (`:p0`)
  ```csharp
  // Before: SELECT 'Active' AS status WHERE :p0 (ERROR)
  // After:  SELECT 'Active' AS status (WORKS)
  .Select(o => new { Status = "Active", o.Amount })
  ```

### ✨ Features Verified

#### Nested Objects (VARIANT)
Full support for Snowflake VARIANT columns with native C# property access:

```csharp
public class Order {
    public int Id { get; set; }
    
    [Variant]  // Marks column as VARIANT
    public OrderData Data { get; set; }
}

// Query nested properties - translates to colon syntax
context.Read.Table<Order>("ORDERS")
    .Where(o => o.Data.Customer.City == "Paris")
// SQL: WHERE data:customer:city = 'Paris'
```

**Supported Patterns:**
- `o.Address.City` → `address:city`
- `o.Data.Customer.City` → `data:customer:city` (multi-level)
- Works in `Where`, `Select`, and comparison operators

### 🧪 Quality

- **122 tests passed, 0 failed, 0 skipped** (package integration)
- Added `NestedObjectTests.cs` - 6 tests for VARIANT/nested object feature
- Fixed previously skipped `WriteTables` and `MergeTables` tests with auto-table creation
- Added `SnowflakeDebug` playground for iterative bug tracing

### ⚠️ Known Limitations

- **GroupBy API**: Full GroupBy+aggregation may require explicit syntax
- **VARIANT Arrays**: `Any()`, `All()`, `Where()`, `Select()` on VARIANT arrays supported, but not method calls inside lambda

---

## [1.2.0] — 2026-01-30

### 🚀 Breaking Changes
- **Removed `Snowflake.Table<T>(options, tableName)`**: Use `Snowflake.Connect(...).Read.Table<T>(tableName)` instead
- **Removed Legacy Write APIs**:
  - `WriteTable(source, options, tableName)` → Use `WriteTable(source, context, tableName)`
  - `MergeTable(source, options, tableName, matchKey)` → Use `MergeTable(source, context, tableName, matchKey)`

### 🐛 Bug Fixes
- **Package Bundling Fixed**: `DataFlow.Licensing.dll` now correctly included
- **Development Tier Detection**: Runtime detection via `Debugger.IsAttached` and environment variables
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
