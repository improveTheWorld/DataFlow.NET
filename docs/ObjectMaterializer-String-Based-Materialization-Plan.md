# String-Based Materialization Enhancement

## Context

### Current Architecture

The DataFlow CSV reader processes data through two stages:

```
CSV File → [ConvertFieldValue] → object[] → [ObjectMaterializer.Create] → T
              (string→typed)       ↑boxing     (assigns to properties)
```

**Key Components:**
- **ConvertFieldValue** (`ReaderInfrastructure.cs`): Parses raw strings into typed values (`"123"` → `int 123`)
- **ObjectMaterializer**: Creates instances of `T` and assigns values to properties/constructor parameters
- **GeneralMaterializationSession**: Reusable session for high-throughput row processing

### The Problem

For strongly-typed models like `Read.Csv<Person>()`:
1. `ConvertFieldValue` infers type and parses (e.g., `"25.5"` → `double 25.5`)
2. Values are boxed into `object[]`
3. ObjectMaterializer assigns to target property, but **target type may differ from inferred type**

**Double Conversion Example:**
```
CSV value: "123.45"
    ↓
ConvertFieldValue infers: double (123.45)
    ↓
Target property: decimal Amount { get; set; }
    ↓
ObjectMaterializer MUST convert: double → decimal
```

This creates **redundant work**:
- **Conversion 1**: CSV reader parses `string` → inferred type (`double`)
- **Conversion 2**: ObjectMaterializer converts inferred type → target type (`decimal`)

ObjectMaterializer already knows target types from `T`'s properties. The intermediate inference is unnecessary and potentially lossy (e.g., `double` → `decimal` precision issues).

### The Solution

Add `CreateFromStrings(string?[])` that accepts raw strings and parses directly into target types:

```
CSV File → [ObjectMaterializer.CreateFromStrings] → T
              (string→T properties directly)
```

---

## Current Error Handling Analysis

### Exceptions Thrown by ObjectMaterializer

| Location | Exception Type | When Thrown | Context Provided |
|----------|----------------|-------------|------------------|
| `ConvertObject` | `FormatException` | String can't parse to target type | Value, source type, target type |
| `CtorMaterializationSession` | `InvalidOperationException` | Type has no accessible constructors | Type name |
| `MaterializationSession` | `InvalidOperationException` | No parameterless ctor for feed session | Type name |
| `CreateViaPrimaryConstructorWithSchema` | `InvalidOperationException` | Schema doesn't match any constructor | Schema columns, attempted ctors |

### What's Missing for CSV Reader Integration

| Needed by CSV Reader | Currently Provided | Gap |
|----------------------|-------------------|-----|
| **Column index** of failure | ❌ Not in exception | Need to add |
| **Column name** of failure | ❌ Not in exception | Need to add |
| **Row number** | ❌ (caller must track) | Acceptable (CSV tracks this) |
| **Parse failure vs schema mismatch** | ✅ Different exception types | OK |
| **Raw value that failed** | ✅ In FormatException message | OK (but needs parsing) |

---

## Phase 1: ObjectMaterializer Enhancement

### 1.1 Add Custom Exception with Full Context

**Why**: The CSV reader needs structured error information to:
- Log which column failed (by name and index)
- Include the raw value in error reports
- Make informed decisions (skip row vs. stop processing)
- Provide actionable error messages to users

**Proposed Exception:**

```csharp
/// <summary>
/// Exception thrown when a field cannot be converted during materialization.
/// Provides structured context for error handling by callers.
/// </summary>
public class MaterializationFieldException : Exception
{
    public int ColumnIndex { get; }
    public string ColumnName { get; }
    public string? RawValue { get; }
    public Type TargetType { get; }
    
    public MaterializationFieldException(
        int columnIndex,
        string columnName,
        string? rawValue,
        Type targetType,
        Exception? innerException = null)
        : base($"Column '{columnName}' (index {columnIndex}): Cannot convert '{rawValue}' to {targetType.Name}", innerException)
    {
        ColumnIndex = columnIndex;
        ColumnName = columnName;
        RawValue = rawValue;
        TargetType = targetType;
    }
}
```

### 1.2 Add `CreateFromStrings` Method

**Files to Modify:**

| File | Change |
|------|--------|
| [ObjectMaterializer.cs](file:///c:/CodeSource/DataFlow/src/DataFlow.Framework.ObjectMaterializer/ObjectMaterializer.cs) | Add `CreateFromStrings` to session classes |
| [MemberMaterializationPlan.cs](file:///c:/CodeSource/DataFlow/src/DataFlow.Framework.ObjectMaterializer/MemberMaterializationPlan.cs) | Add `GetSchemaStringAction`, wrap `ConvertObject` to throw `MaterializationFieldException` |

**Implementation:**

```csharp
// In GeneralMaterializationSession<T>
public T CreateFromStrings(string?[] values)
{
    // Wrap conversion errors with column context
    // Throw MaterializationFieldException on failure
}
```

---

## Phase 2: CSV Reader Integration

### 2.1 Update Error Handling to Use Structured Exception

**Current behavior** (lines 440-446 in `Read.Csv.cs`):
```csharp
catch (Exception ex)
{
    if (!options.HandleError("CSV", ..., ex.GetType().Name, ex.Message, ...))
        return false;
}
```

**New behavior** with `MaterializationFieldException`:
```csharp
catch (MaterializationFieldException mfEx)
{
    // Structured error with full context
    if (!options.HandleError(
        "CSV",
        options.Metrics.LinesRead,
        options.Metrics.RawRecordsParsed,
        filePath,
        "FieldConversionError",
        $"Column '{mfEx.ColumnName}' (index {mfEx.ColumnIndex}): " +
        $"Cannot convert '{mfEx.RawValue}' to {mfEx.TargetType.Name}",
        string.Join(",", rawFields.Take(8))))
        return false;
}
catch (Exception ex) // Other exceptions
{
    // Existing behavior for non-field errors
}
```

### 2.2 Decision Logic

| Condition | Use Fast Path (`CreateFromStrings`)? | Reason |
|-----------|-------------------------------------|--------|
| `T` is concrete class/record | ✅ Yes | Target types known from `T` |
| `T` is `dynamic` or `object` | ❌ No | No target types to derive |
| `FieldValueConverter` is set | ❌ No | User wants custom conversion |
| `InferSchema = true` | ❌ No | User wants type discovery |

---

## Expected Benefits

| Metric | Before | After |
|--------|--------|-------|
| Conversions per row | 2 (parse → cast) | 1 (parse) |
| Boxing allocations | N values/row | 0 |
| Error context | Message parsing required | Structured properties |
| Precision loss risk | Yes (double→decimal) | No (direct string→decimal) |
