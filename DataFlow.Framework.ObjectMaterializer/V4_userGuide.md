# ObjectMaterializer Framework: User Guide

**Version:** 4.0  
**Last Updated:** 2025-10-10

---

## Table of Contents

1. [Overview](#1-overview)
2. [Quick Start](#2-quick-start)
3. [Core Concepts](#3-core-concepts)
4. [Common Use Cases](#4-common-use-cases)
5. [Configuration Guide](#5-configuration-guide)
6. [Error Handling Strategies](#6-error-handling-strategies)
7. [Diagnostics & Monitoring](#7-diagnostics--monitoring)
8. [Performance Optimization](#8-performance-optimization)
9. [Advanced Scenarios](#9-advanced-scenarios)
10. [Troubleshooting](#10-troubleshooting)
11. [Best Practices](#11-best-practices)

---

## 1. Overview

The ObjectMaterializer framework provides high-performance, configurable object creation from raw data (arrays, CSV rows, Excel cells, API payloads). It automatically handles type conversions, error recovery, and diagnostic reporting.

### Key Features

- **Zero-allocation expression trees** for property/field assignment
- **Configurable error handling** (fail-fast, use defaults, skip rows/properties)
- **Culture-aware parsing** for numbers, dates, and enums
- **Pluggable diagnostics** (console, logger, custom sinks)
- **Parallel processing** support for large datasets
- **Async streaming** for memory-efficient imports

### When to Use

✅ **CSV/Excel imports** with messy data  
✅ **API deserialization** with strict validation  
✅ **ETL pipelines** requiring data quality auditing  
✅ **Batch processing** of millions of rows  
✅ **Dynamic object creation** from schema-based data  

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
var schema = new[] { "Id", "Name", "Age", "BirthDate" };
var rows = new[]
{
    new object[] { "1", "Alice", "30", "1994-05-15" },
    new object[] { "2", "Bob", "25", "1999-08-22" }
};

// Materialize objects (lazy evaluation)
var people = ObjectMaterializer.CreateBatch<Person>(schema, rows);

// ⚠️ Note: CreateBatch returns IEnumerable<T> with deferred execution.
// Rows are processed lazily. To force immediate evaluation:
var peopleList = people.ToList(); // or .ToArray()
```

### With Error Handling

```csharp
var options = new MaterializationOptions
{
    DefaultErrorResolution = ErrorResolution.UseDefault,
    Sink = new ConsoleSink(SinkVerbosity.ErrorsOnly)
};

var people = ObjectMaterializer.CreateBatch<Person>(schema, rows, options);
// Invalid data → uses default values (0, null) + logs errors to console
```

---

## 3. Core Concepts

### 3.1 Materialization Flow

```
Raw Data (object[])
    ↓
Schema Mapping (string[] → property names)
    ↓
Type Conversion (string → int, DateTime, etc.)
    ↓
Error Resolution (throw, use default, skip, custom value)
    ↓
Diagnostic Reporting (sinks)
    ↓
Materialized Object (T)
```

### 3.2 Options Hierarchy

```
MaterializationOptions (runtime configuration)
├── CompilationOptions (affects caching)
│   ├── Culture (parsing locale)
│   ├── DateTimeFormats (explicit formats)
│   ├── AllowThousandsSeparators (1,234 vs 1234)
│   └── CaseInsensitiveHeaders (Name vs name)
│
├── Conversion Behavior (runtime)
│   ├── TrimStrings (whitespace handling)
│   ├── NullStringBehavior (null/"" handling)
│   ├── CaseInsensitiveEnums (Status vs status)
│   └── AllowChangeTypeFallback (slow but flexible)
│
├── Error Handling
│   ├── DefaultErrorResolution (global strategy)
│   ├── OnError (per-property callback)
│   └── CustomValueProvider (fallback values)
│
└── Diagnostics
    └── Sink (observability)
```

### 3.3 Error Resolution Strategies

| Strategy | Behavior | When to Use |
|----------|----------|-------------|
| **Throw** | Stop immediately, throw `MaterializationException` | Development, strict validation |
| **UseDefault** | Use `default(T)` (0, null, false) | Production imports with optional fields |
| **UseCustomValue** | Use value from `CustomValueProvider` | Placeholder values ("Unknown", -1) |
| **SkipProperty** | Leave property uninitialized | Non-critical fields |
| **SkipRow** | Return `null`, skip entire row | Critical field failures |

---

## 4. Common Use Cases

### 4.1 CSV Import (Lenient)

**Scenario:** User-uploaded CSV with inconsistent formatting (extra spaces, missing values).

```csharp
var options = MaterializationOptions.ForCsvImport(logger);
// Configured as:
// - TrimStrings = true (handle whitespace)
// - NullStringBehavior = PreserveEmptyStrings (distinguish null vs "")
// - DefaultErrorResolution = UseDefault (continue on errors)
// - Sink = MicrosoftLoggerSink (log warnings/errors)

var people = ObjectMaterializer.CreateBatch<Person>(schema, csvRows, options);
```

**How it works:**
- `"  Alice  "` → `"Alice"` (trimmed)
- `""` (empty age) → `0` (default int)
- `"invalid-date"` → `DateTime.MinValue` (default) + logged warning

---

### 4.2 Excel Import (Strict Dates)

**Scenario:** Financial data with specific date formats (yyyy-MM-dd, MM/dd/yyyy).

```csharp
var options = MaterializationOptions.ForExcelImport(logger);
// Configured as:
// - DateTimeFormats = ["yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy"]
// - NullStringBehavior = ConvertToDefault (empty cells → defaults)
// - TrimStrings = true

var transactions = ObjectMaterializer.CreateBatch<Transaction>(schema, excelRows, options);
```

**How it works:**
- `"2024-12-31"` → parsed using first matching format
- `"12/31/2024"` → parsed using second format
- `"31-12-2024"` → **error** (not in formats list) → default + logged

---

### 4.3 API Deserialization (Fail-Fast)

**Scenario:** REST API request body validation (reject invalid requests immediately).

```csharp
var options = MaterializationOptions.ForApiDeserialization();
// Configured as:
// - TrimStrings = false (preserve exact input)
// - NullStringBehavior = Error (reject null/empty)
// - DefaultErrorResolution = Throw (fail-fast)
// - AllowChangeTypeFallback = false (strict types)

try
{
    var request = ObjectMaterializer.Create<CreateUserRequest>(schema, values, options);
    // Process valid request
}
catch (MaterializationException ex)
{
    return BadRequest(new { error = ex.Message, context = ex.Context });
}
```

**How it works:**
- `null` or `""` for required field → throws immediately
- `"abc"` for int field → throws with detailed error context
- Valid data → creates object, continues

---

### 4.4 Data Quality Auditing

**Scenario:** Import data but collect all warnings/errors for review.

```csharp
var options = MaterializationOptions.ForAuditing(out var sink);
// Configured as:
// - Sink = CollectionSink (collect diagnostics in memory)
// - DefaultErrorResolution = UseDefault (continue processing)
// - Verbosity = WarningsAndErrors

var people = ObjectMaterializer.CreateBatch<Person>(schema, rows, options);

// Review issues
var errors = sink.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
var warnings = sink.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning);

Console.WriteLine($"Imported {people.Count} rows with {errors.Count()} errors, {warnings.Count()} warnings");

foreach (var error in errors)
{
    Console.WriteLine($"Row {error.RowIndex}: {error.Message}");
}
```

---

### 4.5 Critical Fields (Skip Invalid Rows)

**Scenario:** Import orders, but skip rows where `OrderId` or `CustomerId` are invalid.

```csharp
var options = new MaterializationOptions
{
    DefaultErrorResolution = ErrorResolution.UseDefault, // For non-critical fields
    OnError = ctx => ctx.MemberName is "OrderId" or "CustomerId"
        ? ErrorResolution.SkipRow  // Skip entire row
        : ErrorResolution.UseDefault, // Use default for other fields
    Sink = new ConsoleSink(SinkVerbosity.WarningsAndErrors)
};

var orders = ObjectMaterializer.CreateBatch<Order>(schema, rows, options);
// Invalid OrderId → row skipped (returns null)
// Invalid ShipDate → uses DateTime.MinValue, continues
```

**Alternative (using builder):**

```csharp
var options = new MaterializationOptionsBuilder()
    .WithCriticalFields("OrderId", "CustomerId")
    .WithErrorResolution(ErrorResolution.UseDefault)
    .WithSink(new ConsoleSink(SinkVerbosity.WarningsAndErrors))
    .Build();
```

---

### 4.6 Custom Fallback Values

**Scenario:** Use placeholder values instead of defaults.

```csharp
var options = new MaterializationOptions
{
    DefaultErrorResolution = ErrorResolution.UseCustomValue,
    CustomValueProvider = ctx => ctx.MemberName switch
    {
        "Age" => 0,
        "Country" => "Unknown",
        "Status" => OrderStatus.Pending,
        _ => null
    },
    Sink = new ConsoleSink(SinkVerbosity.ErrorsOnly)
};

var people = ObjectMaterializer.CreateBatch<Person>(schema, rows, options);
// Invalid age → 0
// Invalid country → "Unknown"
// Invalid status → OrderStatus.Pending
```

---

### 4.7 Parallel Processing (Large Datasets)

**Scenario:** Import 10 million rows using all CPU cores.

```csharp
var options = MaterializationOptions.ForProduction(logger);

var people = ObjectMaterializer.CreateBatchParallel<Person>(
    schema,
    rows, // IReadOnlyList<object[]>
    options,
    degreeOfParallelism: Environment.ProcessorCount
);

// Uses Parallel.For with thread-local lists
// ~3-4x faster than sequential for CPU-bound conversions
```

**⚠️ Requirements:**
- Sink must be **thread-safe** (`ConsoleSink`, `MicrosoftLoggerSink`, `CollectionSink`)
- Input must be `IReadOnlyList<object[]>` (not `IEnumerable`)

---

### 4.8 Async Streaming (Memory Efficient)

**Scenario:** Process 100GB CSV file without loading entire file into memory.

```csharp
var options = MaterializationOptions.ForCsvImport(logger);

await foreach (var person in ObjectMaterializer.CreateStream<Person>(
    schema,
    ReadCsvRowsAsync(filePath), // IAsyncEnumerable<object[]>
    options))
{
    await ProcessPersonAsync(person);
    // Only one row in memory at a time
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

---

## 5. Configuration Guide

### 5.1 Preset Configurations

Use presets for common scenarios:

```csharp
// 1. Production (minimal overhead, log errors only)
var options = MaterializationOptions.ForProduction(logger);

// 2. Development (verbose output, fail-fast)
var options = MaterializationOptions.ForDevelopment();

// 3. Validation (strict, reject invalid data)
var options = MaterializationOptions.ForValidation();

// 4. Auditing (collect all warnings/errors)
var options = MaterializationOptions.ForAuditing(out var sink);

// 5. CSV Import (lenient, trim whitespace)
var options = MaterializationOptions.ForCsvImport(logger);

// 6. Excel Import (strict dates, specific formats)
var options = MaterializationOptions.ForExcelImport(logger);

// 7. API Deserialization (strict, fail-fast)
var options = MaterializationOptions.ForApiDeserialization();
```

### 5.2 Custom Configuration

```csharp
var options = new MaterializationOptions
{
    // Compilation (affects caching)
    Compilation = new CompilationOptions
    {
        Culture = new CultureInfo("fr-FR"), // French number/date formats
        AllowThousandsSeparators = true, // "1 234,56" → 1234.56
        DateTimeFormats = new[] { "dd/MM/yyyy", "yyyy-MM-dd" },
        CaseInsensitiveHeaders = true // "Name" matches "name"
    },
    
    // Conversion behavior
    TrimStrings = true, // "  Alice  " → "Alice"
    NullStringBehavior = NullStringBehavior.ConvertToDefault,
    CaseInsensitiveEnums = true, // "active" → Status.Active
    AllowChangeTypeFallback = false, // Disable slow fallback
    
    // Error handling
    DefaultErrorResolution = ErrorResolution.UseDefault,
    OnError = ctx => ctx.MemberName == "Id" 
        ? ErrorResolution.SkipRow 
        : ErrorResolution.UseDefault,
    
    // Diagnostics
    Sink = new CompositeSink(
        new ConsoleSink(SinkVerbosity.ErrorsOnly),
        new CollectionSink(SinkVerbosity.WarningsAndErrors)
    )
};

options.Validate(); // Throws if invalid configuration
```

### 5.3 Fluent Builder

```csharp
var options = new MaterializationOptionsBuilder()
    .ForCsvImport(logger)
    .WithCriticalFields("Id", "Email")
    .WithCulture(new CultureInfo("en-US"))
    .WithDateTimeFormats("yyyy-MM-dd", "MM/dd/yyyy")
    .WithSink(new ConsoleSink(SinkVerbosity.ErrorsOnly))
    .Build(); // Validates automatically
```

---

## 6. Error Handling Strategies

### 6.1 Decision Tree

```
Conversion Error Occurs
    ↓
Is OnError callback set?
    ├─ YES → Call OnError(context) → Use returned ErrorResolution
    └─ NO  → Use DefaultErrorResolution
        ↓
ErrorResolution = ?
    ├─ Throw          → Throw MaterializationException (stop processing)
    ├─ UseDefault     → Use default(T) (0, null, false)
    ├─ UseCustomValue → Call CustomValueProvider(context)
    ├─ SkipProperty   → Leave property uninitialized
    └─ SkipRow        → Return null (skip entire row)
```

### 6.2 Strategy Selection Guide

| Scenario | Recommended Strategy | Rationale |
|----------|---------------------|-----------|
| **Development** | `Throw` | Fail-fast to identify issues early |
| **Production Import** | `UseDefault` | Graceful degradation, log warnings |
| **Critical Fields** | `SkipRow` (via `OnError`) | Invalid ID/email → skip entire record |
| **Optional Fields** | `SkipProperty` | Missing phone number → leave null |
| **Placeholder Values** | `UseCustomValue` | "Unknown" for missing country |
| **Strict Validation** | `Throw` | API requests, financial data |

### 6.3 Per-Property Error Handling

```csharp
var options = new MaterializationOptions
{
    DefaultErrorResolution = ErrorResolution.UseDefault,
    OnError = ctx => ctx.MemberName switch
    {
        // Critical fields → skip row
        "Id" or "Email" => ErrorResolution.SkipRow,
        
        // Required fields → throw
        "Name" => ErrorResolution.Throw,
        
        // Optional fields → use custom value
        "PhoneNumber" => ErrorResolution.UseCustomValue,
        
        // Everything else → use default
        _ => ErrorResolution.UseDefault
    },
    CustomValueProvider = ctx => ctx.MemberName switch
    {
        "PhoneNumber" => "N/A",
        "Country" => "Unknown",
        _ => null
    }
};
```

---

## 7. Diagnostics & Monitoring

### 7.1 Built-in Sinks

#### ConsoleSink (Development)

```csharp
var sink = new ConsoleSink(SinkVerbosity.SuccessAndFailures);
// Outputs color-coded messages to console
// Red = errors, Yellow = warnings, Cyan = info
```

#### CollectionSink (Auditing)

```csharp
var sink = new CollectionSink(SinkVerbosity.WarningsAndErrors);
var options = new MaterializationOptions { Sink = sink };

var people = ObjectMaterializer.CreateBatch<Person>(schema, rows, options);

// Review diagnostics
foreach (var diagnostic in sink.Diagnostics)
{
    Console.WriteLine($"[{diagnostic.Severity}] Row {diagnostic.RowIndex}: {diagnostic.Message}");
}

// Export to CSV
File.WriteAllLines("import-errors.csv", 
    sink.Diagnostics.Select(d => $"{d.RowIndex},{d.MemberName},{d.Message}"));
```

#### MicrosoftLoggerSink (Production)

```csharp
var sink = new MicrosoftLoggerSink(logger, SinkVerbosity.ErrorsOnly);
// Integrates with ILogger (Serilog, NLog, etc.)
// Errors → LogLevel.Error
// Warnings → LogLevel.Warning
```

#### CompositeSink (Multiple Outputs)

```csharp
var sink = new CompositeSink(
    new ConsoleSink(SinkVerbosity.ErrorsOnly),
    new CollectionSink(SinkVerbosity.WarningsAndErrors),
    new MicrosoftLoggerSink(logger, SinkVerbosity.ErrorsOnly)
);
// Broadcasts to all sinks simultaneously
```

### 7.2 Verbosity Levels

| Level | Reports | Overhead | Use Case |
|-------|---------|----------|----------|
| **ErrorsOnly** | Conversion failures only | ~2-5% | Production monitoring |
| **WarningsAndErrors** | Recoverable issues + errors | ~5-10% | Data quality auditing |
| **SuccessAndFailures** | All conversions (success + fail) | ~10-20% | Development, validation |
| **Debug** | Every conversion attempt | ~30-50% | Deep troubleshooting |

### 7.3 Custom Sinks

```csharp
public class DatabaseSink : MaterializationSinkBase
{
    private readonly DbContext _db;
    
    public DatabaseSink(DbContext db, SinkVerbosity verbosity)
        : base(verbosity)
    {
        _db = db;
    }
    
    protected override void ReportCore(MaterializationDiagnostic diagnostic)
    {
        _db.ImportLogs.Add(new ImportLog
        {
            Timestamp = diagnostic.Timestamp,
            RowIndex = diagnostic.RowIndex,
            MemberName = diagnostic.MemberName,
            Severity = diagnostic.Severity.ToString(),
            Message = diagnostic.Message
        });
        
        if (_db.ChangeTracker.Entries().Count() > 1000)
            _db.SaveChanges(); // Batch inserts
    }
}
```

### 7.4 Filtering Diagnostics

```csharp
// Only log errors for critical fields
var sink = new MicrosoftLoggerSink(
    logger,
    SinkVerbosity.ErrorsOnly,
    filter: d => d.MemberName is "Id" or "Email");

// Exclude noisy warnings
var sink = new ConsoleSink(
    SinkVerbosity.SuccessAndFailures,
    filter: d => d.ConversionStrategy != "ChangeTypeFallback");
```

---

## 8. Performance Optimization

### 8.1 Caching Strategy

**How it works:**
- Expression trees are compiled once per `(Type, CompilationOptions)` pair
- Cached setters are reused across all rows
- Changing `CompilationOptions` (culture, formats) invalidates cache

**Best practices:**

```csharp
// ✅ GOOD: Reuse options across batches
var options = MaterializationOptions.ForCsvImport(logger);
foreach (var batch in batches)
{
    var people = ObjectMaterializer.CreateBatch<Person>(schema, batch, options);
}

// ❌ BAD: Create new options per batch (cache misses)
foreach (var batch in batches)
{
    var options = new MaterializationOptions { Culture = CultureInfo.InvariantCulture };
    var people = ObjectMaterializer.CreateBatch<Person>(schema, batch, options);
}
```

### 8.2 Parallel Processing

**When to use:**
- CPU-bound conversions (parsing, validation)
- Large datasets (>100K rows)
- Thread-safe sinks available

**Benchmark results:**

| Rows | Sequential | Parallel (8 cores) | Speedup |
|------|------------|-------------------|---------|
| 10K | 50ms | 20ms | 2.5x |
| 100K | 480ms | 150ms | 3.2x |
| 1M | 4.8s | 1.5s | 3.2x |

```csharp
// Sequential (default)
var people = ObjectMaterializer.CreateBatch<Person>(schema, rows, options);

// Parallel (use all cores)
var people = ObjectMaterializer.CreateBatchParallel<Person>(
    schema, 
    rows.ToList(), // Must be IReadOnlyList
    options,
    degreeOfParallelism: Environment.ProcessorCount);
```

### 8.3 Memory Efficiency

**Lazy evaluation with CreateBatch:**

```csharp
// ✅ GOOD: Lazy evaluation - rows processed one at a time
var people = ObjectMaterializer.CreateBatch<Person>(schema, rows, options);
foreach (var person in people)
{
    ProcessPerson(person); // Only current row in memory
}

// ⚠️ CAREFUL: Forcing evaluation loads all rows into memory
var peopleList = ObjectMaterializer.CreateBatch<Person>(schema, rows, options).ToList();
// If rows is large, this defeats lazy evaluation
```

**Streaming for large files:**

```csharp
// ✅ BEST: Async streaming for files larger than available memory
await foreach (var person in ObjectMaterializer.CreateStream<Person>(
    schema,
    ReadCsvRowsAsync("huge.csv"),
    options))
{
    await ProcessPersonAsync(person);
}

```

### 8.4 Sink Overhead

| Sink | Verbosity | Overhead | Notes |
|------|-----------|----------|-------|
| `null` | N/A | 0% | No diagnostics |
| `ConsoleSink` | ErrorsOnly | ~2% | Console I/O is slow |
| `MicrosoftLoggerSink` | ErrorsOnly | ~3% | Depends on logger backend |
| `CollectionSink` | WarningsAndErrors | ~5% | In-memory collection |
| `CollectionSink` | Debug | ~30% | Every conversion logged |

**Recommendation:** Use `ErrorsOnly` in production, `WarningsAndErrors` for auditing.

---

## 9. Advanced Scenarios

### 9.1 Nested Objects

**Scenario:** Import data with nested properties.

```csharp
public class Order
{
    public int Id { get; set; }
    public Customer Customer { get; set; } // Nested object
}

public class Customer
{
    public string Name { get; set; }
    public string Email { get; set; }
}

// Flat schema
var schema = new[] { "Id", "Customer.Name", "Customer.Email" };
var row = new object[] { "1", "Alice", "alice@example.com" };

// Custom converter
var options = new MaterializationOptions
{
    OnError = ctx =>
    {
        if (ctx.MemberName.StartsWith("Customer."))
        {
            // Handle nested property errors
            return ErrorResolution.UseDefault;
        }
        return ErrorResolution.Throw;
    }
};

// Note: Built-in support for nested properties is not included
// You'll need to manually split and assign nested properties
```

### 9.2 Dynamic Schema

**Scenario:** Schema is determined at runtime (user-uploaded CSV).

```csharp
public List<Person> ImportCsv(Stream csvStream)
{
    using var reader = new StreamReader(csvStream);
    
    // Read header row
    var header = reader.ReadLine()?.Split(',');
    if (header == null) throw new InvalidDataException("Empty file");
    
    // Read data rows
    var rows = new List<object[]>();
    string? line;
    while ((line = reader.ReadLine()) != null)
    {
        rows.Add(line.Split(','));
    }
    
    // Materialize
    var options = MaterializationOptions.ForCsvImport(logger);
    return ObjectMaterializer.CreateBatch<Person>(header, rows, options);
}
```

### 9.3 Custom Type Converters

**Scenario:** Convert custom string formats (e.g., "1d 2h 30m" → TimeSpan).

```csharp
var options = new MaterializationOptions
{
    OnError = ctx =>
    {
        if (ctx.TargetType == typeof(TimeSpan) && ctx.AttemptedValue is string str)
        {
            // Custom parsing logic
            if (TryParseCustomTimeSpan(str, out var timeSpan))
            {
                // Store parsed value for CustomValueProvider
                ctx.AdditionalData["ParsedValue"] = timeSpan;
                return ErrorResolution.UseCustomValue;
            }
        }
        return ErrorResolution.Throw;
    },
    CustomValueProvider = ctx =>
    {
        if (ctx.AdditionalData.TryGetValue("ParsedValue", out var value))
            return value;
        return null;
    }
};

bool TryParseCustomTimeSpan(string input, out TimeSpan result)
{
    // Parse "1d 2h 30m" format
    var match = Regex.Match(input, @"(?:(\d+)d)?\s*(?:(\d+)h)?\s*(?:(\d+)m)?");
    if (match.Success)
    {
        int days = int.Parse(match.Groups[1].Value);
        int hours = int.Parse(match.Groups[2].Value);
        int minutes = int.Parse(match.Groups[3].Value);
        result = new TimeSpan(days, hours, minutes, 0);
        return true;
    }
    result = default;
    return false;
}
```

### 9.4 Conditional Validation

**Scenario:** Validate fields based on other field values.

```csharp
var options = new MaterializationOptions
{
    OnError = ctx =>
    {
        // Example: If OrderType is "Express", ShipDate is required
        if (ctx.MemberName == "ShipDate")
        {
            // Access other properties via AdditionalData (set during conversion)
            if (ctx.AdditionalData.TryGetValue("OrderType", out var orderType) &&
                orderType?.ToString() == "Express")
            {
                return ErrorResolution.Throw; // ShipDate is required for Express orders
            }
        }
        return ErrorResolution.UseDefault;
    }
};

// Note: Cross-field validation requires custom logic outside the materializer
```

---

## 10. Troubleshooting

### 10.1 Common Errors

#### Error: "Context not set"

```
InvalidOperationException: TypeConverter.Convert called without context.
This indicates a bug in the materializer.
```

**Cause:** Calling `TypeConverter.Convert` directly without `ConversionContextGuard`.

**Solution:** Always use `ObjectMaterializer.Create` or `CreateBatch`. Don't call `TypeConverter.Convert` manually.

---

#### Error: "CustomValueProvider is null"

```
InvalidOperationException: OnError callback returned UseCustomValue for member 'Age',
but CustomValueProvider is null.
```

**Cause:** `OnError` returned `ErrorResolution.UseCustomValue`, but `CustomValueProvider` is not set.

**Solution:**

```csharp
var options = new MaterializationOptions
{
    OnError = ctx => ErrorResolution.UseCustomValue,
    CustomValueProvider = ctx => ctx.MemberName switch
    {
        "Age" => 0,
        "Country" => "Unknown",
        _ => null
    }
};
```

---

#### Error: "Parallel materialization requires thread-safe sink"

```
InvalidOperationException: Parallel materialization requires a thread-safe sink.
Use CollectionSink, ConsoleSink, or LoggerSink.
```

**Cause:** Using a custom sink without `[ThreadSafe]` attribute in `CreateBatchParallel`.

**Solution:**

```csharp
// Option 1: Use built-in thread-safe sink
var options = new MaterializationOptions
{
    Sink = new CollectionSink(SinkVerbosity.ErrorsOnly)
};

// Option 2: Mark custom sink as thread-safe
[ThreadSafe]
public class MySink : MaterializationSinkBase { ... }
```

---

### 10.2 Performance Issues

#### Symptom: Slow conversions (>1ms per row)

**Diagnosis:**

```csharp
var options = MaterializationOptions.ForAuditing(out var sink);
// Enable Debug verbosity temporarily
var debugSink = new CollectionSink(SinkVerbosity.Debug);
options.Sink = debugSink;

var people = ObjectMaterializer.CreateBatch<Person>(schema, rows.Take(100), options);

// Analyze slow conversions
var slowConversions = debugSink.Diagnostics
    .Where(d => d.ConversionTimeMicroseconds > 1000) // >1ms
    .OrderByDescending(d => d.ConversionTimeMicroseconds);

foreach (var d in slowConversions)
{
    Console.WriteLine($"{d.MemberName}: {d.ConversionTimeMicroseconds}µs ({d.ConversionStrategy})");
}
```

**Common causes:**
- `AllowChangeTypeFallback = true` (100x slower than direct parsing)
- Complex `OnError` callbacks with expensive logic
- Non-thread-safe sink in parallel processing

**Solutions:**
- Disable `AllowChangeTypeFallback`
- Simplify `OnError` callbacks (avoid database queries, API calls)
- Use `CreateBatchParallel` for large datasets
- Cache expensive computations in `CustomValueProvider`

---

#### Symptom: High memory usage

**Diagnosis:**

```csharp
// Check if CollectionSink is accumulating diagnostics
var sink = new CollectionSink(SinkVerbosity.Debug);
var options = new MaterializationOptions { Sink = sink };

var people = ObjectMaterializer.CreateBatch<Person>(schema, millionRows, options);

Console.WriteLine($"Diagnostics count: {sink.Diagnostics.Count}");
// If count is huge (millions), this is the problem
```

**Solutions:**

```csharp
// Option 1: Lower verbosity
var sink = new CollectionSink(SinkVerbosity.ErrorsOnly);

// Option 2: Clear periodically
foreach (var batch in batches)
{
    var people = ObjectMaterializer.CreateBatch<Person>(schema, batch, options);
    ProcessBatch(people);
    sink.Clear(); // ✅ Release memory
}

// Option 3: Use streaming
await foreach (var person in ObjectMaterializer.CreateStream<Person>(schema, rows, options))
{
    await ProcessPersonAsync(person);
}
```

---

### 10.3 Debugging Tips

#### Enable Verbose Logging

```csharp
var options = new MaterializationOptions
{
    Sink = new ConsoleSink(SinkVerbosity.Debug),
    DefaultErrorResolution = ErrorResolution.Throw // Fail-fast
};

try
{
    var person = ObjectMaterializer.Create<Person>(schema, row, options, rowIndex: 42);
}
catch (MaterializationException ex)
{
    Console.WriteLine(ex.ToString()); // Detailed error with context
}
```

#### Inspect Diagnostics

```csharp
var sink = new CollectionSink(SinkVerbosity.SuccessAndFailures);
var options = new MaterializationOptions { Sink = sink };

var people = ObjectMaterializer.CreateBatch<Person>(schema, rows, options);

// Group by conversion strategy
var strategyStats = sink.Diagnostics
    .GroupBy(d => d.ConversionStrategy)
    .Select(g => new { Strategy = g.Key, Count = g.Count() })
    .OrderByDescending(x => x.Count);

foreach (var stat in strategyStats)
{
    Console.WriteLine($"{stat.Strategy}: {stat.Count} conversions");
}
```

#### Test Single Row

```csharp
// Isolate problematic row
var problematicRow = rows[42];
var options = MaterializationOptions.ForDevelopment();

try
{
    var person = ObjectMaterializer.Create<Person>(schema, problematicRow, options, rowIndex: 42);
}
catch (MaterializationException ex)
{
    Console.WriteLine($"Failed at column: {ex.Context.MemberName}");
    Console.WriteLine($"Value: {ex.Context.AttemptedValue}");
    Console.WriteLine($"Target type: {ex.Context.TargetType}");
}
```

---

## 11. Best Practices

### 11.1 Configuration

✅ **DO:**
- Use preset configurations (`ForCsvImport`, `ForProduction`) as starting points
- Validate options early: `options.Validate()` or `builder.Build()`
- Reuse `MaterializationOptions` instances across batches (caching)
- Set explicit `DateTimeFormats` for known formats (faster than culture defaults)

❌ **DON'T:**
- Create new options per row (cache thrashing)
- Use `AllowChangeTypeFallback = true` in production (slow)
- Set `Sink = null` in production (lose observability)
- Mix `ErrorResolution.UseCustomValue` without `CustomValueProvider`

---

### 11.2 Error Handling

✅ **DO:**
- Use `ErrorResolution.Throw` during development (fail-fast)
- Use `ErrorResolution.UseDefault` in production (graceful degradation)
- Use `OnError` for per-property logic (critical vs optional fields)
- Log all errors/warnings to a sink for auditing

❌ **DON'T:**
- Swallow errors silently (`OnError = _ => ErrorResolution.SkipProperty` everywhere)
- Use `SkipRow` for non-critical fields (loses too much data)
- Perform expensive operations in `OnError` callbacks (database queries)
- Return different resolutions for same member across rows (inconsistent behavior)

---

### 11.3 Performance

✅ **DO:**
- Use `CreateBatchParallel` for large datasets (>100K rows)
- Use `CreateStream` for files larger than available memory
- Set `SinkVerbosity.ErrorsOnly` in production
- Profile with `SinkVerbosity.Debug` to identify bottlenecks

❌ **DON'T:**
- Use `CreateBatchParallel` with non-thread-safe sinks
- Use `SinkVerbosity.Debug` in production (30-50% overhead)
- Load entire large files into memory before processing
- Create new `MaterializationOptions` per row

---

### 11.4 Diagnostics

✅ **DO:**
- Use `CollectionSink` for auditing (review errors after import)
- Use `CompositeSink` to log to multiple destinations
- Filter diagnostics by `MemberName` for critical fields
- Clear `CollectionSink` periodically in long-running processes

❌ **DON'T:**
- Use `CollectionSink` with `Debug` verbosity for millions of rows (memory leak)
- Forget to dispose sinks that implement `IDisposable`
- Use custom sinks without thread-safety in parallel scenarios

---

### 11.5 Schema Design

✅ **DO:**
- Use exact property names in schema (case-insensitive matching is slower)
- Order schema columns to match property declaration order (cache-friendly)
- Use `[Order]` attributes for positional mapping (faster than schema lookup)
- Handle null/empty values explicitly via `NullStringBehavior`

❌ **DON'T:**
- Rely on schema order for positional mapping (use `[Order]` instead)
- Use special characters in property names (complicates schema matching)
- Mix schema-based and order-based approaches in same type

---

### 11.6 Testing

✅ **DO:**
- Test with invalid data (null, empty, malformed)
- Test with edge cases (min/max values, special characters)
- Test error handling paths (`OnError`, `CustomValueProvider`)
- Benchmark performance with realistic data volumes

❌ **DON'T:**
- Test only happy paths (valid data)
- Assume defaults are correct (test explicit configurations)
- Skip performance testing for large datasets

---

## 12. Quick Reference

### 12.1 API Cheat Sheet

```csharp
// Single object
var person = ObjectMaterializer.Create<Person>(schema, row, options, rowIndex);

// Batch (sequential, lazy evaluation)
var people = ObjectMaterializer.CreateBatch<Person>(schema, rows, options);
// ⚠️ Returns IEnumerable<T> - deferred execution
// Force evaluation: people.ToList() or people.ToArray()

// Batch (parallel, eager evaluation)
var people = ObjectMaterializer.CreateBatchParallel<Person>(schema, rows, options);
// ✅ Returns IReadOnlyList<T> - all rows processed immediately

// Stream (async, lazy evaluation)
await foreach (var person in ObjectMaterializer.CreateStream<Person>(schema, rows, options))
{
    // Process one at a time
}
```

### 12.2 Options Presets

```csharp
MaterializationOptions.ForProduction(logger)      // Minimal overhead, log errors
MaterializationOptions.ForDevelopment()           // Verbose, fail-fast
MaterializationOptions.ForValidation()            // Strict, reject invalid
MaterializationOptions.ForAuditing(out var sink)  // Collect warnings/errors
MaterializationOptions.ForCsvImport(logger)       // Lenient, trim whitespace
MaterializationOptions.ForExcelImport(logger)     // Strict dates
MaterializationOptions.ForApiDeserialization()    // Strict, fail-fast
```

### 12.3 Error Resolution Quick Guide

| Use Case | Strategy |
|----------|----------|
| Development | `Throw` |
| Production import | `UseDefault` |
| Critical field (ID, email) | `SkipRow` (via `OnError`) |
| Optional field (phone) | `SkipProperty` |
| Placeholder value | `UseCustomValue` |
| Strict validation | `Throw` |

### 12.4 Sink Selection

| Scenario | Sink | Verbosity |
|----------|------|-----------|
| Production monitoring | `MicrosoftLoggerSink` | `ErrorsOnly` |
| Development | `ConsoleSink` | `SuccessAndFailures` |
| Data quality audit | `CollectionSink` | `WarningsAndErrors` |
| Multiple outputs | `CompositeSink` | Varies |
| No diagnostics | `null` | N/A |

---
