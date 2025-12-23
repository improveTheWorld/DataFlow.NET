# ObjectMaterializer Framework: API Reference

**Version:** 5.0  
**Last Updated:** 2025-10-11  
**Status:** Production Ready

---

## Table of Contents

1. [Overview](#1-overview)
2. [Quick Start](#2-quick-start)
3. [Core Concepts](#3-core-concepts)
4. [Configuration Guide](#4-configuration-guide)
5. [Common Use Cases](#5-common-use-cases)
6. [Advanced Scenarios](#6-advanced-scenarios)
7. [Error Handling](#7-error-handling)
8. [Diagnostics & Logging](#8-diagnostics--logging)
9. [Performance & Optimization](#9-performance--optimization)
10. [API Reference](#10-api-reference)
11. [Best Practices](#11-best-practices)

---

## 1. Overview

The ObjectMaterializer framework provides high-performance, configurable object creation from raw data (arrays, CSV rows, Excel cells, API payloads). Version 5.0 introduces a redesigned API with three independent configuration axes for maximum clarity and flexibility.

### Key Features

- **Three-axis configuration model** (Schema, Quality, Logging)
- **Zero-allocation expression trees** for property/field assignment
- **Explicit parallel and streaming APIs** with ordering guarantees
- **Complete error handling parity** with v4.x (advanced hooks available)
- **Privacy-aware diagnostics** (PII/GDPR compliant)
- **Culture-aware parsing** for numbers, dates, and enums
- **Comprehensive preset library** for common scenarios

### When to Use

✅ **CSV/Excel imports** with messy data  
✅ **API deserialization** with strict validation  
✅ **ETL pipelines** requiring data quality auditing  
✅ **Batch processing** of millions of rows  
✅ **Streaming large files** without loading into memory  

❌ **JSON deserialization** (use System.Text.Json instead)  
❌ **ORM mapping** (use Entity Framework/Dapper)  
❌ **Simple POCO creation** (use constructors directly)  

---

## 2. Quick Start

### Basic Usage (CSV Import)

```csharp
using DataFlow.Framework;

// Define your model
public class Person
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int Age { get; set; }
    public DateTime BirthDate { get; set; }
}

// CSV data
var headers = new[] { "Id", "Name", "Age", "BirthDate" };
var rows = new[]
{
    new object[] { "1", "Alice", "30", "1994-05-15" },
    new object[] { "2", "Bob", "25", "1999-08-22" }
};

// Materialize objects (uses sensible defaults)
var people = ObjectMaterializer.Create<Person>(headers, rows);

// Force evaluation if needed
var peopleList = people.ToList();
```

### With Configuration

```csharp
var people = ObjectMaterializer.Create<Person>(
    headers,
    rows,
    schema: SchemaConfig<Person>.Default,
    quality: QualityConfig.Lenient.Trim(),
    logging: LogConfig.Errors.ToConsole()
);
```

### Using Presets

```csharp
// Use preset for common scenarios
var people = Presets.CsvImport<Person>(logger)
    .Materialize(headers, rows);

// Override specific settings
var people = Presets.CsvImport<Person>(logger)
    .Schema(s => s.WithCulture("fr-FR"))
    .Materialize(headers, rows);
```

---

## 3. Core Concepts

### 3.1 Three-Axis Configuration Model

```
ObjectMaterializer<T>
    ├─ Schema (HOW to interpret data)
    │   ├─ Culture (parsing locale)
    │   ├─ DateTimeFormats (explicit formats)
    │   ├─ CaseInsensitiveMapping (header matching)
    │   └─ Type interpretation rules
    │
    ├─ Quality (WHAT to accept/reject)
    │   ├─ Level (Strict/Standard/Lenient)
    │   ├─ Field rules (Critical/Required/Optional)
    │   ├─ Data cleaning (trim, empty strings)
    │   └─ Error handlers (advanced)
    │
    └─ Logging (WHAT to observe)
        ├─ Level (None/Errors/Warnings/Info/All/Debug)
        ├─ Sinks (Console/Logger/Collection/Custom)
        ├─ Privacy controls (PII filtering)
        └─ Filters (field-specific logging)
```

### 3.2 Materialization Flow

```
Raw Data (object[])
    ↓
Schema Mapping (headers → properties)
    ↓
Type Conversion (string → int, DateTime, etc.)
    ↓
Quality Checks (validation, error handling)
    ↓
Diagnostic Reporting (sinks)
    ↓
Materialized Object (T)
```

### 3.3 Processing Modes

| Mode | API | Return Type | Evaluation | Use Case |
|------|-----|-------------|------------|----------|
| **Sequential** | `Create<T>()` | `IEnumerable<T>` | Lazy | Default, memory-efficient |
| **Parallel** | `CreateParallel<T>()` | `IReadOnlyList<T>` | Eager | Large datasets, CPU-bound |
| **Streaming** | `CreateStream<T>()` | `IAsyncEnumerable<T>` | Lazy | Huge files, async I/O |

---

## 4. Configuration Guide

### 4.1 Schema Configuration

Controls **how data is interpreted** (culture, formats, mapping rules).

```csharp
public class SchemaConfig<T>
{
    // Mapping
    public bool CaseInsensitiveMapping { get; init; } = true;
    
    // Parsing Culture
    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;
    public string[]? DateTimeFormats { get; init; } = null;
    public bool AllowThousandsSeparators { get; init; } = false;
    
    // Type Interpretation
    public bool CaseInsensitiveEnums { get; init; } = true;
    
    // Fluent Builders
    public SchemaConfig<T> WithCulture(string cultureName);
    public SchemaConfig<T> WithCulture(CultureInfo culture);
    public SchemaConfig<T> WithDateFormats(params string[] formats);
    public SchemaConfig<T> AllowThousands();
    public SchemaConfig<T> StrictEnums();
    public SchemaConfig<T> CaseSensitiveMapping();
}
```

**Default values:**
```csharp
SchemaConfig<T>.Default {
    CaseInsensitiveMapping = true,    // "Name" matches "name"
    Culture = InvariantCulture,       // Predictable parsing
    DateTimeFormats = null,           // Use culture defaults
    AllowThousandsSeparators = false, // Safe default
    CaseInsensitiveEnums = true       // Lenient enum parsing
}
```

**Examples:**

```csharp
// French culture with specific date formats
var schema = SchemaConfig<Person>.Default
    .WithCulture("fr-FR")
    .WithDateFormats("dd/MM/yyyy", "yyyy-MM-dd")
    .AllowThousands();

// Strict schema (case-sensitive, no thousands separators)
var schema = SchemaConfig<Person>.Default
    .CaseSensitiveMapping()
    .StrictEnums();
```


---

### 4.2 Quality Configuration

Controls **what data is accepted/rejected** (validation, error handling, data cleaning).

```csharp
public class QualityConfig
{
    // Data Cleaning
    public bool TrimWhitespace { get; init; } = false;
    public EmptyStringPolicy EmptyStrings { get; init; } = EmptyStringPolicy.AsIs;
    
    // Error Strategy
    public QualityLevel Level { get; init; } = QualityLevel.Standard;
    public Dictionary<string, FieldRule> FieldRules { get; init; } = new();
    
    // Advanced Hooks (optional)
    public Func<ErrorContext, ErrorResolution>? Handler { get; init; }
    public Func<FallbackContext, object?>? Fallback { get; init; }
    
    // Fluent Builders
    public QualityConfig Trim();
    public QualityConfig TreatEmptyAs(EmptyStringPolicy policy);
    public QualityConfig Critical(params string[] fields);
    public QualityConfig Required(params string[] fields);
    public QualityConfig Optional(params string[] fields);
    public QualityConfig WithHandler(Func<ErrorContext, ErrorResolution> handler);
    public QualityConfig WithFallback(Func<FallbackContext, object?> fallback);
}
```

**Quality Levels:**

```csharp
public enum QualityLevel
{
    Strict,    // Throw on any error (validation mode)
    Standard,  // Use defaults for optional, throw for required (balanced)
    Lenient    // Use defaults for everything, never throw (import mode)
}
```

**Empty String Policies:**

```csharp
public enum EmptyStringPolicy
{
    AsIs,           // Keep as empty string ""
    ConvertToNull,  // Treat "" as null
    Error           // Treat "" as conversion error
}
```


**Field Quality Rules:**

```csharp
public enum FieldQuality
{
    Standard,   // Follow QualityLevel default
    Critical,   // Skip row if invalid
    Required,   // Throw if invalid
    Optional    // Use default if invalid
}

public class FieldRule
{
    public FieldQuality Quality { get; init; } = FieldQuality.Standard;
    public object? FallbackValue { get; init; }
}

```

**Presets:**

```csharp
// Strict: Throw on any error
QualityConfig.Strict {
    Level = Strict,
    EmptyStrings = Error,
    TrimWhitespace = false
}

// Standard: Balanced (default)
QualityConfig.Standard {
    Level = Standard,
    EmptyStrings = AsIs,
    TrimWhitespace = false
}

// Lenient: Accept everything
QualityConfig.Lenient {
    Level = Lenient,
    EmptyStrings = ConvertToNull,
    TrimWhitespace = true
}
```

**Examples:**

```csharp
// Simple: Use preset
var quality = QualityConfig.Lenient.Trim();

// Field-specific rules
var quality = QualityConfig.Standard
    .Critical("OrderId", "CustomerId")  // Skip row if invalid
    .Required("OrderDate", "Amount")    // Throw if invalid
    .Optional("ShipDate", "Notes");     // Use default if invalid

// Custom fallback values
var quality = QualityConfig.Lenient
    .Trim()
    .WithFallback(ctx => ctx.MemberName switch
    {
        "Country" => "Unknown",
        "Status" => OrderStatus.Pending,
        _ => null
    });
```
---
#### 4.2.3 Empty String Policy Interactions

**With Required Fields:**

| EmptyStrings | Required Field | Level=Strict | Level=Standard | Level=Lenient |
|--------------|----------------|--------------|----------------|---------------|
| **AsIs** | `string Email` | Valid ("") | Valid ("") | Valid ("") |
| **ConvertToNull** | `string Email` | Throw | Throw | UseDefault (null) |
| **ConvertToNull** | `string? Email` | Valid (null) | Valid (null) | Valid (null) |
| **Error** | `string Email` | Throw | Throw | UseDefault ("") |

**Key insight:** `EmptyStrings=ConvertToNull` + `Required` field + non-nullable type → **always triggers validation**.

---

### 4.3 Logging Configuration

Controls **what is observed** (diagnostics, sinks, verbosity, privacy).

```csharp
public class LogConfig
{
    // Verbosity
    public LogLevel Level { get; init; } = LogLevel.Errors;
    
    // Output
    public IMaterializationSink? Sink { get; init; }
    
    // Filtering
    public Func<DiagnosticEvent, bool>? Filter { get; init; }
    
    // Privacy Controls
    public bool IncludeAttemptedValue { get; init; } = false;
    public bool IncludeTiming { get; init; } = false;
    public bool IncludeStrategy { get; init; } = true;
    
    // Fluent Builders
    public LogConfig WithLevel(LogLevel level);
    public LogConfig Where(Func<DiagnosticEvent, bool> filter);
    public LogConfig To(IMaterializationSink sink);
    public LogConfig ToConsole();
    public LogConfig ToLogger(ILogger logger);
    public LogConfig ToCollection(out CollectionSink sink);
    public LogConfig WithoutAttemptedValues();
    public LogConfig WithTiming();
    public LogConfig WithoutStrategy();
    
}
```

**Log Levels:**

```csharp
public enum LogLevel
{
    None,       // No logging (0% overhead)
    Errors,     // Only conversion failures (~2-5% overhead)
    Warnings,   // Warnings + errors (~5-10% overhead)
    Info,       // Info + warnings + errors (~10-15% overhead)
    All,        // Everything including success (~10-20% overhead)
    Debug       // Every attempt + timings (~30-50% overhead)
}
```

**Overhead Guidance:**

| Level | Overhead | Use Case |
|-------|----------|----------|
| **None** | 0% | Maximum performance, no observability |
| **Errors** | 2-5% | Production monitoring |
| **Warnings** | 5-10% | Data quality auditing |
| **Info** | 10-15% | Development, troubleshooting |
| **All** | 10-20% | Validation, testing |
| **Debug** | 30-50% | Deep performance analysis |

**Presets:**

```csharp
LogConfig.None        // No logging
LogConfig.Errors      // Errors only (production)
LogConfig.Warnings    // Warnings + errors (auditing)
LogConfig.Info        // Info + warnings + errors
LogConfig.All         // Everything including success
LogConfig.Debug       // Debug level with timings
```

**Examples:**

```csharp
// Simple: Console output
var logging = LogConfig.Errors.ToConsole();

// Production: Logger with privacy
var logging = LogConfig.Errors
    .ToLogger(logger)
    .WithoutAttemptedValues();  // PII/GDPR safe

// Auditing: Collect diagnostics
var logging = LogConfig.Warnings.ToCollection(out var sink);

// Multiple outputs
var logging = LogConfig.Errors
    .ToConsole()
    .ToLogger(logger);

// Field-specific logging
var logging = LogConfig.Debug
    .ToConsole()
    .Where(d => d.MemberName is "Price" or "Date");

// Performance analysis
var logging = LogConfig.Debug
    .ToConsole()
    .WithTiming()
    .Where(d => d.ConversionTimeMicroseconds > 1000);  // >1ms
```

---

### 4.4 Preset Library

Pre-configured combinations for common scenarios.

```csharp
public static class Presets	
{
    // CSV Import (lenient, trim whitespace)
    public static MaterializerBuilder<T> CsvImport<T>(ILogger logger);
    
    // Excel Import (strict dates, specific formats)
    public static MaterializerBuilder<T> ExcelImport<T>(ILogger logger);
    
    // Production (minimal overhead, log errors)
    public static MaterializerBuilder<T> Production<T>(ILogger logger);
    
    // Development (verbose, fail-fast)
    public static MaterializerBuilder<T> Development<T>();
    
    // Validation (strict, reject invalid)
    public static MaterializerBuilder<T> Validation<T>();
    
    // Auditing (collect warnings/errors)
    public static MaterializerBuilder<T> Auditing<T>(out CollectionSink sink);
    
    // API Deserialization (strict, fail-fast)
    public static MaterializerBuilder<T> ApiDeserialization<T>();
    
    // Production Safe (privacy-aware, minimal overhead)
    public static MaterializerBuilder<T> ProductionSafe<T>(ILogger logger);
}
```

**Preset Configurations:**

| Preset | Schema | Quality | Logging |
|--------|--------|---------|---------|
| **CsvImport** | Default | Lenient + Trim | Errors → Logger |
| **ExcelImport** | DateFormats("yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy") | Lenient + Trim | Warnings → Logger |
| **Production** | Default | Standard | Errors → Logger |
| **Development** | Default | Strict | All → Console |
| **Validation** | Default | Strict | Errors → Console |
| **Auditing** | Default | Standard | Warnings → Collection |
| **ApiDeserialization** | Default | Strict | Errors → Console |
| **ProductionSafe** | Default | Standard | Errors → Logger (no PII) |

**Usage:**

```csharp
// Use preset as-is
var people = Presets.CsvImport<Person>(logger)
    .Materialize(headers, rows);

// Override specific settings
var people = Presets.CsvImport<Person>(logger)
    .Schema(s => s.WithCulture("fr-FR"))
    .Quality(q => q.Critical("Email"))
    .Materialize(headers, rows);

// Production with privacy controls
var people = Presets.ProductionSafe<Person>(logger)
    .Materialize(headers, rows);
```

---

## 5. Common Use Cases

### 5.1 CSV Import (Lenient)

**Scenario:** User-uploaded CSV with inconsistent formatting.

```csharp
// Simple (using preset)
var people = Presets.CsvImport<Person>(logger)
    .Materialize(headers, rows);

// Explicit configuration
var people = ObjectMaterializer.Create<Person>(
    headers,
    rows,
    schema: SchemaConfig<Person>.Default,
    quality: QualityConfig.Lenient.Trim(),
    logging: LogConfig.Errors.ToLogger(logger)
);
```
**Behavior with QualityConfig.Lenient:**
- `"  Alice  "` → `"Alice"` (trimmed because TrimWhitespace=true)
- `""` (empty age) → `null` (EmptyStrings=ConvertToNull) 
                   → conversion error (null → int) 
                   → `0` (default(int), Level=Lenient)
- `"invalid-date"` → conversion error → `DateTime.MinValue` (default) + logged error

---

### 5.2 Excel Import (Strict Dates)

**Scenario:** Financial data with specific date formats.

```csharp
// Using preset
var transactions = Presets.ExcelImport<Transaction>(logger)
    .Materialize(headers, rows);

// Explicit configuration
var transactions = ObjectMaterializer.Create<Transaction>(
    headers,
    rows,
    schema: SchemaConfig<Transaction>.Default
        .WithDateFormats("yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy"),
    quality: QualityConfig.Lenient.Trim(),
    logging: LogConfig.Warnings.ToLogger(logger)
);
```

**Behavior:**
- `"2024-12-31"` → parsed using first matching format
- `"12/31/2024"` → parsed using second format
- `"31-12-2024"` → error (not in formats) → default + logged

---

### 5.3 API Validation (Fail-Fast)

**Scenario:** REST API request body validation.

```csharp
// Using preset
try
{
    var request = Presets.ApiDeserialization<CreateUserRequest>()
        .Materialize(headers, values)
        .Single();
    // Process valid request
}
catch (MaterializationException ex)
{
    return BadRequest(new { 
        error = ex.Message, 
        field = ex.Context.MemberName,
        value = ex.Context.AttemptedValue
    });
}

// Explicit configuration
var request = ObjectMaterializer.Create<CreateUserRequest>(
    headers,
    values,
    schema: SchemaConfig<CreateUserRequest>.Default,
    quality: QualityConfig.Strict,
    logging: LogConfig.Errors.ToConsole()
).Single();
```

**Behavior:**
- `null` or `""` for required field → throws immediately
- `"abc"` for int field → throws with detailed context
- Valid data → creates object, continues

---

### 5.4 Critical Fields (Skip Invalid Rows)

**Scenario:** Import orders, skip rows with invalid IDs.

```csharp
// Simple (using field rules)
var orders = ObjectMaterializer.Create<Order>(
    headers,
    rows,
    schema: SchemaConfig<Order>.Default,
    quality: QualityConfig.Standard
        .Critical("OrderId", "CustomerId")  // Skip row if invalid
        .Required("OrderDate")              // Throw if invalid
        .Optional("ShipDate"),              // Use default if invalid
    logging: LogConfig.Warnings.ToConsole()
);
// Invalid OrderId → row skipped (not returned)
// Invalid OrderDate → exception thrown
// Invalid ShipDate → DateTime.MinValue used
```

**Note:** Skipped rows are **automatically filtered** from results. Diagnostics still reference original row indices.

---

### 5.5 Custom Fallback Values

**Scenario:** Use placeholder values instead of defaults.

```csharp
var people = ObjectMaterializer.Create<Person>(
    headers,
    rows,
    schema: SchemaConfig<Person>.Default,
    quality: QualityConfig.Lenient
        .Trim()
        .WithFallback(ctx => ctx.MemberName switch
        {
            "Age" => 0,
            "Country" => "Unknown",
            "Status" => OrderStatus.Pending,
            _ => null
        }),
    logging: LogConfig.Errors.ToConsole()
);
// Invalid age → 0
// Invalid country → "Unknown"
// Invalid status → OrderStatus.Pending
```

---

### 5.6 Data Quality Auditing

**Scenario:** Import data and collect all warnings/errors for review.

```csharp
// Using preset
var people = Presets.Auditing<Person>(out var sink)
    .Materialize(headers, rows)
    .ToList();

// Review diagnostics
Console.WriteLine($"Imported {people.Count} rows");
Console.WriteLine($"Errors: {sink.Errors.Count()}");
Console.WriteLine($"Warnings: {sink.Warnings.Count()}");

foreach (var error in sink.Errors)
{
    Console.WriteLine($"Row {error.RowIndex}, {error.MemberName}: {error.Message}");
}

// Export to CSV
File.WriteAllLines("import-errors.csv",
    sink.Diagnostics.Select(d => 
        $"{d.RowIndex},{d.MemberName},{d.Severity},{d.Message}"));
```

---


### 5.7 Async Streaming (Memory Efficient)

**Scenario:** Process 100GB CSV file without loading into memory.

```csharp
// Using explicit stream API
await foreach (var person in ObjectMaterializer.CreateStream<Person>(
    headers: headers,
    rows: ReadCsvRowsAsync(filePath),
    schema: SchemaConfig<Person>.Default,
    quality: QualityConfig.Lenient.Trim(),
    logging: LogConfig.Errors.ToLogger(logger)))
{
    await ProcessPersonAsync(person);
    // Only one row in memory at a time
}

// Using builder
await foreach (var person in Presets.CsvImport<Person>(logger)
    .MaterializeAsync(headers, ReadCsvRowsAsync(filePath)))
{
    await ProcessPersonAsync(person);
}

async IAsyncEnumerable<object[]> ReadCsvRowsAsync(string path)
{
    await using var reader = new StreamReader(path);
    string? line;
    while ((line = await reader.ReadLineAsync()) != null)
    {
        yield return line.Split(',');
    }
}
```

**Guarantees:**
- ✅ One-row-at-a-time processing
- ✅ Ordered diagnostics by row arrival
- ✅ Memory-efficient (no buffering)

---
#### 5.7.1 CreateParallel Ordering Guarantees

**Results order:**
- ✅ Results preserve **input order** (results[i] corresponds to rows[i], excluding skipped rows)
- ✅ Parallel processing is **internally chunked**, but results are **merged in order** before returning
- ✅ Skipped rows are filtered, but relative order of valid rows is preserved

**Diagnostics order:**
- ✅ Diagnostics are **sorted by RowIndex** before returning
- ✅ RowIndex always references **original input position** (before filtering)

**Example:**
```csharp
var rows = new[]
{
    new object[] { "1", "Alice" },   // Row 0
    new object[] { "BAD", "Bob" },   // Row 1 (invalid, skipped)
    new object[] { "3", "Charlie" }  // Row 2
};

var results = ObjectMaterializer.CreateParallel<Person>(headers, rows, ...);
// results[0] = Person { Id = 1, Name = "Alice" }   (from row 0)
// results[1] = Person { Id = 3, Name = "Charlie" } (from row 2)
// Row 1 is filtered (not in results)

// Diagnostics:
// [0] RowIndex=1, MemberName="Id", Severity=Error (row 1 error)
```

**Implementation note:** Internally uses `Parallel.ForEach` with `ConcurrentBag`, then sorts by original index before returning.


---
### 5.8 Parallel Processing (Large Datasets)

**Scenario:** Import 10 million rows using all CPU cores.

```csharp
// Using explicit parallel API
var people = ObjectMaterializer.CreateParallel<Person>(
    headers: headers,
    rows: rows.ToList(),  // Must be IReadOnlyList
    schema: SchemaConfig<Person>.Default,
    quality: QualityConfig.Lenient.Trim(),
    logging: LogConfig.Errors.ToLogger(logger),
    degreeOfParallelism: Environment.ProcessorCount
);
// Returns IReadOnlyList<Person> (eager evaluation)
// ~3-4x faster than sequential for CPU-bound conversions

// Using builder
var people = Presets.Production<Person>(logger)
    .MaterializeParallel(headers, rows.ToList(), 
        degreeOfParallelism: Environment.ProcessorCount);
```

**Requirements:**
- ✅ Input must be `IReadOnlyList<object[]>` (not `IEnumerable`)
- ✅ Sink must be thread-safe (validated at runtime)
- ✅ Returns `IReadOnlyList<T>` (eager evaluation)

**Guarantees:**
- ✅ Results preserve input order (results[i] corresponds to rows[i], excluding skipped)
- ✅ Diagnostics sorted by RowIndex
- ✅ Thread-safety validated upfront

---

### 5.9 Multiple Sinks (Console + Logger)

**Scenario:** Output diagnostics to multiple destinations.

var logging = LogConfig.Errors
    .ToConsole()
    .ToLogger(logger);
// ✅ Both sinks receive the same diagnostics
// ✅ Each To*() returns a new LogConfig instance (immutable)
// ✅ Internally wrapped in CompositeSink

// You can also add a collection sink
var logging = LogConfig.Errors
    .ToConsole()
    .ToLogger(logger)
    .ToCollection(out var sink);
// ✅ All three sinks receive diagnostics

**Warning:**
// ⚠️ For CreateParallel, ALL sinks must be thread-safe
var logging = LogConfig.Errors
    .ToConsole()              // ✅ Thread-safe
    .ToLogger(logger)         // ✅ Thread-safe
    .To(customSink);          // ⚠️ Must be [ThreadSafe]

---

### 5.10 Field-Specific Debug Logging

**Scenario:** Debug only specific fields with high overhead.

```csharp
var people = ObjectMaterializer.Create<Person>(
    headers,
    rows,
    schema: SchemaConfig<Person>.Default,
    quality: QualityConfig.Standard,
    logging: LogConfig.Debug
        .ToConsole()
        .WithTiming()
        .Where(d => d.MemberName is "Price" or "Date")
);
// Only logs conversions for Price and Date fields
// Includes timing information for performance analysis
```
---

#### 5.10.1 Multi-Sink Implementation

**Chaining behavior:**
```csharp
// Each To*() method returns a NEW LogConfig instance
var config1 = LogConfig.Errors;                    // Original
var config2 = config1.ToConsole();                 // New instance
var config3 = config2.ToLogger(logger);            // New instance
var config4 = config3.ToCollection(out var sink);  // New instance

// Internally, config4 wraps all sinks in a CompositeSink:
public class CompositeSink : IMaterializationSink
{
    private readonly List<IMaterializationSink> _sinks;
    
    public void Report(DiagnosticEvent diagnostic)
    {
        foreach (var sink in _sinks)
        {
            sink.Report(diagnostic);
        }
    }
}
```

**Immutability guarantee:**
```csharp
var baseConfig = LogConfig.Errors;
var consoleConfig = baseConfig.ToConsole();

// baseConfig is unchanged (no sink)
// consoleConfig has ConsoleSink
// Both are independent instances
```

**Thread-safety:**
- ✅ `CompositeSink` validates that **all wrapped sinks** are thread-safe
- ✅ If any sink lacks `[ThreadSafe]`, `CreateParallel` throws
---

## 6. Advanced Scenarios

### 6.1 Advanced Error Handling (Custom Logic)

**Scenario:** Custom parsing logic for special formats.

```csharp
var quality = QualityConfig.Standard
    .WithHandler(ctx =>
    {
        // Custom TimeSpan parsing: "1d 2h 30m"
        if (ctx.TargetType == typeof(TimeSpan) && 
            ctx.AttemptedValue is string s &&
            TryParseCustomTimeSpan(s, out var timeSpan))
        {
            ctx.AdditionalData["ParsedValue"] = timeSpan;
            return ErrorResolution.UseCustomValue;
        }
        
        // Critical fields → skip row
        if (ctx.MemberName is "OrderId" or "CustomerId")
            return ErrorResolution.SkipRow;
        
        // Required fields → throw
        if (ctx.MemberName == "Amount")
            return ErrorResolution.Throw;
        
        // Everything else → use default
        return ErrorResolution.UseDefault;
    })
    .WithFallback(ctx =>
    {
        // Return custom parsed value
        if (ctx.AdditionalData.TryGetValue("ParsedValue", out var value))
            return value;
        
        // Field-specific fallbacks
        return ctx.MemberName switch
        {
            "Country" => "Unknown",
            "Status" => OrderStatus.Pending,
            _ => null
        };
    });

var orders = ObjectMaterializer.Create<Order>(
    headers, rows,
    SchemaConfig<Order>.Default,
    quality,
    LogConfig.Warnings.ToConsole()
);

bool TryParseCustomTimeSpan(string input, out TimeSpan result)
{
    var match = Regex.Match(input, @"(?:(\d+)d)?\s*(?:(\d+)h)?\s*(?:(\d+)m)?");
    if (match.Success)
    {
        int days = int.TryParse(match.Groups[1].Value, out var d) ? d : 0;
        int hours = int.TryParse(match.Groups[2].Value, out var h) ? h : 0;
        int minutes = int.TryParse(match.Groups[3].Value, out var m) ? m : 0;
        result = new TimeSpan(days, hours, minutes, 0);
        return true;
    }
    result = default;
    return false;
}
```

---

### 6.2 Conditional Validation

**Scenario:** Validate fields based on other field values.

```csharp
var quality = QualityConfig.Standard
    .WithHandler(ctx =>
    {
        // If OrderType is "Express", ShipDate is required
        if (ctx.MemberName == "ShipDate")
        {
            if (ctx.AdditionalData.TryGetValue("OrderType", out var orderType) &&
                orderType?.ToString() == "Express")
            {
                return ErrorResolution.Throw;  // Required for Express
            }
            return ErrorResolution.UseDefault;  // Optional otherwise
        }
        
        // Store OrderType for later validation
        if (ctx.MemberName == "OrderType" && ctx.AttemptedValue != null)
        {
            ctx.AdditionalData["OrderType"] = ctx.AttemptedValue;
        }
        
        return ErrorResolution.UseDefault;
    });
```

**Note:** For complex cross-field validation, consider validating after materialization using FluentValidation or similar libraries.

---

### 6.3 Dynamic Schema

**Scenario:** Schema determined at runtime (user-uploaded CSV).

```csharp
public async Task<List<Person>> ImportCsvAsync(Stream csvStream)
{
    using var reader = new StreamReader(csvStream);
    
    // Read header row
    var headerLine = await reader.ReadLineAsync();
    if (headerLine == null) throw new InvalidDataException("Empty file");
    var headers = headerLine.Split(',');
    
    // Read data rows
    var rows = new List<object[]>();
    string? line;
    while ((line = await reader.ReadLineAsync()) != null)
    {
        rows.Add(line.Split(','));
    }
    
    // Materialize with dynamic schema
    return Presets.CsvImport<Person>(logger)
        .Materialize(headers, rows)
        .ToList();
}
```

---

### 6.4 Performance Profiling

**Scenario:** Identify slow conversions.

```csharp
var sink = new CollectionSink();
var people = ObjectMaterializer.Create<Person>(
    headers,
    rows.Take(1000),  // Sample first 1000 rows
    schema: SchemaConfig<Person>.Default,
    quality: QualityConfig.Standard,
    logging: LogConfig.Debug
        .ToCollection(out sink)
        .WithTiming()
        .WithoutAttemptedValues()  // Privacy
);

// Analyze slow conversions
var slowConversions = sink.Diagnostics
    .Where(d => d.ConversionTimeMicroseconds > 1000)  // >1ms
    .OrderByDescending(d => d.ConversionTimeMicroseconds)
    .GroupBy(d => d.MemberName)
    .Select(g => new
    {
        Property = g.Key,
        Count = g.Count(),
        AvgTimeMicroseconds = g.Average(d => d.ConversionTimeMicroseconds),
        MaxTimeMicroseconds = g.Max(d => d.ConversionTimeMicroseconds)
    });

foreach (var stat in slowConversions)
{
    Console.WriteLine($"{stat.Property}: {stat.Count} conversions, " +
                     $"avg={stat.AvgTimeMicroseconds:F2}μs, " +
                     $"max={stat.MaxTimeMicroseconds}μs");
}
```

---

### 6.5 Streaming Large Files

**Scenario:** Process 10GB CSV without loading into memory.

```csharp
public async Task ProcessLargeFileAsync(string filePath)
{
    var headers = await ReadHeadersAsync(filePath);
    var rowStream = ReadRowsAsync(filePath);
    
    var sink = new CollectionSink();
    var config = Presets.CsvImport<Order>(logger)
        .Logging(LogConfig.Errors.ToCollection(out sink));
    
    await foreach (var order in config.MaterializeAsync(headers, rowStream))
    {
        // Process one order at a time
        await ProcessOrderAsync(order);
        
        // Check for errors every 1000 rows
        if (order.Id % 1000 == 0 && sink.Errors.Any())
        {
            logger.LogWarning($"Errors at row {order.Id}: {sink.Errors.Count()}");
            sink.Clear();
        }
    }
}

async IAsyncEnumerable<object[]> ReadRowsAsync(string filePath)
{
    using var reader = new StreamReader(filePath);
    await reader.ReadLineAsync();  // Skip header
    
    string? line;
    while ((line = await reader.ReadLineAsync()) != null)
    {
        yield return line.Split(',');
        await Task.Yield();  // Allow cancellation
    }
}
```

---


### 6.7 Multi-Culture Processing

**Scenario:** Import data from multiple regions.

```csharp
public class RegionalImporter
{
    private readonly Dictionary<string, CultureInfo> _cultures = new()
    {
        ["US"] = new CultureInfo("en-US"),
        ["EU"] = new CultureInfo("de-DE"),
        ["JP"] = new CultureInfo("ja-JP")
    };
    
    public List<Order> ImportByRegion(
        string region,
        string[] headers,
        IEnumerable<object[]> rows)
    {
        var culture = _cultures.GetValueOrDefault(region, CultureInfo.InvariantCulture);
        
        var schema = new SchemaConfig<Order>
        {
            Culture = culture,
            DateTimeFormats = region switch
            {
                "US" => new[] { "MM/dd/yyyy", "M/d/yyyy" },
                "EU" => new[] { "dd.MM.yyyy", "d.M.yyyy" },
                "JP" => new[] { "yyyy/MM/dd" },
                _ => null
            },
            AllowThousandsSeparators = true
        };
        
        return ObjectMaterializer.Create<Order>(
            headers, rows,
            schema,
            QualityConfig.Standard,
            LogConfig.Warnings.ToLogger(logger)
        ).ToList();
    }
}
```

---

### 6.8 Custom Sink Implementation

**Scenario:** Send diagnostics to Application Insights.

```csharp
[ThreadSafe]
public class ApplicationInsightsSink : IMaterializationSink
{
    private readonly TelemetryClient _telemetry;
    
    public ApplicationInsightsSink(TelemetryClient telemetry)
    {
        _telemetry = telemetry;
    }
    
    public void Report(DiagnosticEvent diagnostic)
    {
        var properties = new Dictionary<string, string>
        {
            ["RowIndex"] = diagnostic.RowIndex.ToString(),
            ["MemberName"] = diagnostic.MemberName,
            ["Severity"] = diagnostic.Severity.ToString(),
            ["Strategy"] = diagnostic.ConversionStrategy,
            ["TargetType"] = diagnostic.TargetType.Name
        };
        
        var metrics = new Dictionary<string, double>();
        if (diagnostic.ConversionTimeMicroseconds > 0)
        {
            metrics["ConversionTimeMicroseconds"] = diagnostic.ConversionTimeMicroseconds;
        }
        
        if (diagnostic.Severity == DiagnosticSeverity.Error)
        {
            _telemetry.TrackException(
                new MaterializationException(diagnostic.Message),
                properties,
                metrics);
        }
        else
        {
            _telemetry.TrackEvent(
                "MaterializationDiagnostic",
                properties,
                metrics);
        }
    }
}

// Usage
var sink = new ApplicationInsightsSink(telemetryClient);
var orders = ObjectMaterializer.CreateParallel<Order>(
    headers, rows,
    SchemaConfig<Order>.Default,
    QualityConfig.Standard,
    LogConfig.All.To(sink).WithTiming()
);
```

---

### 6.9 Field-Level Quality Rules

**Scenario:** Different validation rules per field.

```csharp
var quality = QualityConfig.Standard
    .Required("Email")       // Email: Always throw if invalid
    .Optional("PhoneNumber"); // PhoneNumber: Always use default if invalid





var orders = ObjectMaterializer.Create<Order>(
    headers, rows,
    SchemaConfig<Order>.Default,
    quality,
    LogConfig.Warnings.ToConsole()
);
```

**Behavior:**
// - Invalid Email → throws exception (Required)
// - Invalid PhoneNumber → uses default (Optional)
// - Other fields follow Standard level:
//   - Non-nullable types (int, DateTime, string): Throw (required by default)
//   - Nullable types (int?, DateTime?, string?): UseDefault (optional by default)

---

### 6.10 Deduplication

**Scenario:** Remove duplicate rows based on composite key.

**Note:** Cross-row logic (like deduplication) should be done **after materialization**, not in error handlers (which are per-row).

```csharp
// Materialize all rows
var allOrders = ObjectMaterializer.Create<Order>(
    headers, rows,
    SchemaConfig<Order>.Default,
    QualityConfig.Standard,
    LogConfig.Warnings.ToConsole()
).ToList();

// Deduplicate by composite key
var uniqueOrders = allOrders
    .DistinctBy(o => new { o.OrderId, o.CustomerId })
    .ToList();

// Or track duplicates for auditing
var seenKeys = new HashSet<string>();
var uniqueOrders = new List<Order>();
var duplicates = new List<Order>();

foreach (var order in allOrders)
{
    var key = $"{order.OrderId}:{order.CustomerId}";
    if (seenKeys.Add(key))
        uniqueOrders.Add(order);
    else
        duplicates.Add(order);
}

logger.LogWarning($"Found {duplicates.Count} duplicate orders");

```


---

## 7. Error Handling

### 7.1 Error Resolution Flow

The framework resolves errors using a **four-stage pipeline**:

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Field-Specific Rules                                      │
│    FieldRules[member].FallbackValue (if set)                 │
│    → Highest priority, explicit override                     │
└────────────────┬────────────────────────────────────────────┘
                 │ (if not set)
                 ▼
┌─────────────────────────────────────────────────────────────┐
│ 2. Custom Handler                                            │
│    Handler(ErrorContext) → ErrorResolution                   │
│    → Custom logic, can access row context                    │
└────────────────┬────────────────────────────────────────────┘
                 │ (if returns null or not set)
                 ▼
┌─────────────────────────────────────────────────────────────┐
│ 3. Quality Level Defaults                                    │
│    Critical → SkipRow                                        │
│    Required → Throw                                          │
│    Optional → UseDefault                                     │                                      │
└────────────────┬────────────────────────────────────────────┘
                 │ (if UseCustomValue)
                 ▼
┌─────────────────────────────────────────────────────────────┐
│ 4. Fallback Provider                                         │
│    Fallback(FallbackContext) → object?                       │
│    → Provides actual value for UseCustomValue                │
└─────────────────────────────────────────────────────────────┘
```

---
#### 7.1.1 Resolution Order Details

**Stage 1: Field-Specific Fallback Value**
```csharp
if (FieldRules.TryGetValue(memberName, out var rule) && 
    rule.FallbackValue != null)
{
    return rule.FallbackValue;  // Highest priority, explicit override
}
```

**Stage 2: Custom Handler**
```csharp
if (Handler != null)
{
    var resolution = Handler(errorContext);
    if (resolution == ErrorResolution.UseCustomValue)
    {
        // Proceed to Stage 4 (Fallback provider)
    }
    else
    {
        return resolution;  // Throw, UseDefault, SkipProperty, SkipRow
    }
}
```

**Stage 3: Quality Level Defaults**
```csharp
// If no Handler or Handler returns null
var resolution = DetermineResolution(fieldQuality, qualityLevel);
if (resolution == ErrorResolution.UseCustomValue)
{
    // Proceed to Stage 4
}
```

**Stage 4: Fallback Provider**
```csharp
if (Fallback != null)
{
    return Fallback(fallbackContext);
}
return default(T);  // Final fallback
```

**Critical note:** `FieldRule.FallbackValue` **bypasses all other stages** if set. This is by design for explicit overrides.


---
### 7.2 ErrorResolution Behaviors

| Resolution | Behavior | Use Case |
|------------|----------|----------|
| **Throw** | Throws `MaterializationException` | Critical data integrity |
| **UseDefault** | Assigns `default(T)` to property | Optional fields |
| **UseCustomValue** | Calls `Fallback()` for value | Smart defaults |
| **SkipProperty** | Leaves property unassigned | Partial updates |
| **SkipRow** | Filters row from results | Invalid records |

**Important:** `SkipRow` is **filtered internally** before yielding. API returns `IEnumerable<T>` (never null).
**Behavior:**
- For reference/nullable properties, SkipProperty leaves the existing value (default for new instances is null) and does not assign a default.
- For value types without a prior value, result equals default(T) anyway due to the object being newly constructed.

---
#### 7.2.1 SkipProperty Behavior

**For newly constructed objects:**
```csharp
public class Order
{
    public int Id { get; set; }           // Value type
    public string? Name { get; set; }     // Reference type
    public DateTime Date { get; set; }    // Value type
}

// If SkipProperty is used:
var order = new Order();  // Default constructor
// Id = 0 (default(int))
// Name = null (default(string?))
// Date = DateTime.MinValue (default(DateTime))

// SkipProperty leaves these defaults unchanged
```

**For existing objects (partial updates):**
```csharp
var existingOrder = GetOrderFromDatabase();
// Id = 123, Name = "Alice", Date = 2024-01-01

// If SkipProperty is used during update:
// Properties retain existing values
// Id = 123 (unchanged)
// Name = "Alice" (unchanged)
// Date = 2024-01-01 (unchanged)
```

**SkipProperty vs. UseDefault:**

| Scenario | SkipProperty | UseDefault |
|----------|--------------|------------|
| **New object, value type** | `default(T)` (0, false, etc.) | `default(T)` (same) |
| **New object, reference type** | `null` | `null` (same) |
| **Existing object, value type** | Keeps existing value | Overwrites with `default(T)` |
| **Existing object, reference type** | Keeps existing value | Overwrites with `null` |

**Use case:** `SkipProperty` is useful for **partial updates** where you want to preserve existing values for invalid fields.


---
### 7.3 Exception Types

```csharp
// Base exception
public class MaterializationException : Exception
{
    public int RowIndex { get; }
    public string MemberName { get; }
    public object? AttemptedValue { get; }
    public Type TargetType { get; }
}

// Specific exceptions
public class TypeConversionException : MaterializationException { }
public class MissingMemberException : MaterializationException { }
public class ValidationException : MaterializationException { }
```

---

### 7.4 Error Context Usage

**ErrorContext** provides rich information for custom handlers:

```csharp
var quality = QualityConfig.Standard
    .WithHandler(ctx =>
    {
        // Access error details
        logger.LogWarning(
            "Conversion failed at row {RowIndex}, field {MemberName}: " +
            "attempted to convert '{Value}' to {Type}",
            ctx.RowIndex,
            ctx.MemberName,
            ctx.AttemptedValue,
            ctx.TargetType.Name);
        
        // Access underlying exception
        if (ctx.Exception is FormatException)
        {
            // Handle format errors specially
            return ErrorResolution.UseDefault;
        }
        
        // Use AdditionalData for stateful logic
        if (!ctx.AdditionalData.ContainsKey("ErrorCount"))
        {
            ctx.AdditionalData["ErrorCount"] = 0;
        }
        
        var errorCount = (int)ctx.AdditionalData["ErrorCount"];
        ctx.AdditionalData["ErrorCount"] = errorCount + 1;
        
        // Skip row after 3 errors
        if (errorCount >= 3)
        {
            return ErrorResolution.SkipRow;
        }
        
        return ErrorResolution.UseDefault;
    });
```

---

### 7.5 Fallback Strategies

**Pattern 1: Static Fallbacks**

```csharp
var quality = QualityConfig.Standard
    .WithFallback(ctx => ctx.MemberName switch
    {
        "Country" => "USA",
        "Status" => OrderStatus.Pending,
        "Quantity" => 1,
        _ => null
    });
```

**Pattern 2: Type-Based Fallbacks**

```csharp
var quality = QualityConfig.Standard
    .WithFallback(ctx =>
    {
        if (ctx.TargetType == typeof(string))
            return "N/A";
        if (ctx.TargetType == typeof(int))
            return 0;
        if (ctx.TargetType == typeof(DateTime))
            return DateTime.UtcNow;
        if (ctx.TargetType.IsEnum)
            return Enum.GetValues(ctx.TargetType).GetValue(0);
        
        return null;
    });
```

**Pattern 3: Row-Aware Fallbacks**

```csharp
var quality = QualityConfig.Standard
    .WithFallback(ctx =>
    {
        // Use row index for deterministic defaults
        if (ctx.MemberName == "BatchId")
        {
            return $"BATCH_{ctx.RowIndex / 1000}";
        }
        
        // Use other field values from AdditionalData
        if (ctx.MemberName == "ShipDate" &&
            ctx.AdditionalData.TryGetValue("OrderDate", out var orderDate))
        {
            return ((DateTime)orderDate).AddDays(3);
        }
        
        return null;
    });
```

---

### 7.6 Error Aggregation

**Scenario:** Collect all errors before deciding to proceed.

```csharp
var sink = new CollectionSink();
var quality = QualityConfig.Standard
    .WithHandler(ctx =>
    {
        // Don't throw, collect errors
        return ErrorResolution.UseDefault;
    });

var orders = ObjectMaterializer.Create<Order>(
    headers, rows,
    SchemaConfig<Order>.Default,
    quality,
    LogConfig.Errors.ToCollection(out sink)
).ToList();

// Analyze errors
var errorRate = (double)sink.Errors.Count() / rows.Count();
if (errorRate > 0.1)  // >10% error rate
{
    throw new InvalidOperationException(
        $"Too many errors: {sink.Errors.Count()} / {rows.Count()} " +
        $"({errorRate:P})");
}

// Group errors by field
var errorsByField = sink.Errors
    .GroupBy(e => e.MemberName)
    .Select(g => new
    {
        Field = g.Key,
        Count = g.Count(),
        SampleError = g.First().Message
    });

foreach (var error in errorsByField)
{
    logger.LogWarning(
        "Field {Field}: {Count} errors. Sample: {Message}",
        error.Field,
        error.Count,
        error.SampleError);
}
```

---

## 8. Diagnostics & Logging

### 8.1 Log Levels

| Level | Captures | Performance Overhead | Use Case |
|-------|----------|---------------------|----------|
| **None** | Nothing | 0% | Production (no diagnostics) |
| **Errors** | Errors only | 2-5% | Production (error tracking) |
| **Warnings** | Warnings + Errors | 5-10% | Production (quality monitoring) |
| **Info** | Info + Warnings + Errors | 10-15% | Staging (detailed monitoring) |
| **All** | All events including success | 10-20% | Development (full visibility) |
| **Debug** | All + timing + strategies | 30-50% | Performance profiling |

---

### 8.2 DiagnosticEvent Structure

```csharp
public class DiagnosticEvent
{
    // Core identification
    public DateTimeOffset Timestamp { get; init; }
    public int RowIndex { get; init; }              // Original row index (before filtering)
    public string MemberName { get; init; }
    
    // Severity and outcome
    public DiagnosticSeverity Severity { get; init; }  // Error, Warning, Info, Debug
    public bool SuccessfulConversion { get; init; }
    public string Message { get; init; }
    
    // Conversion details
    public Type TargetType { get; init; }
    public string ConversionStrategy { get; init; }    // "DirectParse", "EnumParse", etc.
    
    // Optional (privacy-controlled)
    public object? AttemptedValue { get; init; }       // Only if IncludeAttemptedValue=true
    public long ConversionTimeMicroseconds { get; init; }  // Only if IncludeTiming=true
    
    // Extensibility
    public IDictionary<string, object?> Additional { get; init; }
}
```

---

### 8.3 Built-in Sinks

#### 8.3.1 CollectionSink

**Thread-safe**, stores diagnostics in memory.

```csharp
var sink = new CollectionSink();
var orders = ObjectMaterializer.CreateParallel<Order>(
    headers, rows,
    SchemaConfig<Order>.Default,
    QualityConfig.Standard,
    LogConfig.All.ToCollection(out sink)
);

// Query diagnostics
Console.WriteLine($"Total events: {sink.Diagnostics.Count}");
Console.WriteLine($"Errors: {sink.Errors.Count()}");
Console.WriteLine($"Warnings: {sink.Warnings.Count()}");

// Find most problematic fields
var problemFields = sink.Errors
    .GroupBy(e => e.MemberName)
    .OrderByDescending(g => g.Count())
    .Take(5);

foreach (var field in problemFields)
{
    Console.WriteLine($"{field.Key}: {field.Count()} errors");
}

// Clear for next batch
sink.Clear();
```

#### 8.3.2 ConsoleSink

**Thread-safe**, writes to console with color coding.

```csharp
var orders = ObjectMaterializer.Create<Order>(
    headers, rows,
    SchemaConfig<Order>.Default,
    QualityConfig.Standard,
    LogConfig.Warnings.ToConsole()
);

// Output format:
// [ERROR] Row 42, OrderDate: Failed to parse '2024-13-01' as DateTime
// [WARN]  Row 15, Discount: Value '150' exceeds expected range, using default
```

#### 8.3.3 MicrosoftLoggerSink

**Thread-safe**, integrates with `Microsoft.Extensions.Logging`.

```csharp
var logger = loggerFactory.CreateLogger<OrderProcessor>();
var orders = ObjectMaterializer.Create<Order>(
    headers, rows,
    SchemaConfig<Order>.Default,
    QualityConfig.Standard,
    LogConfig.Errors.ToLogger(logger)
);

// Logs use structured logging:
// logger.LogError(
//     "Materialization error at row {RowIndex}, field {MemberName}: {Message}",
//     42, "OrderDate", "Failed to parse...");
```

---

### 8.4 Privacy Controls

**Default:** Privacy-safe (no PII in logs).

```csharp
// Production-safe logging (no values)
var config = LogConfig.Errors
    .ToLogger(logger)
    .WithoutAttemptedValues()  // Default: false
    .WithoutTiming();          // Default: false

// Development logging (full details)
var config = LogConfig.Debug
    .ToConsole()
    .WithAttemptedValues()     // Show values
    .WithTiming();             // Show performance
```

**Privacy Checklist:**

- ✅ `IncludeAttemptedValue = false` (default) → No PII in logs
- ✅ `IncludeTiming = false` (default) → No performance overhead
- ✅ `IncludeStrategy = true` (default) → Useful for debugging
- ⚠️ Enable `IncludeAttemptedValue` only in non-production environments

---

### 8.5 Filtering Diagnostics

```csharp
var sink = new CollectionSink();
var config = LogConfig.All
    .ToCollection(out sink)
    .Where(diagnostic =>
    {
        // Ignore warnings for optional fields
        if (diagnostic.Severity == DiagnosticSeverity.Warning &&
            diagnostic.MemberName is "PhoneNumber" or "MiddleName")
        {
            return false;
        }
        
        // Only log slow conversions
        if (diagnostic.ConversionTimeMicroseconds > 0 &&
            diagnostic.ConversionTimeMicroseconds < 1000)
        {
            return false;
        }
        
        return true;
    });
```

---

### 8.6 Custom Sink Implementation

**Requirements:**
- Implement `IMaterializationSink`
- Mark with `[ThreadSafe]` if supporting `CreateParallel`
- Handle null `AttemptedValue` gracefully

```csharp
[ThreadSafe]
public class DatabaseSink : IMaterializationSink
{
    private readonly DbContext _context;
    private readonly ConcurrentQueue<DiagnosticEvent> _buffer = new();
    private readonly Timer _flushTimer;
    
    public DatabaseSink(DbContext context)
    {
        _context = context;
        _flushTimer = new Timer(_ => FlushAsync().Wait(), null, 
            TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
    }
    
    public void Report(DiagnosticEvent diagnostic)
    {
        _buffer.Enqueue(diagnostic);
        
        // Flush if buffer is large
        if (_buffer.Count >= 1000)
        {
            FlushAsync().Wait();
        }
    }
    
    private async Task FlushAsync()
    {
        var events = new List<DiagnosticEvent>();
        while (_buffer.TryDequeue(out var evt))
        {
            events.Add(evt);
        }
        
        if (events.Any())
        {
            await _context.DiagnosticLogs.AddRangeAsync(events);
            await _context.SaveChangesAsync();
        }
    }
    
    public void Dispose()
    {
        _flushTimer?.Dispose();
        FlushAsync().Wait();
    }
}
```

---

## 9. Performance & Optimization

### 9.1 Performance Characteristics

| Method | Throughput | Memory | Latency | Thread-Safe |
|--------|-----------|--------|---------|-------------|
| **Create** | 100K-500K rows/sec | O(1) | Low (lazy) | No |
| **CreateParallel** | 500K-2M rows/sec | O(n) | Medium (eager) | Yes |
| **CreateStream** | 50K-200K rows/sec | O(1) | Low (lazy) | No |

* Throughput measured with 10 properties (mix of string, int, DateTime, decimal, enum),
  InvariantCulture, LogLevel.None, on Intel Core i7-12700K.
  Actual performance varies based on type complexity and configuration.

**Factors affecting performance:**
- Type complexity (primitives vs. nested objects)
- Validation rules (Strict > Standard > Lenient)
- Logging level (None < Errors < Debug)
- Culture-specific parsing (InvariantCulture fastest)
- Property count (fewer properties = faster)

---

### 9.2 Benchmarks

**Test Setup:** 100,000 rows, 10 properties (mix of string, int, DateTime, decimal, enum)

```
BenchmarkDotNet=v0.13.12, OS=Windows 11
Intel Core i7-12700K, 1 CPU, 12 logical and 8 physical cores
.NET SDK=8.0.100

| Method           | Rows    | LogLevel | Mean      | Allocated |
|----------------- |-------- |--------- |----------:|----------:|
| Create           | 100000  | None     |  45.2 ms  |   12 MB   |
| Create           | 100000  | Errors   |  47.8 ms  |   14 MB   |
| Create           | 100000  | Debug    |  68.3 ms  |   28 MB   |
| CreateParallel   | 100000  | None     |  18.5 ms  |  156 MB   |
| CreateParallel   | 100000  | Errors   |  19.2 ms  |  164 MB   |
| CreateStream     | 100000  | None     |  52.1 ms  |   12 MB   |
```

---

### 9.3 Optimization Strategies

#### 9.3.1 Choose the Right Method

```csharp
// Small datasets (<10K rows) → Create (lazy)
var orders = ObjectMaterializer.Create<Order>(headers, rows, ...);

// Large datasets (>100K rows) → CreateParallel
var orders = ObjectMaterializer.CreateParallel<Order>(headers, rows, ...);

// Streaming (files, APIs) → CreateStream
await foreach (var order in ObjectMaterializer.CreateStream<Order>(headers, rows, ...))
{
    await ProcessAsync(order);
}
```

#### 9.3.2 Minimize Logging Overhead

```csharp
// Production: Errors only
LogConfig.Errors.ToLogger(logger)

// Development: Full diagnostics
LogConfig.Debug.ToConsole().WithTiming()

// High-throughput: No logging
LogConfig.None
```

#### 9.3.3 Use InvariantCulture

```csharp
// Fastest (no culture-specific parsing)
var schema = new SchemaConfig<Order>
{
    Culture = CultureInfo.InvariantCulture
};

// Slower (culture-specific number/date parsing)
var schema = new SchemaConfig<Order>
{
    Culture = new CultureInfo("de-DE")
};
```

#### 9.3.4 Optimize Quality Level

```csharp
// Fastest (minimal validation)
QualityConfig.Lenient

// Balanced
QualityConfig.Standard

// Slowest (maximum validation)
QualityConfig.Strict
```

#### 9.3.5 Batch Processing

```csharp
// Process in chunks for better cache locality
const int chunkSize = 10000;
var allOrders = new List<Order>();

foreach (var chunk in rows.Chunk(chunkSize))
{
    var orders = ObjectMaterializer.CreateParallel<Order>(
        headers, chunk, schema, quality, logging);
    allOrders.AddRange(orders);
}
```

---

### 9.4 Memory Management

#### 9.4.1 Streaming for Large Files

```csharp
// BAD: Loads entire file into memory
var rows = File.ReadAllLines("huge.csv")
    .Select(line => line.Split(','))
    .ToList();
var orders = ObjectMaterializer.Create<Order>(headers, rows, ...);

// GOOD: Streams one row at a time
async IAsyncEnumerable<object[]> StreamRows(string filePath)
{
    using var reader = new StreamReader(filePath);
    await reader.ReadLineAsync();  // Skip header
    
    string? line;
    while ((line = await reader.ReadLineAsync()) != null)
    {
        yield return line.Split(',');
    }
}

await foreach (var order in ObjectMaterializer.CreateStream<Order>(
    headers, StreamRows("huge.csv"), ...))
{
    // Process one order at a time
}
```

#### 9.4.2 Dispose Resources

```csharp
// CollectionSink holds references to diagnostics
var sink = new CollectionSink();
var orders = ObjectMaterializer.Create<Order>(
    headers, rows,
    schema, quality,
    LogConfig.All.ToCollection(out sink)
).ToList();

// Clear diagnostics after processing
sink.Clear();

// Custom sinks should implement IDisposable
using var customSink = new DatabaseSink(context);
```

---

### 9.5 Parallel Processing Best Practices

#### 9.5.1 Degree of Parallelism

```csharp
// Default: Use all cores
var orders = ObjectMaterializer.CreateParallel<Order>(
    headers, rows, schema, quality, logging);

// Custom: Limit parallelism
var orders = ObjectMaterializer.CreateParallel<Order>(
    headers, rows, schema, quality, logging,
    degreeOfParallelism: 4);

// CPU-bound: Use processor count
degreeOfParallelism: Environment.ProcessorCount

// I/O-bound: Use higher value
degreeOfParallelism: Environment.ProcessorCount * 2
```

#### 9.5.2 Thread-Safe Sinks

```csharp
// SAFE: Built-in thread-safe sinks
LogConfig.Errors.ToConsole()
LogConfig.Errors.ToLogger(logger)
LogConfig.Errors.ToCollection(out sink)

// UNSAFE: Custom sink without [ThreadSafe]
public class FileSink : IMaterializationSink
{
    private readonly StreamWriter _writer;
    
    public void Report(DiagnosticEvent diagnostic)
    {
        _writer.WriteLine(diagnostic.Message);  // NOT THREAD-SAFE!
    }
}

// Runtime validation will throw:
// InvalidOperationException: Sink 'FileSink' is not thread-safe
```

---

### 9.6 Profiling and Diagnostics

#### 9.6.1 Identify Bottlenecks

```csharp
var sink = new CollectionSink();
var stopwatch = Stopwatch.StartNew();

var orders = ObjectMaterializer.Create<Order>(
    headers, rows,
    schema, quality,

	LogConfig.Debug.ToCollection(out sink)
			.WithTiming()
	).ToList();

	stopwatch.Stop();

	// Analyze timing data
	var slowConversions = sink.Diagnostics
		.Where(d => d.ConversionTimeMicroseconds > 1000)
		.GroupBy(d => d.MemberName)
		.Select(g => new
		{
			Property = g.Key,
			AvgTime = g.Average(d => d.ConversionTimeMicroseconds),
			MaxTime = g.Max(d => d.ConversionTimeMicroseconds),
			Count = g.Count()
		})
		.OrderByDescending(x => x.AvgTime);

	foreach (var item in slowConversions)
	{
		Console.WriteLine($"{item.Property}: Avg={item.AvgTime}μs, Max={item.MaxTime}μs, Count={item.Count}");
	}

	Console.WriteLine($"Total time: {stopwatch.ElapsedMilliseconds}ms");
	Console.WriteLine($"Rows/sec: {rows.Count / stopwatch.Elapsed.TotalSeconds:N0}");
```

#### 9.6.2 Conversion Strategy Analysis

```csharp
// Identify which strategies are used most
var strategyStats = sink.Diagnostics
    .GroupBy(d => d.ConversionStrategy)
    .Select(g => new
    {
        Strategy = g.Key,
        Count = g.Count(),
        SuccessRate = g.Count(d => d.SuccessfulConversion) / (double)g.Count(),
        AvgTime = g.Average(d => d.ConversionTimeMicroseconds)
    })
    .OrderByDescending(x => x.Count);

// Example output:
// DirectParse: 50000 conversions, 99.8% success, 12μs avg
// EnumParse: 10000 conversions, 95.2% success, 45μs avg
// CustomConverter: 5000 conversions, 100% success, 150μs avg
```

---

### 9.7 Caching and Reuse

#### 9.7.1 Configuration Reuse

```csharp
// GOOD: Reuse configurations across multiple materializations
var schema = new SchemaConfig<Order>
{
    Culture = CultureInfo.InvariantCulture,
    CaseInsensitiveMapping = true
};

var quality = QualityConfig.Standard;
var logging = LogConfig.Errors.ToLogger(logger);

// Same configuration, different data sources
var ordersFromCsv = ObjectMaterializer.Create<Order>(
    csvHeaders, csvRows, schema, quality, logging);

var ordersFromExcel = ObjectMaterializer.Create<Order>(
    excelHeaders, excelRows, schema, quality, logging);

var ordersFromApi = ObjectMaterializer.Create<Order>(
    apiHeaders, apiRows, schema, quality, logging);
```

#### 9.7.2 Builder Pattern Reuse

```csharp
// Create base builder
var baseBuilder = ObjectMaterializer.For<Order>()
    .Schema(s => s.WithCulture(CultureInfo.InvariantCulture))
    .Quality(QualityConfig.Standard);

// Customize for different scenarios
var csvOrders = baseBuilder
    .Logging(LogConfig.Errors.ToLogger(csvLogger))
    .Materialize(csvHeaders, csvRows);

var excelOrders = baseBuilder
    .Logging(LogConfig.Warnings.ToLogger(excelLogger))
    .Materialize(excelHeaders, excelRows);
```

---

### 9.8 Expected Overhead by Configuration

#### 9.8.1 Logging Overhead

| LogLevel | Overhead | Use Case |
|----------|----------|----------|
| **None** | 0% | High-throughput production |
| **Errors** | 2-5% | Standard production |
| **Warnings** | 5-10% | Production with monitoring |
| **Info** | 10-15% | Development/staging |
| **All** | 10-20% | Debugging |
| **Debug** | 30-50% | Performance analysis |

```csharp
// Benchmark: 100K rows
// None:     45.2ms (baseline)
// Errors:   47.8ms (+5.8%)
// Warnings: 49.5ms (+9.5%)
// Info:     51.2ms (+13.3%)
// All:      53.8ms (+19.0%)
// Debug:    68.3ms (+51.1%)
```

#### 9.8.2 Quality Level Overhead

| QualityLevel | Overhead | Validation |
|--------------|----------|------------|
| **Lenient** | 0% (baseline) | Minimal |
| **Standard** | 5-10% | Balanced |
| **Strict** | 15-25% | Maximum |

```csharp
// Benchmark: 100K rows with LogLevel.None
// Lenient:  45.2ms (baseline)
// Standard: 48.7ms (+7.7%)
// Strict:   54.3ms (+20.1%)
```

#### 9.8.3 Culture Overhead

```csharp
// Benchmark: 100K rows with mixed numeric/date data
// InvariantCulture:  45.2ms (baseline)
// en-US:             47.1ms (+4.2%)   // Thousand separators, AM/PM
// de-DE:             49.8ms (+10.2%)  // Comma as decimal, different date format
// ja-JP:             51.5ms (+13.9%)  // Different calendar system

// Why: Culture-specific parsing requires additional checks and conversions

```

---


## 10. API Reference

### 10.1 Core Types

#### 10.1.1 ObjectMaterializer

```csharp
public static class ObjectMaterializer
{
    // Sequential materialization
    public static IEnumerable<T> Create<T>(
        string[] headers,
        IEnumerable<object[]> rows,
        SchemaConfig<T> schema,
        QualityConfig quality,
        LogConfig logging);

    // Parallel materialization
    public static IReadOnlyList<T> CreateParallel<T>(
        string[] headers,
        IReadOnlyList<object[]> rows,
        SchemaConfig<T> schema,
        QualityConfig quality,
        LogConfig logging,
        int degreeOfParallelism = -1);  // -1 = Environment.ProcessorCount

    // Async streaming
    public static IAsyncEnumerable<T> CreateStream<T>(
        string[] headers,
        IAsyncEnumerable<object[]> rows,
        SchemaConfig<T> schema,
        QualityConfig quality,
        LogConfig logging);

    // Builder entry point
    public static MaterializerBuilder<T> For<T>();
}
```

---

#### 10.1.2 MaterializerBuilder<T>

```csharp
public class MaterializerBuilder<T>
{
    // Configuration methods
    public MaterializerBuilder<T> Schema(SchemaConfig<T> config);
    public MaterializerBuilder<T> Schema(Action<SchemaConfig<T>> configure);
    
    public MaterializerBuilder<T> Quality(QualityConfig config);
    public MaterializerBuilder<T> Quality(Action<QualityConfig> configure);
    
    public MaterializerBuilder<T> Logging(LogConfig config);
    public MaterializerBuilder<T> Logging(Action<LogConfig> configure);

    // Materialization methods
    public IEnumerable<T> Materialize(
        string[] headers,
        IEnumerable<object[]> rows);

    public IReadOnlyList<T> MaterializeParallel(
        string[] headers,
        IReadOnlyList<object[]> rows,
        int degreeOfParallelism = -1);

    public IAsyncEnumerable<T> MaterializeAsync(
        string[] headers,
        IAsyncEnumerable<object[]> rows);
}
```

---

### 10.2 Configuration Types

#### 10.2.1 SchemaConfig<T>

```csharp
public class SchemaConfig<T>
{
    // Mapping options
    public bool CaseInsensitiveMapping { get; init; } = true;
    
    // Culture settings
    public CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;
    public string[]? DateTimeFormats { get; init; } = null;
    public bool AllowThousandsSeparators { get; init; } = false;
    
    // Enum parsing
    public bool CaseInsensitiveEnums { get; init; } = true;
	
	public SchemaConfig<T> WithCulture(string cultureName);
	public SchemaConfig<T> WithCulture(CultureInfo culture);
	public SchemaConfig<T> WithDateFormats(params string[] formats);
	public SchemaConfig<T> AllowThousands();
	public SchemaConfig<T> StrictEnums();
	public SchemaConfig<T> CaseSensitiveMapping();

}
```

---

#### 10.2.2 QualityConfig

```csharp
public class QualityConfig
{
    // Validation level
    public QualityLevel Level { get; init; } = QualityLevel.Standard;
    
    // String handling
    public bool TrimWhitespace { get; init; } = false;
    public EmptyStringPolicy EmptyStrings { get; init; } = EmptyStringPolicy.AsIs;
    
    // Error handling
    public Func<ErrorContext, ErrorResolution>? Handler { get; init; } = null;
    public Func<FallbackContext, object?>? Fallback { get; init; } = null;
    
    // Field-specific rules
    public IDictionary<string, FieldRule> FieldRules { get; init; }
        = new Dictionary<string, FieldRule>();
    
    
    public QualityConfig Trim();
	public QualityConfig TreatEmptyAs(EmptyStringPolicy policy);
	public QualityConfig WithHandler(Func<ErrorContext, ErrorResolution> handler);
	public QualityConfig WithFallback(Func<FallbackContext, object?> fallback);
	
	// Presets
	public QualityConfig Critical(params string[] fields);
	public QualityConfig Required(params string[] fields);
	public QualityConfig Optional(params string[] fields);

}
```

**Enums:**

```csharp
public enum QualityLevel
{
    Strict,    // Throw on any error
    Standard,  // Use defaults for optional, throw for required
    Lenient    // Use defaults for everything, never throw
}


public enum EmptyStringPolicy
{
    AsIs,           // Keep as empty string
    ConvertToNull,  // Convert to null
    Error           // Treat as error
}
```

---

#### 10.2.3 LogConfig

```csharp
public class LogConfig
{
    // Logging level
    public LogLevel Level { get; init; } = LogLevel.Errors;
    
    // Output sink
    public IMaterializationSink? Sink { get; init; } = null;
    
    // Filtering
    public Func<DiagnosticEvent, bool>? Filter { get; init; } = null;
    
    // Privacy controls
    public bool IncludeAttemptedValue { get; init; } = false;
    public bool IncludeTiming { get; init; } = false;
    public bool IncludeStrategy { get; init; } = true;
    
    // Presets
    public static LogConfig None { get; }
    public static LogConfig Errors { get; }
    public static LogConfig Warnings { get; }
    public static LogConfig Info { get; }
    public static LogConfig All { get; }
    public static LogConfig Debug { get; }
    
    // Fluent API - Sink Configuration
	public LogConfig To(IMaterializationSink sink);          // Custom sink
	public LogConfig ToConsole();                            // Built-in console sink
	public LogConfig ToLogger(ILogger logger);               // Built-in logger sink
	public LogConfig ToCollection(out CollectionSink sink);  // Built-in collection sink

	// Note: Each To*() method returns a NEW LogConfig instance (immutable).
	// Multiple calls create a CompositeSink that reports to all sinks.

    public LogConfig Where(Func<DiagnosticEvent, bool> filter);
    public LogConfig WithAttemptedValues();
    public LogConfig WithoutAttemptedValues();
    public LogConfig WithTiming();
    public LogConfig WithoutTiming();
    public LogConfig WithStrategy();
    public LogConfig WithoutStrategy();
    

}
```

**Enum:**

```csharp
public enum LogLevel
{
    None,       // No logging
    Errors,     // Errors only
    Warnings,   // Warnings + errors
    Info,       // Info + warnings + errors
    All,        // Everything including success
    Debug       // All + timing + attempted values
}
```

---

### 10.3 Error Handling Types

#### 10.3.1 ErrorContext

```csharp
public class ErrorContext
{
    public int RowIndex { get; init; }
    public string MemberName { get; init; }
    public object? AttemptedValue { get; init; }
    public Type TargetType { get; init; }
    public Exception? Exception { get; init; }
    public IDictionary<string, object?> AdditionalData { get; init; }
}
```

#### 10.3.2 FallbackContext

```csharp
public class FallbackContext
{
    public int RowIndex { get; init; }
    public string MemberName { get; init; }
    public object? AttemptedValue { get; init; }
    public Type TargetType { get; init; }
    public IDictionary<string, object?> AdditionalData { get; init; }
}
```

#### 10.3.3 ErrorResolution

```csharp
public enum ErrorResolution
{
    Throw,           // Throw MaterializationException
    UseDefault,      // Use default(T) for property
    UseCustomValue,  // Use value from Fallback()
    SkipProperty,    // Don't assign property
    SkipRow          // Filter row from results
}
```

#### 10.3.4 FieldRule

```csharp
public enum FieldQuality
{
    Standard,   // Follow QualityLevel default
    Critical,   // Skip row if invalid
    Required,   // Throw if invalid
    Optional    // Use default if invalid
}

public class FieldRule
{
    public FieldQuality Quality { get; init; } = FieldQuality.Standard;
    public object? FallbackValue { get; init; }
}


```

---

### 10.4 Diagnostics Types

#### 10.4.1 DiagnosticEvent

```csharp
public class DiagnosticEvent
{
    public DateTimeOffset Timestamp { get; init; }
    public int RowIndex { get; init; }
    public string MemberName { get; init; }
    public DiagnosticSeverity Severity { get; init; }
    public string Message { get; init; }
    public object? AttemptedValue { get; init; }
    public Type TargetType { get; init; }
    public string ConversionStrategy { get; init; }
    public long ConversionTimeMicroseconds { get; init; }
    public IDictionary<string, object?> Additional { get; init; }
    public bool SuccessfulConversion { get; init; }
}
```

**Enum:**

```csharp
public enum DiagnosticSeverity
{
    Error,
    Warning,
    Info,
    Debug
}
```

#### 10.4.2 IMaterializationSink

```csharp
public interface IMaterializationSink
{
    void Report(DiagnosticEvent diagnostic);
}
```

#### 10.4.3 CollectionSink

```csharp
[ThreadSafe]
public class CollectionSink : IMaterializationSink
{
    public IReadOnlyList<DiagnosticEvent> Diagnostics { get; }
    
    public IEnumerable<DiagnosticEvent> Errors { get; }
    public IEnumerable<DiagnosticEvent> Warnings { get; }
    public IEnumerable<DiagnosticEvent> Infos { get; }
    public IEnumerable<DiagnosticEvent> Debug { get; }
    
    public void Report(DiagnosticEvent diagnostic);
    public void Clear();
}
```

#### 10.4.4 Built-in Sinks

```csharp
[ThreadSafe]
public class ConsoleSink : IMaterializationSink
{
    public void Report(DiagnosticEvent diagnostic);
}

[ThreadSafe]
public class MicrosoftLoggerSink : IMaterializationSink
{
    public MicrosoftLoggerSink(ILogger logger);
    public void Report(DiagnosticEvent diagnostic);
}
```

---

### 10.5 Presets

```csharp
public static class Presets
{
    // CSV import with lenient parsing
    public static MaterializerBuilder<T> CsvImport<T>(ILogger logger);
    
    // Excel import with common date formats
    public static MaterializerBuilder<T> ExcelImport<T>(ILogger logger);
    
    // Standard production configuration
    public static MaterializerBuilder<T> Production<T>(ILogger logger);
    
    // Development with full diagnostics
    public static MaterializerBuilder<T> Development<T>();
    
    // Strict validation for data quality checks
    public static MaterializerBuilder<T> Validation<T>();
    
    // Auditing with diagnostic collection
    public static MaterializerBuilder<T> Auditing<T>(out CollectionSink sink);
    
    // API deserialization with strict validation
    public static MaterializerBuilder<T> ApiDeserialization<T>();
    
    // Production-safe (no PII in logs)
    public static MaterializerBuilder<T> ProductionSafe<T>(ILogger logger);
}
```

---

### 10.6 Exceptions

#### 10.6.1 MaterializationException

```csharp
public class MaterializationException : Exception
{
    public int RowIndex { get; }
    public string MemberName { get; }
    public object? AttemptedValue { get; }
    public Type TargetType { get; }
    
    public MaterializationException(
        string message,
        int rowIndex,
        string memberName,
        object? attemptedValue,
        Type targetType,
        Exception? innerException = null);
}
```

#### 10.6.2 ConfigurationException

```csharp
public class ConfigurationException : Exception
{
    public string ConfigurationAxis { get; }  // "Schema", "Quality", or "Logging"
    
    public ConfigurationException(
        string message,
        string configurationAxis,
        Exception? innerException = null);
}
```

---

## 11. Best Practices

### 11.1 Configuration Management

#### 11.1.1 Use Presets as Starting Points

```csharp
// GOOD: Start with preset, customize as needed
var orders = Presets.CsvImport<Order>(logger)
    .Quality(q => q.Level = QualityLevel.Standard)
    .Materialize(headers, rows);

// AVOID: Building from scratch every time
var orders = ObjectMaterializer.For<Order>()
    .Schema(new SchemaConfig<Order>
    {
        CaseInsensitiveMapping = true,
        Culture = CultureInfo.InvariantCulture
    })
    .Quality(new QualityConfig
    {
        Level = QualityLevel.Lenient,
        TrimWhitespace = true,
        EmptyStrings = EmptyStringPolicy.ConvertToNull
    })
    .Logging(LogConfig.Errors.ToLogger(logger))
    .Materialize(headers, rows);
```

#### 11.1.2 Centralize Configuration

```csharp
// GOOD: Configuration factory
public static class MaterializerConfigs
{
    public static SchemaConfig<T> GetStandardSchema<T>() =>
        new SchemaConfig<T>
        {
            Culture = CultureInfo.InvariantCulture,
            CaseInsensitiveMapping = true
        };
    
    public static QualityConfig GetProductionQuality() =>
        QualityConfig.Standard with  // Use with-expression
		{
			TrimWhitespace = true,
			Handler = ProductionErrorHandler
		};
    
    private static ErrorResolution ProductionErrorHandler(ErrorContext ctx)
    {
        // Centralized error handling logic
        return ctx.MemberName switch
        {
            "Price" or "Quantity" => ErrorResolution.UseDefault,
            _ => ErrorResolution.Throw
        };
    }
}

// Usage
var orders = ObjectMaterializer.Create<Order>(
    headers, rows,
    MaterializerConfigs.GetStandardSchema<Order>(),
    MaterializerConfigs.GetProductionQuality(),
    LogConfig.Errors.ToLogger(logger));
```

---

### 11.2 Error Handling Strategies

#### 11.2.1 Fail Fast vs. Collect Errors

```csharp
// Fail Fast: Stop on first error
var quality = QualityConfig.Strict;
try
{
    var orders = ObjectMaterializer.Create<Order>(
        headers, rows, schema, quality, logging).ToList();
}
catch (MaterializationException ex)
{
    logger.LogError($"Row {ex.RowIndex}, {ex.MemberName}: {ex.Message}");
}

// Collect Errors: Process all rows, review errors after
var sink = new CollectionSink();
var orders = ObjectMaterializer.Create<Order>(
    headers, rows,
    schema,
    QualityConfig.Lenient,
    LogConfig.Errors.ToCollection(out sink)
).ToList();

if (sink.Errors.Any())
{
    foreach (var error in sink.Errors)
    {
        logger.LogWarning($"Row {error.RowIndex}, {error.MemberName}: {error.Message}");
    }
}
```

#### 11.2.2 Graceful Degradation

```csharp
// Use fallback values for non-critical fields
var quality = new QualityConfig
{
    Level = QualityLevel.Standard,
    FieldRules = new Dictionary<string, FieldRule>
    {
        ["DiscountPercent"] = new FieldRule
        {
            IsOptional = true, 
            FallbackValue = 0m
        },
        ["Notes"] = new FieldRule
        {
            IsOptional = true, 
            FallbackValue = string.Empty
        }
    },
    Handler = context =>
    {
        // Critical fields: fail
        if (context.MemberName is "CustomerId" or "OrderDate")
            return ErrorResolution.Throw;
        
        // Optional fields: use default
        return ErrorResolution.UseDefault;
    }
};
```

---

### 11.3 Performance Optimization

#### 11.3.1 Choose the Right Method

```csharp
// Small datasets (<10K rows): Use Create
if (rows.Count < 10_000)
{
    var orders = ObjectMaterializer.Create<Order>(
        headers, rows, schema, quality, logging);
}

// Large datasets (>100K rows): Use CreateParallel
else if (rows.Count > 100_000)
{
    var orders = ObjectMaterializer.CreateParallel<Order>(
        headers, rows, schema, quality, logging);
}

// Streaming data: Use CreateStream
else
{
    await foreach (var order in ObjectMaterializer.CreateStream<Order>(
        headers, rowsAsync, schema, quality, logging))
    {
        await ProcessOrderAsync(order);
    }
}
```

#### 11.3.2 Minimize Logging Overhead

```csharp
// Production: Log errors only
var prodLogging = LogConfig.Errors
    .ToLogger(logger)
    .WithoutAttemptedValues()  // Privacy + performance
    .WithoutTiming();

// Development: Full diagnostics
var devLogging = LogConfig.Debug
    .ToConsole()
    .WithAttemptedValues()
    .WithTiming();

// Use appropriate config per environment
var logging = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development"
    ? devLogging
    : prodLogging;
```

#### 11.3.3 Reuse Configuration Objects

```csharp
// GOOD: Reuse immutable configurations
private static readonly SchemaConfig<Order> _orderSchema = new()
{
    Culture = CultureInfo.InvariantCulture,
    DateTimeFormats = new[] { "yyyy-MM-dd", "MM/dd/yyyy" }
};

private static readonly QualityConfig _standardQuality = QualityConfig.Standard;

public IEnumerable<Order> ImportOrders(string[] headers, IEnumerable<object[]> rows)
{
    return ObjectMaterializer.Create<Order>(
        headers, rows, _orderSchema, _standardQuality, _logging);
}

// AVOID: Creating new configs every time
public IEnumerable<Order> ImportOrders(string[] headers, IEnumerable<object[]> rows)
{
    return ObjectMaterializer.Create<Order>(
        headers, rows,
        new SchemaConfig<Order> { /* ... */ },  // Allocation overhead
        new QualityConfig { /* ... */ },
        LogConfig.Errors);
}
```

#### 11.3.4 Parallel Processing Guidelines

```csharp
// Tune degree of parallelism based on workload
var degreeOfParallelism = rows.Count switch
{
    < 10_000 => 1,                              // Sequential
    < 100_000 => Environment.ProcessorCount / 2, // Half cores
    _ => Environment.ProcessorCount              // All cores
};

var orders = ObjectMaterializer.CreateParallel<Order>(
    headers, rows, schema, quality, logging, degreeOfParallelism);

// For I/O-bound operations, consider higher parallelism
var degreeOfParallelism = Environment.ProcessorCount * 2;
```

---

### 11.4 Memory Management

#### 11.4.1 Use Lazy Evaluation for Large Datasets

```csharp
// GOOD: Lazy evaluation with Create
var orders = ObjectMaterializer.Create<Order>(
    headers, rows, schema, quality, logging);

foreach (var order in orders)  // Materialized one at a time
{
    ProcessOrder(order);
}

// AVOID: Eager evaluation for large datasets
var orders = ObjectMaterializer.Create<Order>(
    headers, rows, schema, quality, logging)
    .ToList();  // Loads all into memory
```

#### 11.4.2 Stream Processing for Very Large Files

```csharp
// Stream from file without loading entire dataset
public async Task ProcessLargeFileAsync(string filePath)
{
    var headers = await ReadHeadersAsync(filePath);
    var rowsAsync = ReadRowsAsync(filePath);
    
    await foreach (var order in ObjectMaterializer.CreateStream<Order>(
        headers, rowsAsync, schema, quality, logging))
    {
        await ProcessOrderAsync(order);
        // Each row is processed and released immediately
    }
}

private async IAsyncEnumerable<object[]> ReadRowsAsync(string filePath)
{
    using var reader = new StreamReader(filePath);
    await reader.ReadLineAsync(); // Skip header
    
    string? line;
    while ((line = await reader.ReadLineAsync()) != null)
    {
        yield return ParseCsvLine(line);
    }
}
```

#### 11.4.3 Clear Diagnostic Sinks Periodically

```csharp
// For long-running processes with CollectionSink
var sink = new CollectionSink();
var logging = LogConfig.Warnings.ToCollection(out sink);

foreach (var batch in GetBatches())
{
    var orders = ObjectMaterializer.Create<Order>(
        headers, batch, schema, quality, logging).ToList();
    
    ProcessOrders(orders);
    
    // Review diagnostics
    if (sink.Errors.Any())
    {
        LogErrors(sink.Errors);
    }
    
    // Clear to prevent memory growth
    sink.Clear();
}
```

---

### 11.5 Type Safety and Validation

#### 11.5.1 Use Strongly-Typed Models

```csharp
// GOOD: Strongly-typed model
public class Order
{
    public int OrderId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public OrderStatus Status { get; set; }
}

public enum OrderStatus
{
    Pending,
    Confirmed,
    Shipped,
    Delivered
}

// AVOID: Weakly-typed model
public class Order
{
    public object OrderId { get; set; }      // Loses type safety
    public string OrderDate { get; set; }    // Should be DateTime
    public string TotalAmount { get; set; }  // Should be decimal
    public string Status { get; set; }       // Should be enum
}
```

#### 11.5.2 Validate Business Rules with Custom Handlers

```csharp
var quality = new QualityConfig
{
    Level = QualityLevel.Standard,
    Handler = context =>
    {
        // Type validation
        if (context.MemberName == "TotalAmount" && context.AttemptedValue is decimal amount)
        {
            if (amount < 0)
            {
                context.AdditionalData["ValidationError"] = "Amount cannot be negative";
                return ErrorResolution.Throw;
            }
            if (amount > 1_000_000)
            {
                context.AdditionalData["ValidationWarning"] = "Unusually large amount";
                return ErrorResolution.UseDefault;
            }
        }
        
        // Date validation
        if (context.MemberName == "OrderDate" && context.AttemptedValue is DateTime date)
        {
            if (date > DateTime.Now.AddDays(30))
            {
                context.AdditionalData["ValidationError"] = "Future date too far";
                return ErrorResolution.Throw;
            }
        }
        
        return ErrorResolution.UseDefault;
    }
};
```

---

### 11.6 Thread Safety

#### 11.6.1 Use Thread-Safe Sinks with CreateParallel

```csharp
// GOOD: Built-in thread-safe sinks
var sink = new CollectionSink();  // Thread-safe
var logging = LogConfig.Warnings.ToCollection(out sink);

var orders = ObjectMaterializer.CreateParallel<Order>(
    headers, rows, schema, quality, logging);

// GOOD: Multiple thread-safe sinks
var logging = LogConfig.Warnings
    .ToLogger(logger)           // MicrosoftLoggerSink is thread-safe
    .ToConsole()             // ConsoleSink is thread-safe
    .ToCollection(out sink); // CollectionSink is thread-safe

// AVOID: Custom sink without [ThreadSafe] attribute
public class CustomSink : IMaterializationSink
{
    private readonly List<DiagnosticEvent> _events = new();
    
    public void Report(DiagnosticEvent diagnostic)
    {
        _events.Add(diagnostic);  // NOT thread-safe!
    }
}

// This will throw InvalidOperationException
var orders = ObjectMaterializer.CreateParallel<Order>(
    headers, rows, schema, quality,
    LogConfig.Warnings.To(new CustomSink()));
```

#### 11.6.2 Mark Custom Sinks as Thread-Safe

```csharp
// GOOD: Thread-safe custom sink
[ThreadSafe]
public class ConcurrentCustomSink : IMaterializationSink
{
    private readonly ConcurrentBag<DiagnosticEvent> _events = new();
    
    public IReadOnlyList<DiagnosticEvent> Events => _events.ToList();
    
    public void Report(DiagnosticEvent diagnostic)
    {
        _events.Add(diagnostic);  // Thread-safe
    }
}

// Now safe to use with CreateParallel
var orders = ObjectMaterializer.CreateParallel<Order>(
    headers, rows, schema, quality,
    LogConfig.Warnings.To(new ConcurrentCustomSink()));
```

---

### 11.7 Privacy and Security

#### 11.7.1 Disable Attempted Values in Production

```csharp
// GOOD: Production-safe logging
var logging = LogConfig.Errors
    .ToLogger(logger)
    .WithoutAttemptedValues()  // No PII in logs
    .WithoutTiming();

// Or use preset
var orders = Presets.ProductionSafe<Order>(logger)
    .Materialize(headers, rows);

// AVOID: Logging sensitive data in production
var logging = LogConfig.Debug
    .ToLogger(logger)
    .WithAttemptedValues();  // May expose PII!
```

#### 11.7.2 Filter Sensitive Fields from Diagnostics

```csharp
var logging = LogConfig.Warnings
    .ToLogger(logger)
    .Where(e =>
    {
        // Exclude sensitive fields from diagnostics
        var sensitiveFields = new[] { "Password", "CreditCard", "SSN" };
        return !sensitiveFields.Contains(e.MemberName);
    });
```

#### 11.7.3 Sanitize Custom Error Messages

```csharp
var quality = new QualityConfig
{
    Handler = context =>
    {
        // Don't include actual value in error message
        if (context.MemberName == "CreditCard")
        {
            context.AdditionalData["Error"] = "Invalid credit card format";
            // Don't log context.AttemptedValue
            return ErrorResolution.Throw;
        }
        return ErrorResolution.UseDefault;
    }
};
```

---

### 11.8 Testing Strategies

#### 11.8.1 Unit Testing with CollectionSink

```csharp
[Fact]
public void Should_Report_Conversion_Errors()
{
    // Arrange
    var headers = new[] { "OrderId", "OrderDate", "Amount" };
    var rows = new[]
    {
        new object[] { "1", "2024-01-01", "100.50" },
        new object[] { "2", "invalid-date", "200.00" },  // Error
        new object[] { "3", "2024-01-03", "not-a-number" }  // Error
    };
    
    var sink = new CollectionSink();
    var logging = LogConfig.Errors.ToCollection(out sink);
    
    // Act
    var orders = ObjectMaterializer.Create<Order>(
        headers, rows,
        new SchemaConfig<Order>(),
        QualityConfig.Lenient,
        logging
    ).ToList();
    
    // Assert
    Assert.Equal(3, orders.Count);  // All rows processed
    Assert.Equal(2, sink.Errors.Count());  // Two errors logged
    Assert.Contains(sink.Errors, e => e.MemberName == "OrderDate");
    Assert.Contains(sink.Errors, e => e.MemberName == "Amount");
}
```

#### 11.8.2 Integration Testing with Real Data

```csharp
[Fact]
public async Task Should_Process_Large_CSV_File()
{
    // Arrange
    var filePath = "test-data/orders-10k.csv";
    var headers = await ReadHeadersAsync(filePath);
    var rows = ReadRowsAsync(filePath);
    
    var sink = new CollectionSink();
    var logging = LogConfig.Warnings.ToCollection(out sink);
    
    // Act
    var orders = new List<Order>();
    await foreach (var order in ObjectMaterializer.CreateStream<Order>(
        headers, rows,
        new SchemaConfig<Order>(),
        QualityConfig.Standard,
        logging))
    {
        orders.Add(order);
    }
    
    // Assert
    Assert.True(orders.Count >= 9000);  // Allow some invalid rows
    Assert.True(sink.Errors.Count() < 100);  // Less than 1% error rate
}
```

#### 11.8.3 Performance Testing

```csharp
[Fact]
public void Should_Process_100K_Rows_Under_5_Seconds()
{
    // Arrange
    var headers = new[] { "OrderId", "OrderDate", "Amount" };
    var rows = GenerateTestRows(100_000);
    
    var stopwatch = Stopwatch.StartNew();
    
    // Act
    var orders = ObjectMaterializer.CreateParallel<Order>(
        headers, rows,
        new SchemaConfig<Order>(),
        QualityConfig.Standard,
        LogConfig.None  // No logging overhead
    );
    
    stopwatch.Stop();
    
    // Assert
    Assert.Equal(100_000, orders.Count);
    Assert.True(stopwatch.ElapsedMilliseconds < 5000,
        $"Processing took {stopwatch.ElapsedMilliseconds}ms");
}
```

---

### 11.9 Common Pitfalls

#### 11.9.1 Avoid Multiple Enumeration

```csharp
// AVOID: Multiple enumeration
var orders = ObjectMaterializer.Create<Order>(
    headers, rows, schema, quality, logging);

var count = orders.Count();  // First enumeration
var sum = orders.Sum(o => o.Amount);  // Second enumeration - WRONG!

// GOOD: Materialize once
var orders = ObjectMaterializer.Create<Order>(
    headers, rows, schema, quality, logging)
    .ToList();  // Single enumeration

var count = orders.Count;
var sum = orders.Sum(o => o.Amount);
```

#### 11.9.2 Don't Modify Configuration After Creation

```csharp
// AVOID: Configurations are immutable
var schema = new SchemaConfig<Order>();
schema.Culture = CultureInfo.CurrentCulture;  // Won't work - immutable!


// GOOD: Use with-expression or fluent API
var schema = SchemaConfig<Order>.Default with
{
    Culture = CultureInfo.CurrentCulture
};

// Or use fluent API:
var schema = SchemaConfig<Order>.Default
    .WithCulture(CultureInfo.CurrentCulture);

```

#### 11.9.3 Handle Async Disposal Properly

```csharp
// GOOD: Proper async disposal
public async Task ProcessStreamAsync()
{
    var rowsAsync = GetRowsAsync();
    
    await foreach (var order in ObjectMaterializer.CreateStream<Order>(
        headers, rowsAsync, schema, quality, logging))
    {
        await ProcessOrderAsync(order);
    }
    // Stream is automatically disposed
}

// AVOID: Blocking on async operations
public void ProcessStream()
{
    var rowsAsync = GetRowsAsync();
    
    var orders = ObjectMaterializer.CreateStream<Order>(
        headers, rowsAsync, schema, quality, logging)
        .ToBlockingEnumerable();  // Blocks async operations!
}
```

#### 11.9.4 Don't Ignore Diagnostics

```csharp
// AVOID: Silent failures
var orders = ObjectMaterializer.Create<Order>(
    headers, rows, schema, QualityConfig.Lenient, LogConfig.None)
    .ToList();
// No way to know if errors occurred!

// GOOD: Always collect diagnostics
var sink = new CollectionSink();
var orders = ObjectMaterializer.Create<Order>(
    headers, rows, schema, QualityConfig.Lenient,
    LogConfig.Warnings.ToCollection(out sink))
    .ToList();

if (sink.Errors.Any())
{
    logger.LogWarning($"Processed {orders.Count} orders with {sink.Errors.Count()} errors");
}
```

---
