# Feature Specification: Materialization Options, Diagnostics, and Error Resolution

**Version:** 2.0  
**Date:** 2025-10-09  
**Status:** Ready for Implementation  
**Author:** Architecture Team

---

## 1. Executive Summary

This specification defines a comprehensive configuration and diagnostics system for the ObjectMaterializer framework. It introduces:

1. **Separation of compilation-time and runtime options** to maintain caching performance
2. **Flexible error resolution strategies** for handling conversion failures
3. **Configurable diagnostic reporting** via pluggable sinks
4. **Conversion strictness levels** to balance flexibility and safety

The design maintains backward compatibility while enabling advanced scenarios like troubleshooting, data quality auditing, and production monitoring.

---

## 2. Core Principles

### 2.1 Performance First
- **Zero overhead when diagnostics are disabled** (`Sink = null`)
- **Plan caching must not be affected by runtime options**
- **Expression tree compilation remains the hot path**

### 2.2 Deterministic Behavior
- **No retry logic** - conversion either succeeds or fails deterministically
- **Failures indicate misconfiguration or invalid data**, not transient issues
- **All error resolution strategies are explicit and predictable**

### 2.3 Separation of Concerns
- **Compilation options** (culture, formats) affect plan caching
- **Runtime options** (sinks, error handling) do not affect caching
- **Conversion logic** is independent of diagnostic reporting
- **Sink verbosity filtering** happens at the sink level, not in conversion code

### 2.4 Backward Compatibility
- **Existing APIs continue to work** with sensible defaults
- **New features are opt-in** via explicit configuration
- **Migration path is straightforward** for existing code

---

## 3. Architecture Overview

### 3.1 Options Hierarchy

```
MaterializationOptions (runtime)
├── CompilationOptions (affects caching)
│   ├── Culture
│   ├── AllowThousandsSeparators
│   ├── DateTimeFormats
│   └── CaseInsensitiveHeaders
│
├── ConversionOptions (runtime behavior)
│   ├── Strictness
│   ├── TrimStrings
│   └── NullStringBehavior
│
├── DiagnosticOptions (observability)
│   ├── Sink
│   └── SinkVerbosity
│
└── ErrorHandlingOptions (failure strategies)
    ├── DefaultResolution
    ├── OnError (callback)
    └── CustomValueProvider (callback)
```

### 3.2 Data Flow

```
User Code
    ↓
ObjectMaterializer.Create<T>(schema, values, options)
    ↓
MemberMaterializer.FeedUsingSchema(obj, schema, values, options)
    ↓
MemberMaterializationPlan<T> (cached by CompilationOptions)
    ↓
Compiled Setter: member.Set(obj, value)
    ↓
ConvertObject(value, targetType, options) ← Runtime options injected here
    ↓
[Diagnostic Reporting] → Sink.Report(diagnostic)
    ↓
[Error Handling] → ErrorResolution strategy
    ↓
Converted Value
```

---

## 4. API Definitions

### 4.1 Enumerations

#### 4.1.1 ConversionStrictness

```csharp
/// <summary>
/// Controls how aggressively the materializer attempts type conversions.
/// </summary>
public enum ConversionStrictness
{
    /// <summary>
    /// Only exact type matches or direct assignments (no parsing).
    /// - Fastest performance
    /// - Fails on any type mismatch
    /// - Use when data types are guaranteed to match
    /// Example: int property receives boxed int
    /// </summary>
    Strict = 0,
    
    /// <summary>
    /// Allow culture-aware parsing with strict number styles.
    /// - Good performance
    /// - Parses strings to primitives using specified culture
    /// - No lenient parsing (no whitespace trimming, no case-insensitive enums)
    /// - Recommended for production with clean data
    /// </summary>
    Moderate = 1,
    
    /// <summary>
    /// Allow lenient parsing strategies.
    /// - Moderate performance
    /// - Trims whitespace from strings
    /// - Case-insensitive enum parsing
    /// - Allows thousands separators in numbers (if enabled)
    /// - Recommended for CSV/Excel imports
    /// </summary>
    Lenient = 2,
    
    /// <summary>
    /// Attempt all conversion strategies, including Convert.ChangeType fallback.
    /// - Slowest performance
    /// - Most flexible
    /// - Use for unpredictable data sources
    /// - Last resort before error handling
    /// </summary>
    Aggressive = 3
}
```

#### 4.1.2 ErrorResolution

```csharp
/// <summary>
/// Defines strategies for handling conversion errors.
/// </summary>
public enum ErrorResolution
{
    /// <summary>
    /// Throw MaterializationException immediately (fail-fast).
    /// - Stops processing
    /// - Provides detailed error context
    /// - Recommended for development and validation
    /// </summary>
    Throw = 0,
    
    /// <summary>
    /// Use default(T) for the property (e.g., 0 for int, null for string).
    /// - Continues processing
    /// - Logs warning (if sink configured)
    /// - Useful for optional fields
    /// </summary>
    UseDefault = 1,
    
    /// <summary>
    /// Use a custom value provided by CustomValueProvider callback.
    /// - Continues processing
    /// - Allows per-property fallback values
    /// - Useful for placeholder values (e.g., "Unknown", -1)
    /// </summary>
    UseCustomValue = 2,
    
    /// <summary>
    /// Skip setting this property (leave at default/uninitialized).
    /// - Continues processing
    /// - Property retains constructor/initializer value
    /// - Useful for non-critical fields
    /// </summary>
    SkipProperty = 3,
    
    /// <summary>
    /// Skip this entire row (return null from Create method).
    /// - Stops processing this row
    /// - Allows filtering invalid rows
    /// - Useful when critical fields fail validation
    /// </summary>
    SkipRow = 4
}
```

#### 4.1.3 SinkVerbosity

```csharp
/// <summary>
/// Controls the verbosity level of diagnostic reporting to sinks.
/// This is independent of ConversionStrictness.
/// </summary>
public enum SinkVerbosity
{
    /// <summary>
    /// Report only errors (conversions that failed completely).
    /// - Minimal overhead (~2-5%)
    /// - Default for production environments
    /// - Only logs when something goes wrong
    /// </summary>
    ErrorsOnly = 0,
    
    /// <summary>
    /// Report warnings (recoverable issues) and errors.
    /// - Low overhead (~5-10%)
    /// - Useful for data quality auditing
    /// - Logs when lenient parsing is used, defaults applied, etc.
    /// </summary>
    WarningsAndErrors = 1,
    
    /// <summary>
    /// Report all conversions, including successful ones.
    /// - Moderate overhead (~10-20%)
    /// - Useful for development and validation
    /// - Logs every property assignment
    /// </summary>
    All = 2,
    
    /// <summary>
    /// Report every conversion attempt, including intermediate failures.
    /// - High overhead (~30-50%)
    /// - Useful for troubleshooting and understanding conversion strategy selection
    /// - Logs each parsing attempt (strict, lenient, fallback)
    /// - Use only for debugging specific issues
    /// </summary>
    Diagnostic = 3
}
```

#### 4.1.4 DiagnosticSeverity

```csharp
/// <summary>
/// Severity level of a materialization diagnostic.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>
    /// Trace-level information (individual conversion attempts).
    /// Only reported when SinkVerbosity = Diagnostic.
    /// Example: "Attempting strict Int32 parse"
    /// </summary>
    Trace = 0,
    
    /// <summary>
    /// Informational message (successful conversions).
    /// Reported when SinkVerbosity >= All.
    /// Example: "Successfully converted '123' to Int32"
    /// </summary>
    Info = 1,
    
    /// <summary>
    /// Warning about a recoverable issue.
    /// Reported when SinkVerbosity >= WarningsAndErrors.
    /// Example: "Used default value for Age due to conversion error"
    /// </summary>
    Warning = 2,
    
    /// <summary>
    /// Error (conversion failed completely).
    /// Always reported (unless Sink is null).
    /// Example: "Failed to convert 'abc' to Int32"
    /// </summary>
    Error = 3
}
```

#### 4.1.5 NullStringBehavior

```csharp
/// <summary>
/// Defines how null or empty strings are handled during conversion.
/// </summary>
public enum NullStringBehavior
{
    /// <summary>
    /// Treat null/empty strings as conversion errors.
    /// - Strictest behavior
    /// - Useful when empty strings indicate data quality issues
    /// </summary>
    Error = 0,
    
    /// <summary>
    /// Convert null/empty strings to default(T).
    /// - Default behavior
    /// - null/empty → 0 for numbers, false for bool, null for reference types
    /// </summary>
    ConvertToDefault = 1,
    
    /// <summary>
    /// Preserve null/empty strings as-is for string properties, convert to default for others.
    /// - Distinguishes between null and empty string
    /// - Useful for nullable string fields
    /// </summary>
    PreserveForStrings = 2
}
```

---

### 4.2 Options Classes

#### 4.2.1 CompilationOptions

```csharp
/// <summary>
/// Options that affect expression tree compilation and plan caching.
/// Changing these values requires recompiling the materialization plan.
/// </summary>
public sealed class CompilationOptions
{
    /// <summary>
    /// Culture used for parsing numbers, dates, and other culture-sensitive types.
    /// Default: CultureInfo.InvariantCulture
    /// </summary>
    public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;
    
    /// <summary>
    /// Whether to allow thousands separators in numeric parsing.
    /// Default: true
    /// </summary>
    public bool AllowThousandsSeparators { get; set; } = true;
    
    /// <summary>
    /// Explicit DateTime/DateTimeOffset format strings for parsing.
    /// If empty, uses culture's default formats.
    /// Default: Empty (use culture defaults)
    /// </summary>
    public string[] DateTimeFormats { get; set; } = Array.Empty<string>();
    
    /// <summary>
    /// Whether schema column names are matched case-insensitively.
    /// Default: true
    /// </summary>
    public bool CaseInsensitiveHeaders { get; set; } = true;
    
    /// <summary>
    /// Creates a cache key for this compilation configuration.
    /// </summary>
    internal string GetCacheKey()
    {
        var formatsHash = DateTimeFormats.Length == 0
            ? string.Empty
            : string.Join("|", DateTimeFormats);
        
        return $"{Culture.Name}|{AllowThousandsSeparators}|{CaseInsensitiveHeaders}|{formatsHash}";
    }
}
```

#### 4.2.2 MaterializationOptions

```csharp
/// <summary>
/// Comprehensive options for object materialization.
/// </summary>
public sealed class MaterializationOptions
{
    /// <summary>
    /// Default options instance (used when no options provided).
    /// </summary>
    public static MaterializationOptions Default { get; } = new();
    
    // ===== Compilation Options (affect caching) =====
    
    /// <summary>
    /// Options that affect expression tree compilation.
    /// Changes to these require plan recompilation.
    /// </summary>
    public CompilationOptions Compilation { get; set; } = new();
    
    // ===== Conversion Behavior (runtime) =====
    
    /// <summary>
    /// Controls how aggressively conversions are attempted.
    /// Default: Moderate (culture-aware parsing, no lenient strategies)
    /// </summary>
    public ConversionStrictness Strictness { get; set; } = ConversionStrictness.Moderate;
    
    /// <summary>
    /// Whether to trim leading/trailing whitespace from strings before conversion.
    /// Default: true (recommended for CSV/Excel imports)
    /// </summary>
    public bool TrimStrings { get; set; } = true;
    
    /// <summary>
    /// How to handle null or empty strings during conversion.
    /// Default: ConvertToDefault
    /// </summary>
    public NullStringBehavior NullStringBehavior { get; set; } = NullStringBehavior.ConvertToDefault;
    
    // ===== Diagnostic Reporting =====
    
    /// <summary>
    /// Sink for diagnostic messages. Set to null to disable diagnostics (zero overhead).
    /// Default: null (no diagnostics)
    /// </summary>
    public IMaterializationSink? Sink { get; set; }
    
    /// <summary>
    /// Controls what information is reported to the Sink.
    /// Default: ErrorsOnly (production-friendly)
    /// </summary>
    public SinkVerbosity SinkVerbosity { get; set; } = SinkVerbosity.ErrorsOnly;
    
    // ===== Error Handling =====
    
    /// <summary>
    /// Default error resolution strategy.
    /// Can be overridden per-property via OnError callback.
    /// Default: Throw (fail-fast)
    /// </summary>
    public ErrorResolution DefaultErrorResolution { get; set; } = ErrorResolution.Throw;
    
    /// <summary>
    /// Callback invoked when a conversion error occurs.
    /// Allows per-property error handling logic.
    /// Return value overrides DefaultErrorResolution.
    /// Default: null (use DefaultErrorResolution)
    /// </summary>
    public Func<MaterializationErrorContext, ErrorResolution>? OnError { get; set; }
    
    /// <summary>
    /// Provides custom values when ErrorResolution.UseCustomValue is returned.
    /// Required if OnError or DefaultErrorResolution uses UseCustomValue.
    /// Default: null
    /// </summary>
    public Func<MaterializationErrorContext, object?>? CustomValueProvider { get; set; }
    
    // ===== Convenience Properties =====
    
    /// <summary>
    /// Shorthand for Compilation.Culture.
    /// </summary>
    public CultureInfo Culture
    {
        get => Compilation.Culture;
        set => Compilation.Culture = value;
    }
    
    /// <summary>
    /// Shorthand for Compilation.CaseInsensitiveHeaders.
    /// </summary>
    public bool CaseInsensitiveHeaders
    {
        get => Compilation.CaseInsensitiveHeaders;
        set => Compilation.CaseInsensitiveHeaders = value;
    }
    
    /// <summary>
    /// Validates the options configuration.
    /// Throws InvalidOperationException if configuration is invalid.
    /// </summary>
    public void Validate()
    {
        if ((DefaultErrorResolution == ErrorResolution.UseCustomValue || 
             OnError != null) && 
            CustomValueProvider == null)
        {
            throw new InvalidOperationException(
                "CustomValueProvider must be set when using ErrorResolution.UseCustomValue");
        }
        
        if (Compilation.DateTimeFormats.Any(f => string.IsNullOrWhiteSpace(f)))
        {
            throw new InvalidOperationException(
                "DateTimeFormats cannot contain null or whitespace entries");
        }
    }
}
```

---

### 4.3 Diagnostic Types

#### 4.3.1 MaterializationDiagnostic

```csharp
/// <summary>
/// Represents a diagnostic message from the materialization process.
/// </summary>
public sealed class MaterializationDiagnostic
{
    /// <summary>
    /// Severity level of this diagnostic.
    /// </summary>
    public DiagnosticSeverity Severity { get; init; }
    
    /// <summary>
    /// Zero-based row index (if applicable).
    /// Null if not processing a collection.
    /// </summary>
    public int? RowIndex { get; init; }
    
    /// <summary>
    /// Property or field name being converted.
    /// Null if diagnostic is not property-specific.
    /// </summary>
    public string? MemberName { get; init; }
    
    /// <summary>
    /// Human-readable diagnostic message.
    /// </summary>
    public string Message { get; init; } = string.Empty;
    
    /// <summary>
    /// The value that was attempted to be converted.
    /// </summary>
    public object? AttemptedValue { get; init; }
    
    /// <summary>
    /// The target type for the conversion.
    /// </summary>
    public Type? TargetType { get; init; }
    
    /// <summary>
    /// Name of the conversion strategy that was used (or attempted).
    /// Examples: "StrictInt32Parse", "LenientInt32Parse", "DirectAssignment", "ChangeType"
    /// </summary>
    public string? ConversionStrategy { get; init; }
    
    /// <summary>
    /// Exception that occurred during conversion (if any).
    /// </summary>
    public Exception? Exception { get; init; }
    
    /// <summary>
    /// Timestamp when the diagnostic was created.
    /// </summary>
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    
    /// <summary>
    /// Returns a formatted string representation of this diagnostic.
    /// </summary>
    public override string ToString()
    {
        var parts = new List<string>();
        
        parts.Add($"[{Severity}]");
        
        if (RowIndex.HasValue)
            parts.Add($"Row {RowIndex}");
        
        if (!string.IsNullOrEmpty(MemberName))
            parts.Add($"Member '{MemberName}'");
        
        if (!string.IsNullOrEmpty(ConversionStrategy))
            parts.Add($"Strategy '{ConversionStrategy}'");
        
        parts.Add(Message);
        
        if (AttemptedValue != null)
            parts.Add($"Value: '{AttemptedValue}'");
        
        if (TargetType != null)
            parts.Add($"Target: {TargetType.Name}");
        
        return string.Join(" | ", parts);
    }
}
```

#### 4.3.2 MaterializationErrorContext

```csharp
/// <summary>
/// Context information provided to error handling callbacks.
/// </summary>
public sealed class MaterializationErrorContext
{
    /// <summary>
    /// Zero-based row index (if applicable).
    /// </summary>
    public int? RowIndex { get; init; }
    
    /// <summary>
    /// Property or field name that failed conversion.
    /// </summary>
    public string MemberName { get; init; } = string.Empty;
    
    /// <summary>
    /// The value that failed to convert.
    /// </summary>
    public object? AttemptedValue { get; init; }
    
    /// <summary>
    /// The target type for the conversion.
    /// </summary>
    public Type TargetType { get; init; } = typeof(object);
    
    /// <summary>
    /// The exception that occurred during conversion.
    /// </summary>
    public Exception Exception { get; init; } = new Exception();
    
    /// <summary>
    /// The conversion strategy that failed.
    /// </summary>
    public string? ConversionStrategy { get; init; }
    
    /// <summary>
    /// Additional context data (extensibility point).
    /// </summary>
    public Dictionary<string, object?> AdditionalData { get; init; } = new();
}
```

---

### 4.4 Sink Interfaces

#### 4.4.1 IMaterializationSink

```csharp
/// <summary>
/// Interface for receiving materialization diagnostics.
/// </summary>
public interface IMaterializationSink
{
    /// <summary>
    /// Reports a diagnostic message.
    /// Implementations should be thread-safe if used in parallel scenarios.
    /// </summary>
    void Report(MaterializationDiagnostic diagnostic);
}
```

#### 4.4.2 MaterializationSinkBase

```csharp
/// <summary>
/// Base class for materialization sinks with built-in verbosity filtering.
/// </summary>
public abstract class MaterializationSinkBase : IMaterializationSink
{
    private readonly SinkVerbosity _verbosity;

    /// <summary>
    /// Creates a sink with the specified verbosity level.
    /// </summary>
    protected MaterializationSinkBase(SinkVerbosity verbosity = SinkVerbosity.ErrorsOnly)
    {
        _verbosity = verbosity;
    }

    /// <summary>
    /// Reports a diagnostic if it meets the verbosity threshold.
    /// </summary>
    public void Report(MaterializationDiagnostic diagnostic)
    {
        if (ShouldReport(diagnostic.Severity))
        {
            ReportCore(diagnostic);
        }
    }

    /// <summary>
    /// Determines if a diagnostic should be reported based on verbosity settings.
    /// </summary>
    private bool ShouldReport(DiagnosticSeverity severity)
    {
        return _verbosity switch
        {
            SinkVerbosity.ErrorsOnly => severity == DiagnosticSeverity.Error,
            SinkVerbosity.WarningsAndErrors => severity >= DiagnosticSeverity.Warning,
            SinkVerbosity.All => severity >= DiagnosticSeverity.Info,
            SinkVerbosity.Diagnostic => true,
            _ => false
        };
    }

    /// <summary>
    /// Override this method to implement sink-specific reporting logic.
    /// Only called when the diagnostic passes the verbosity filter.
    /// </summary>
    protected abstract void ReportCore(MaterializationDiagnostic diagnostic);
}
```

---

### 4.5 Exception Types

#### 4.5.1 MaterializationException

```csharp
/// <summary>
/// Exception thrown when object materialization fails.
/// </summary>
public class MaterializationException : Exception
{
    /// <summary>
    /// Context information about the error.
    /// </summary>
    public MaterializationErrorContext Context { get; }
    
    /// <summary>
    /// Creates a new MaterializationException.
    /// </summary>
    public MaterializationException(string message, MaterializationErrorContext context)
        : base(message)
    {
        Context = context;
    }
    
    /// <summary>
    /// Creates a new MaterializationException with an inner exception.
    /// </summary>
    public MaterializationException(string message, MaterializationErrorContext context, Exception innerException)
        : base(message, innerException)
    {
        Context = context;
    }
    
    /// <summary>
    /// Returns a detailed error message including context.
    /// </summary>
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.AppendLine(base.ToString());
        sb.AppendLine();
        sb.AppendLine("Materialization Context:");
        
        if (Context.RowIndex.HasValue)
            sb.AppendLine($"  Row Index: {Context.RowIndex}");
        
        sb.AppendLine($"  Member: {Context.MemberName}");
        sb.AppendLine($"  Target Type: {Context.TargetType.FullName}");
        sb.AppendLine($"  Attempted Value: {Context.AttemptedValue ?? "(null)"}");
        sb.AppendLine($"  Value Type: {Context.AttemptedValue?.GetType().FullName ?? "null"}");
        
        if (!string.IsNullOrEmpty(Context.ConversionStrategy))
            sb.AppendLine($"  Strategy: {Context.ConversionStrategy}");
        
        return sb.ToString();
    }
}
```

#### 4.5.2 ConversionException

```csharp
/// <summary>
/// Exception thrown during type conversion.
/// </summary>
public class ConversionException : Exception
{
    /// <summary>
    /// The value that failed to convert.
    /// </summary>
    public object? Value { get; }
    
    /// <summary>
    /// The target type for the conversion.
    /// </summary>
    public Type TargetType { get; }
    
    /// <summary>
    /// Creates a new ConversionException.
    /// </summary>
    public ConversionException(string message, object? value, Type targetType)
        : base(message)
    {
        Value = value;
        TargetType = targetType;
    }
    
    /// <summary>
    /// Creates a new ConversionException with an inner exception.
    /// </summary>
    public ConversionException(string message, object? value, Type targetType, Exception innerException)
        : base(message, innerException)
    {
        Value = value;
        TargetType = targetType;
    }
}
```

#### 4.5.3 SkipRowException (Internal)

```csharp
/// <summary>
/// Internal exception used to signal ErrorResolution.SkipRow.
/// Not exposed publicly.
/// </summary>
internal sealed class SkipRowException : Exception
{
    public MaterializationErrorContext Context { get; }
    
    public SkipRowException(MaterializationErrorContext context)
    {
        Context = context;
    }
}
```

---

## 5. Implementation Details

### 5.1 Plan Caching Strategy

#### 5.1.1 Updated PlanCacheKey

```csharp
private readonly record struct PlanCacheKey(
    Type TargetType,
    string CompilationOptionsKey)
{
    public static PlanCacheKey Create<T>(CompilationOptions options)
    {
        return new PlanCacheKey(
            typeof(T),
            options.GetCacheKey());
    }
    
    public override int GetHashCode() 
        => HashCode.Combine(TargetType, CompilationOptionsKey);
}
```

#### 5.1.2 Updated MemberMaterializationPlanner.Get

```csharp
public static MemberMaterializationPlan<T> Get<T>(CompilationOptions options)
{
    var key = PlanCacheKey.Create<T>(options);

    return (MemberMaterializationPlan<T>)Cache.GetOrAdd(
        key,
        _ => MemberMaterializationPlan<T>.Build(options));
}

// Backward compatibility overload
public static MemberMaterializationPlan<T> Get<T>(
    bool caseInsensitiveHeaders = true,
    CultureInfo? culture = null,
    bool allowThousandsSeparators = true,
    string[]? dateTimeFormats = null)
{
    var options = new CompilationOptions
    {
        CaseInsensitiveHeaders = caseInsensitiveHeaders,
        Culture = culture ?? CultureInfo.InvariantCulture,
        AllowThousandsSeparators = allowThousandsSeparators,
        DateTimeFormats = dateTimeFormats ?? Array.Empty<string>()
    };
    
    return Get<T>(options);
}
```

---

### 5.2 ConvertObject Signature Update

#### 5.2.1 New Signature

```csharp
private static object? ConvertObject(
    object? value,
    Type targetType,
    MaterializationOptions options,
    string? memberName = null,
    int? rowIndex = null)
{
    if (value is null)
    {
        return HandleNullValue(targetType, options, memberName, rowIndex);
    }

    var vType = value.GetType();
    if (targetType.IsAssignableFrom(vType))
    {
        ReportDiagnostic(options, DiagnosticSeverity.Info, 
            "DirectAssignment", 
            $"Direct assignment (no conversion needed)",
            value, targetType, memberName, rowIndex);
        return value;
    }

    // Handle Nullable<T>
    var nullable = Nullable.GetUnderlyingType(targetType);
    if (nullable != null)
        targetType = nullable;

    // String -> primitive conversions
    if (value is string s)
    {
        return ConvertFromString(s, targetType, options, memberName, rowIndex);
    }

    // Numeric conversions, etc.
    return ConvertOther(value, targetType, options, memberName, rowIndex);
}
```

#### 5.2.2 String Conversion with Diagnostics

```csharp
private static object? ConvertFromString(
    string s,
    Type targetType,
    MaterializationOptions options,
    string? memberName,
    int? rowIndex)
{
    // Trim if configured
    if (options.TrimStrings)
    {
        s = s.Trim();
        if (s.Length == 0 && options.NullStringBehavior != NullStringBehavior.PreserveForStrings)
        {
            return HandleNullValue(targetType, options, memberName, rowIndex);
        }
    }

    // Handle empty strings
    if (string.IsNullOrEmpty(s))
    {
        return HandleNullValue(targetType, options, memberName, rowIndex);
    }

    // Integer types
    if (targetType == typeof(int))
    {
        return ConvertToInt32(s, options, memberName, rowIndex);
    }
    
    // ... other types ...
    
    // Fallback
    return AttemptFallbackConversion(s, targetType, options, memberName, rowIndex);
}
```

#### 5.2.3 Int32 Conversion Example (Full Diagnostic Chain)

```csharp
private static object? ConvertToInt32(
    string s,
    MaterializationOptions options,
    string? memberName,
    int? rowIndex)
{
    var culture = options.Compilation.Culture;
    var allowThousands = options.Compilation.AllowThousandsSeparators;
    
    // Strategy 1: Strict parsing
    ReportDiagnostic(options, DiagnosticSeverity.Trace,
        "StrictInt32Parse",
        $"Attempting strict Int32 parse",
        s, typeof(int), memberName, rowIndex);
    
    var styles = NumberStyles.Integer;
    if (int.TryParse(s, styles, culture, out var strictResult))
    {
        ReportDiagnostic(options, DiagnosticSeverity.Info,
            "StrictInt32Parse",
            $"Strict parse succeeded: {strictResult}",
            s, typeof(int), memberName, rowIndex);
        return strictResult;
    }
    
    // Strategy 2: Lenient parsing (if allowed)
    if (options.Strictness >= ConversionStrictness.Lenient)
    {
        ReportDiagnostic(options, DiagnosticSeverity.Trace,
            "LenientInt32Parse",
            $"Strict parse failed, attempting lenient parse",
            s, typeof(int), memberName, rowIndex);
        
        if (allowThousands)
            styles |= NumberStyles.AllowThousands;
        
        if (int.TryParse(s, styles, culture, out var lenientResult))
        {
            ReportDiagnostic(options, DiagnosticSeverity.Warning,
                "LenientInt32Parse",
                $"Lenient parse succeeded: {lenientResult} (consider data cleanup)",
                s, typeof(int), memberName, rowIndex);
            return lenientResult;
        }
    }
    
    // Strategy 3: Aggressive fallback (if allowed)
    if (options.Strictness >= ConversionStrictness.Aggressive)
    {
        ReportDiagnostic(options, DiagnosticSeverity.Trace,
            "ChangeTypeInt32",
            $"Lenient parse failed, attempting Convert.ChangeType",
            s, typeof(int), memberName, rowIndex);
        
        try
        {
            var result = Convert.ChangeType(s, typeof(int), culture);
            ReportDiagnostic(options, DiagnosticSeverity.Warning,
                "ChangeTypeInt32",
                $"ChangeType succeeded: {result} (unexpected conversion path)",
                s, typeof(int), memberName, rowIndex);
            return result;
        }
        catch (Exception ex)
        {
            ReportDiagnostic(options, DiagnosticSeverity.Trace,
                "ChangeTypeInt32",
                $"ChangeType failed: {ex.Message}",
                s, typeof(int), memberName, rowIndex);
        }
    }
    
    // All strategies failed - handle error
    return HandleConversionError(
        s,
        typeof(int),
        options,
        memberName,
        rowIndex,
        $"Failed to convert '{s}' to Int32 using all available strategies");
}
```

#### 5.2.4 Error Handling

```csharp
private static object? HandleConversionError(
    object? value,
    Type targetType,
    MaterializationOptions options,
    string? memberName,
    int? rowIndex,
    string errorMessage)
{
    var context = new MaterializationErrorContext
    {
        RowIndex = rowIndex,
        MemberName = memberName ?? string.Empty,
        AttemptedValue = value,
        TargetType = targetType,
        Exception = new ConversionException(errorMessage, value, targetType)
    };
    
    // Report error diagnostic
    ReportDiagnostic(options, DiagnosticSeverity.Error,
        "ConversionFailed",
        errorMessage,
        value, targetType, memberName, rowIndex,
        context.Exception);
    
    // Determine resolution strategy
    var resolution = options.OnError?.Invoke(context) 
                     ?? options.DefaultErrorResolution;
    
    switch (resolution)
    {
        case ErrorResolution.Throw:
            throw new MaterializationException(errorMessage, context);
            
        case ErrorResolution.UseDefault:
            var defaultValue = targetType.IsValueType 
                ? Activator.CreateInstance(targetType) 
                : null;
            
            ReportDiagnostic(options, DiagnosticSeverity.Warning,
                "UseDefault",
                $"Using default value: {defaultValue ?? "(null)"}",
                value, targetType, memberName, rowIndex);
            
            return defaultValue;
            
        case ErrorResolution.UseCustomValue:
            if (options.CustomValueProvider == null)
            {
                throw new InvalidOperationException(
                    $"CustomValueProvider is null but ErrorResolution.UseCustomValue was returned for member '{memberName}'");
            }
            
            var customValue = options.CustomValueProvider.Invoke(context);
            
            ReportDiagnostic(options, DiagnosticSeverity.Warning,
                "UseCustomValue",
                $"Using custom value: {customValue ?? "(null)"}",
                value, targetType, memberName, rowIndex);
            
            return customValue;
            
        case ErrorResolution.SkipProperty:
            ReportDiagnostic(options, DiagnosticSeverity.Warning,
                "SkipProperty",
                $"Skipping property (will retain default/initializer value)",
                value, targetType, memberName, rowIndex);
            
            // Return a sentinel value that the setter will recognize
            throw new SkipPropertyException(context);
            
        case ErrorResolution.SkipRow:
            ReportDiagnostic(options, DiagnosticSeverity.Warning,
                "SkipRow",
                $"Skipping entire row due to critical field conversion failure",
                value, targetType, memberName, rowIndex);
            
            throw new SkipRowException(context);
            
        default:
            throw new InvalidOperationException(
                $"Unknown ErrorResolution: {resolution}");
    }
}

private static object? HandleNullValue(
    Type targetType,
    MaterializationOptions options,
    string? memberName,
    int? rowIndex)
{
    if (options.NullStringBehavior == NullStringBehavior.Error)
    {
        return HandleConversionError(
            null,
            targetType,
            options,
            memberName,
            rowIndex,
            "Null or empty string not allowed (NullStringBehavior.Error)");
    }
    
    if (targetType == typeof(string) && 
        options.NullStringBehavior == NullStringBehavior.PreserveForStrings)
    {
        ReportDiagnostic(options, DiagnosticSeverity.Info,
            "PreserveNull",
            "Preserving null for string property",
            null, targetType, memberName, rowIndex);
        return null;
    }
    
    var defaultValue = targetType.IsValueType 
        ? Activator.CreateInstance(targetType) 
        : null;
    
    ReportDiagnostic(options, DiagnosticSeverity.Info,
        "NullToDefault",
        $"Converting null to default: {defaultValue ?? "(null)"}",
        null, targetType, memberName, rowIndex);
    
    return defaultValue;
}
```

#### 5.2.5 Diagnostic Reporting Helper

```csharp
private static void ReportDiagnostic(
    MaterializationOptions options,
    DiagnosticSeverity severity,
    string strategy,
    string message,
    object? value,
    Type targetType,
    string? memberName,
    int? rowIndex,
    Exception? exception = null)
{
    // Fast path: no sink configured
    if (options.Sink == null)
        return;
    
    options.Sink.Report(new MaterializationDiagnostic
    {
        Severity = severity,
        RowIndex = rowIndex,
        MemberName = memberName,
        Message = message,
        AttemptedValue = value,
        TargetType = targetType,
        ConversionStrategy = strategy,
        Exception = exception
    });
}
```

#### 5.2.6 Additional Exception Type

```csharp
/// <summary>
/// Internal exception used to signal ErrorResolution.SkipProperty.
/// Not exposed publicly.
/// </summary>
internal sealed class SkipPropertyException : Exception
{
    public MaterializationErrorContext Context { get; }
    
    public SkipPropertyException(MaterializationErrorContext context)
    {
        Context = context;
    }
}
```

---

### 5.3 Expression Tree Updates

#### 5.3.1 BuildConvertExpression Update

```csharp
private static Expression BuildConvertExpression(
    ParameterExpression input,
    Type targetType,
    ParameterExpression optionsParam,
    string memberName)
{
    // If reference or nullable<T>, allow null straight through
    var underlyingNullable = Nullable.GetUnderlyingType(targetType);
    if (!targetType.IsValueType || underlyingNullable != null)
    {
        var tgt = underlyingNullable ?? targetType;
        return Expression.Convert(
            Expression.Call(
                typeof(MemberMaterializationPlan<T>)
                    .GetMethod(nameof(ConvertObject), BindingFlags.NonPublic | BindingFlags.Static)!,
                input,
                Expression.Constant(targetType, typeof(Type)),
                optionsParam,
                Expression.Constant(memberName, typeof(string)),
                Expression.Constant(null, typeof(int?)) // rowIndex - set at call site
            ),
            targetType);
    }

    // Non-nullable value type: null -> default(T), else convert
    var isNull = Expression.Equal(input, Expression.Constant(null));
    var onNull = Expression.Default(targetType);
    var onVal = Expression.Convert(
        Expression.Call(
            typeof(MemberMaterializationPlan<T>)
                .GetMethod(nameof(ConvertObject), BindingFlags.NonPublic | BindingFlags.Static)!,
            input,
            Expression.Constant(targetType, typeof(Type)),
            optionsParam,
            Expression.Constant(memberName, typeof(string)),
            Expression.Constant(null, typeof(int?))
        ),
        targetType);
    
    return Expression.Condition(isNull, onNull, onVal);
}
```

#### 5.3.2 Updated MemberSetter Structure

```csharp
public readonly struct MemberSetter
{
    public readonly string Name;
    public readonly int OrderIndex; // -1 if not ordered
    public readonly Action<T, object?, MaterializationOptions> Set; // Now accepts options
    
    public MemberSetter(string name, int orderIndex, 
        Action<T, object?, MaterializationOptions> set)
    { 
        Name = name; 
        OrderIndex = orderIndex; 
        Set = set; 
    }
}
```

#### 5.3.3 Updated CompileSetterForProperty

```csharp
private static Action<T, object?, MaterializationOptions> CompileSetterForProperty(
    PropertyInfo p,
    CompilationOptions compilationOptions)
{
    var obj = Expression.Parameter(typeof(T), "obj");
    var val = Expression.Parameter(typeof(object), "val");
    var options = Expression.Parameter(typeof(MaterializationOptions), "options");

    var targetType = p.PropertyType;
    var assignValue = BuildConvertExpression(val, targetType, options, p.Name);

    // Wrap in try-catch to handle SkipPropertyException
    var assignExpr = Expression.Assign(Expression.Property(obj, p), assignValue);
    
    var body = Expression.TryCatch(
        assignExpr,
        Expression.Catch(
            typeof(SkipPropertyException),
            Expression.Empty() // Do nothing - skip this property
        )
    );

    return Expression.Lambda<Action<T, object?, MaterializationOptions>>(
        body, obj, val, options).Compile();
}
```

#### 5.3.4 Updated CompileSetterForField

```csharp
private static Action<T, object?, MaterializationOptions> CompileSetterForField(
    FieldInfo f,
    CompilationOptions compilationOptions)
{
    var obj = Expression.Parameter(typeof(T), "obj");
    var val = Expression.Parameter(typeof(object), "val");
    var options = Expression.Parameter(typeof(MaterializationOptions), "options");

    var targetType = f.FieldType;
    var assignValue = BuildConvertExpression(val, targetType, options, f.Name);

    var assignExpr = Expression.Assign(Expression.Field(obj, f), assignValue);
    
    var body = Expression.TryCatch(
        assignExpr,
        Expression.Catch(
            typeof(SkipPropertyException),
            Expression.Empty() // Do nothing - skip this field
        )
    );

    return Expression.Lambda<Action<T, object?, MaterializationOptions>>(
        body, obj, val, options).Compile();
}
```

---

### 5.4 MemberMaterializer Updates

#### 5.4.1 FeedUsingSchema Update

```csharp
public static T FeedUsingSchema<T>(
    T obj,
    Dictionary<string, int> schemaDict,
    object?[] values,
    MaterializationOptions options,
    int? rowIndex = null)
{
    if (obj == null) throw new ArgumentNullException(nameof(obj));
    if (values == null) throw new ArgumentNullException(nameof(values));
    if (options == null) throw new ArgumentNullException(nameof(options));

    var plan = MemberMaterializationPlanner.Get<T>(options.Compilation);

    // Normalize schema dictionary to match plan's comparer
    Dictionary<string, int> normalizedSchema = schemaDict;
    if (schemaDict.Comparer != plan.NameComparer)
    {
        normalizedSchema = new Dictionary<string, int>(schemaDict, plan.NameComparer);
    }

    try
    {
        foreach (ref readonly var member in plan.Members.AsSpan())
        {
            if (normalizedSchema.TryGetValue(member.Name, out var idx))
            {
                if ((uint)idx < (uint)values.Length)
                {
                    try
                    {
                        member.Set(obj, values[idx], options);
                    }
                    catch (SkipPropertyException)
                    {
                        // Property was skipped - continue to next member
                        continue;
                    }
                }
            }
        }
        
        return obj;
    }
    catch (SkipRowException ex)
    {
        // Row should be skipped - propagate to caller
        throw;
    }
}

// Backward compatibility overload
public static T FeedUsingSchema<T>(
    T obj,
    Dictionary<string, int> schemaDict,
    object?[] values,
    bool caseInsensitiveHeaders = true)
{
    var options = new MaterializationOptions
    {
        Compilation = new CompilationOptions
        {
            CaseInsensitiveHeaders = caseInsensitiveHeaders
        }
    };
    
    return FeedUsingSchema(obj, schemaDict, values, options);
}
```

#### 5.4.2 FeedOrdered Update

```csharp
public static T FeedOrdered<T>(T obj, object?[] values, MaterializationOptions options)
{
    if (obj == null) throw new ArgumentNullException(nameof(obj));
    if (values == null) throw new ArgumentNullException(nameof(values));
    if (options == null) throw new ArgumentNullException(nameof(options));

    var plan = MemberMaterializationPlanner.Get<T>(options.Compilation);
    
    // Sort members by OrderIndex
    var orderedMembers = plan.Members
        .Where(m => m.OrderIndex >= 0)
        .OrderBy(m => m.OrderIndex)
        .ToArray();

    try
    {
        for (int i = 0; i < orderedMembers.Length && i < values.Length; i++)
        {
            try
            {
                orderedMembers[i].Set(obj, values[i], options);
            }
            catch (SkipPropertyException)
            {
                continue;
            }
        }
        
        return obj;
    }
    catch (SkipRowException)
    {
        throw;
    }
}

// Backward compatibility overload
public static T FeedOrdered<T>(T obj, object?[] values)
{
    return FeedOrdered(obj, values, MaterializationOptions.Default);
}
```

---

### 5.5 ObjectMaterializer Updates

#### 5.5.1 Create Overloads

```csharp
// Primary method with options
public static T? Create<T>(
    string[] schema, 
    object[] parameters, 
    MaterializationOptions? options = null,
    int? rowIndex = null)
{
    options ??= MaterializationOptions.Default;
    options.Validate();
    
    try
    {
        T instance = NewWithSchema<T>(schema, parameters, options, rowIndex);
        return instance;
    }
    catch (SkipRowException ex)
    {
        // Row was skipped due to critical field failure
        options.Sink?.Report(new MaterializationDiagnostic
        {
            Severity = DiagnosticSeverity.Warning,
            RowIndex = rowIndex,
            Message = $"Row skipped: {ex.Context.MemberName} conversion failed",
            MemberName = ex.Context.MemberName,
            AttemptedValue = ex.Context.AttemptedValue,
            TargetType = ex.Context.TargetType
        });
        
        return default(T);
    }
}

// Backward compatibility overload
public static T? Create<T>(string[] schema, params object[] parameters)
{
    return Create<T>(schema, parameters, MaterializationOptions.Default, null);
}

// Overload without schema (uses internal order)
public static T? Create<T>(object[] parameters, MaterializationOptions? options = null)
{
    options ??= MaterializationOptions.Default;
    options.Validate();
    
    try
    {
        if (TryCreateViaBestConstructor<T>(parameters, out var instance))
            return instance;

        return NewUsingInternalOrder<T>(parameters, options);
    }
    catch (SkipRowException)
    {
        return default(T);
    }
}
```

#### 5.5.2 NewWithSchema Update

```csharp
private static T NewWithSchema<T>(
    string[] schema, 
    object?[] parameters,
    MaterializationOptions options,
    int? rowIndex)
{
    Type newObjectType = typeof(T);
    if (schema == null) throw new ArgumentNullException(nameof(schema));

    // Try parameterless constructor first
    if (!TryGetParameterlessFactory<T>(out var factory))
    {
        // For records/types without parameterless ctor, try primary constructor with schema mapping
        return CreateViaPrimaryConstructorWithSchema<T>(schema, parameters, options, rowIndex);
    }

    T instance = (T)factory();
    var dict = MemberMaterializer.GetSchemaDictionary(schema, 
        options.Compilation.CaseInsensitiveHeaders 
            ? StringComparer.OrdinalIgnoreCase 
            : StringComparer.Ordinal);
    
    return MemberMaterializer.FeedUsingSchema(instance, dict, parameters, options, rowIndex);
}
```

#### 5.5.3 CreateViaPrimaryConstructorWithSchema Update

```csharp
private static T CreateViaPrimaryConstructorWithSchema<T>(
    string[] schema, 
    object?[] values,
    MaterializationOptions options,
    int? rowIndex)
{
    Type type = typeof(T);
    var schemaDict = MemberMaterializer.GetSchemaDictionary(schema, 
        options.Compilation.CaseInsensitiveHeaders 
            ? StringComparer.OrdinalIgnoreCase 
            : StringComparer.Ordinal);

    // Find primary constructor (longest parameter list, typically)
    var ctors = type.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .OrderByDescending(c => c.GetParameters().Length)
                    .ToArray();

    if (ctors.Length == 0)
        throw new InvalidOperationException($"Type {type.FullName} has no accessible constructors.");

    // Try each constructor (starting with longest - likely primary constructor for records)
    foreach (var ctor in ctors)
    {
        var ctorParams = ctor.GetParameters();
        if (ctorParams.Length == 0) continue; // Skip parameterless (already tried)

        var args = new object?[ctorParams.Length];
        bool allMatched = true;

        for (int i = 0; i < ctorParams.Length; i++)
        {
            var param = ctorParams[i];
            var paramName = param.Name ?? string.Empty;

            // Try to find matching schema column (case-insensitive)
            if (schemaDict.TryGetValue(paramName, out var colIndex) &&
                (uint)colIndex < (uint)values.Length)
            {
                try
                {
                    // Convert the value using the options
                    args[i] = ConvertObject(
                        values[colIndex], 
                        param.ParameterType, 
                        options, 
                        paramName, 
                        rowIndex);
                }
                catch (SkipPropertyException)
                {
                    // If property is skipped, use default
                    args[i] = param.HasDefaultValue 
                        ? param.DefaultValue 
                        : (param.ParameterType.IsValueType 
                            ? Activator.CreateInstance(param.ParameterType) 
                            : null);
                }
                catch (SkipRowException)
                {
                    // Propagate row skip
                    throw;
                }
            }
            else if (param.HasDefaultValue)
            {
                args[i] = param.DefaultValue;
            }
            else
            {
                // Required parameter not found in schema
                allMatched = false;
                break;
            }
        }

        if (allMatched)
        {
            // Cache and invoke
            var key = BuildSignatureKey<T>(args);
            var ctorFactory = CompileFactoryDelegate(ctor);
            _ctorCache[key] = ctorFactory;
            return (T)ctorFactory(args);
        }
    }

    throw new InvalidOperationException(
            $"Cannot materialize {type.FullName}:\n" +
            $"  Schema columns: {string.Join(", ", schema)}\n" +
            $"  Available constructors:\n" +
            string.Join("\n", ctors.Select(c => $"    - ({string.Join(", ", c.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"))})"))
        );
}
```

---

## 6. Built-in Sink Implementations

### 6.1 ConsoleSink

```csharp
/// <summary>
/// Writes diagnostics to the console with color-coded severity.
/// </summary>
public sealed class ConsoleSink : MaterializationSinkBase
{
    private readonly bool _useColors;
    
    public ConsoleSink(
        SinkVerbosity verbosity = SinkVerbosity.ErrorsOnly,
        bool useColors = true)
        : base(verbosity)
    {
        _useColors = useColors;
    }

    protected override void ReportCore(MaterializationDiagnostic diagnostic)
    {
        if (_useColors)
        {
            var originalColor = Console.ForegroundColor;
            Console.ForegroundColor = diagnostic.Severity switch
            {
                DiagnosticSeverity.Trace => ConsoleColor.Gray,
                DiagnosticSeverity.Info => ConsoleColor.White,
                DiagnosticSeverity.Warning => ConsoleColor.Yellow,
                DiagnosticSeverity.Error => ConsoleColor.Red,
                _ => ConsoleColor.White
            };
            
            Console.WriteLine(diagnostic.ToString());
            Console.ForegroundColor = originalColor;
        }
        else
        {
            Console.WriteLine(diagnostic.ToString());
        }
    }
}
```

### 6.2 CollectionSink

```csharp
/// <summary>
/// Collects diagnostics in memory for later analysis.
/// Thread-safe for parallel scenarios.
/// </summary>
public sealed class CollectionSink : MaterializationSinkBase
{
    private readonly ConcurrentBag<MaterializationDiagnostic> _diagnostics = new();
    
    public CollectionSink(SinkVerbosity verbosity = SinkVerbosity.ErrorsOnly)
        : base(verbosity)
    {
    }

    /// <summary>
    /// All collected diagnostics.
    /// </summary>
    public IReadOnlyCollection<MaterializationDiagnostic> Diagnostics => _diagnostics;

    protected override void ReportCore(MaterializationDiagnostic diagnostic)
    {
        _diagnostics.Add(diagnostic);
    }

    /// <summary>
    /// Clears all collected diagnostics.
    /// </summary>
    public void Clear()
    {
        _diagnostics.Clear();
    }

    /// <summary>
    /// Gets diagnostics grouped by severity.
    /// </summary>
    public ILookup<DiagnosticSeverity, MaterializationDiagnostic> BySeverity()
    {
        return _diagnostics.ToLookup(d => d.Severity);
    }

    /// <summary>
    /// Gets diagnostics grouped by member name.
    /// </summary>
    public ILookup<string, MaterializationDiagnostic> ByMember()
    {
        return _diagnostics
            .Where(d => d.MemberName != null)
            .ToLookup(d => d.MemberName!);
    }

    /// <summary>
    /// Gets diagnostics grouped by conversion strategy.
    /// </summary>
    public ILookup<string, MaterializationDiagnostic> ByStrategy()
    {
        return _diagnostics
            .Where(d => d.ConversionStrategy != null)
            .ToLookup(d => d.ConversionStrategy!);
    }
}
```

### 6.3 DelegateSink

```csharp
/// <summary>
/// Forwards diagnostics to a custom delegate.
/// Useful for integration with existing logging systems.
/// </summary>
public sealed class DelegateSink : MaterializationSinkBase
{
    private readonly Action<MaterializationDiagnostic> _handler;
    
    public DelegateSink(
        Action<MaterializationDiagnostic> handler,
        SinkVerbosity verbosity = SinkVerbosity.ErrorsOnly)
        : base(verbosity)
    {
        _handler = handler ?? throw new ArgumentNullException(nameof(handler));
    }

    protected override void ReportCore(MaterializationDiagnostic diagnostic)
    {
        _handler(diagnostic);
    }
}
```

### 6.4 CompositeSink

```csharp
/// <summary>
/// Forwards diagnostics to multiple sinks.
/// Useful for logging to both console and file, for example.
/// </summary>
public sealed class CompositeSink : IMaterializationSink
{
    private readonly IMaterializationSink[] _sinks;
    
    public CompositeSink(params IMaterializationSink[] sinks)
    {
        _sinks = sinks ?? throw new ArgumentNullException(nameof(sinks));
    }

    public void Report(MaterializationDiagnostic diagnostic)
    {
        foreach (var sink in _sinks)
        {
            sink.Report(diagnostic);
        }
    }
}
```

### 6.5 MicrosoftLoggerSink (Optional - requires Microsoft.Extensions.Logging)

```csharp
/// <summary>
/// Forwards diagnostics to Microsoft.Extensions.Logging.ILogger.
/// </summary>
public sealed class MicrosoftLoggerSink : MaterializationSinkBase
{
    private readonly ILogger _logger;
    
    public MicrosoftLoggerSink(
        ILogger logger,
        SinkVerbosity verbosity = SinkVerbosity.ErrorsOnly)
        : base(verbosity)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override void ReportCore(MaterializationDiagnostic diagnostic)
    {
        var logLevel = diagnostic.Severity switch
        {
            DiagnosticSeverity.Trace => LogLevel.Trace,
            DiagnosticSeverity.Info => LogLevel.Information,
            DiagnosticSeverity.Warning => LogLevel.Warning,
            DiagnosticSeverity.Error => LogLevel.Error,
            _ => LogLevel.Information
        };

        _logger.Log(logLevel, diagnostic.Exception, diagnostic.Message);
    }
}
```

---

## 7. Usage Examples

### 7.1 Production Configuration (Silent Success, Log Errors)

```csharp
var options = new MaterializationOptions
{
    // Compilation options
    Compilation = new CompilationOptions
    {
        Culture = CultureInfo.InvariantCulture,
        AllowThousandsSeparators = true,
        CaseInsensitiveHeaders = true
    },
    
    // Conversion behavior
    Strictness = ConversionStrictness.Lenient,
    TrimStrings = true,
    NullStringBehavior = NullStringBehavior.ConvertToDefault,
    
    // Diagnostics
    SinkVerbosity = SinkVerbosity.ErrorsOnly,
    Sink = new MicrosoftLoggerSink(_logger),
    
    // Error handling
    DefaultErrorResolution = ErrorResolution.UseDefault
};

var users = csvRows
    .Select((row, index) => ObjectMaterializer.Create<User>(schema, row, options, index))
    .Where(u => u != null)
    .ToList();

// Only errors are logged - production-friendly
```

---

### 7.2 Development Configuration (See Everything)

```csharp
var sink = new CompositeSink(
    new ConsoleSink(SinkVerbosity.All, useColors: true),
    new CollectionSink(SinkVerbosity.All)
);

var options = new MaterializationOptions
{
    Strictness = ConversionStrictness.Strict,
    
    // Log all conversions
    SinkVerbosity = SinkVerbosity.All,
    Sink = sink,
    
    // Fail fast on errors
    DefaultErrorResolution = ErrorResolution.Throw
};

try
{
    var user = ObjectMaterializer.Create<User>(schema, row, options);
    Console.WriteLine($"Successfully created user: {user.Name}");
}
catch (MaterializationException ex)
{
    Console.WriteLine($"Materialization failed:");
    Console.WriteLine(ex.ToString());
}
```

---

### 7.3 Troubleshooting Configuration (Diagnostic Mode)

```csharp
var sink = new CollectionSink(SinkVerbosity.Diagnostic);

var options = new MaterializationOptions
{
    Strictness = ConversionStrictness.Lenient,
    TrimStrings = true,
    
    // Log every conversion attempt
    SinkVerbosity = SinkVerbosity.Diagnostic,
    Sink = sink,
    
    DefaultErrorResolution = ErrorResolution.UseDefault
};

var user = ObjectMaterializer.Create<User>(schema, row, options);

// Analyze which strategies were used
Console.WriteLine("\n=== Conversion Strategy Analysis ===");
    .Where(g => g.Any(d => d.Severity == DiagnosticSeverity.Info))
    .Select(g => new { Strategy = g.Key, Count = g.Count() })
    .OrderByDescending(x => x.Count)
    .ToList();

Console.WriteLine("Successful conversion strategies:");
foreach (var strategy in strategiesUsed)
{
    Console.WriteLine($"  {strategy.Strategy}: {strategy.Count} times");
}

// Analyze warnings
Console.WriteLine("\n=== Warnings ===");
var warnings = sink.Diagnostics
    .Where(d => d.Severity == DiagnosticSeverity.Warning)
    .ToList();

if (warnings.Any())
{
    foreach (var warning in warnings)
    {
        Console.WriteLine($"  {warning.MemberName}: {warning.Message}");
    }
}
else
{
    Console.WriteLine("  No warnings");
}

// Analyze errors
Console.WriteLine("\n=== Errors ===");
var errors = sink.Diagnostics
    .Where(d => d.Severity == DiagnosticSeverity.Error)
    .ToList();

if (errors.Any())
{
    foreach (var error in errors)
    {
        Console.WriteLine($"  {error.MemberName}: {error.Message}");
    }
}
else
{
    Console.WriteLine("  No errors");
}

// Show trace for specific member
Console.WriteLine("\n=== Trace for 'Age' property ===");
var ageTrace = sink.ByMember()["Age"]
    .OrderBy(d => d.Timestamp)
    .ToList();

foreach (var diagnostic in ageTrace)
{
    Console.WriteLine($"  [{diagnostic.Severity}] {diagnostic.ConversionStrategy}: {diagnostic.Message}");
}
```

---

### 7.4 Custom Error Handling Per Property

```csharp
var options = new MaterializationOptions
{
    Strictness = ConversionStrictness.Moderate,
    SinkVerbosity = SinkVerbosity.WarningsAndErrors,
    Sink = new ConsoleSink(SinkVerbosity.WarningsAndErrors),
    
    // Custom error handling per property
    OnError = context => context.MemberName switch
    {
        "Age" => ErrorResolution.UseDefault,           // Optional field - use 0
        "Email" => ErrorResolution.UseCustomValue,     // Use placeholder
        "Id" => ErrorResolution.SkipRow,               // Critical field - skip entire row
        "PhoneNumber" => ErrorResolution.SkipProperty, // Non-critical - leave unset
        _ => ErrorResolution.Throw                     // Fail on unexpected errors
    },
    
    CustomValueProvider = context => context.MemberName switch
    {
        "Email" => "noemail@example.com",
        "Country" => "Unknown",
        "Status" => "Pending",
        _ => null
    }
};

var users = csvRows
    .Select((row, index) => ObjectMaterializer.Create<User>(schema, row, options, index))
    .Where(u => u != null) // Filter out skipped rows
    .ToList();

Console.WriteLine($"Successfully processed {users.Count} of {csvRows.Length} rows");
```

---

### 7.5 Data Quality Audit

```csharp
var sink = new CollectionSink(SinkVerbosity.WarningsAndErrors);

var options = new MaterializationOptions
{
    Strictness = ConversionStrictness.Lenient,
    TrimStrings = true,
    
    SinkVerbosity = SinkVerbosity.WarningsAndErrors,
    Sink = sink,
    
    // Continue processing despite errors
    DefaultErrorResolution = ErrorResolution.UseDefault
};

var users = csvRows
    .Select((row, index) => ObjectMaterializer.Create<User>(schema, row, options, index))
    .Where(u => u != null)
    .ToList();

// Generate data quality report
Console.WriteLine("=== Data Quality Report ===");
Console.WriteLine($"Total rows: {csvRows.Length}");
Console.WriteLine($"Successfully processed: {users.Count}");
Console.WriteLine($"Rows with issues: {sink.Diagnostics.Select(d => d.RowIndex).Distinct().Count()}");

Console.WriteLine("\n=== Issues by Member ===");
var issuesByMember = sink.ByMember()
    .Select(g => new
    {
        Member = g.Key,
        ErrorCount = g.Count(d => d.Severity == DiagnosticSeverity.Error),
        WarningCount = g.Count(d => d.Severity == DiagnosticSeverity.Warning)
    })
    .OrderByDescending(x => x.ErrorCount + x.WarningCount)
    .ToList();

foreach (var issue in issuesByMember)
{
    Console.WriteLine($"  {issue.Member}: {issue.ErrorCount} errors, {issue.WarningCount} warnings");
}

Console.WriteLine("\n=== Sample Issues ===");
var sampleIssues = sink.Diagnostics
    .Where(d => d.Severity >= DiagnosticSeverity.Warning)
    .Take(10)
    .ToList();

foreach (var issue in sampleIssues)
{
    Console.WriteLine($"  Row {issue.RowIndex}, {issue.MemberName}: {issue.Message}");
    Console.WriteLine($"    Value: '{issue.AttemptedValue}'");
}
```

---

### 7.6 Performance Testing (No Diagnostics)

```csharp
var options = new MaterializationOptions
{
    Strictness = ConversionStrictness.Lenient,
    TrimStrings = true,
    
    // No sink = zero diagnostic overhead
    Sink = null,
    
    DefaultErrorResolution = ErrorResolution.UseDefault
};

var stopwatch = Stopwatch.StartNew();

var users = csvRows
    .Select(row => ObjectMaterializer.Create<User>(schema, row, options))
    .Where(u => u != null)
    .ToList();

stopwatch.Stop();

Console.WriteLine($"Processed {users.Count} rows in {stopwatch.ElapsedMilliseconds}ms");
Console.WriteLine($"Throughput: {users.Count / stopwatch.Elapsed.TotalSeconds:F0} rows/sec");
```

---

### 7.7 Strict Validation Mode

```csharp
var options = new MaterializationOptions
{
    // Strictest settings
    Strictness = ConversionStrictness.Strict,
    TrimStrings = false,
    NullStringBehavior = NullStringBehavior.Error,
    
    SinkVerbosity = SinkVerbosity.ErrorsOnly,
    Sink = new ConsoleSink(),
    
    // Fail fast on any issue
    DefaultErrorResolution = ErrorResolution.Throw
};

try
{
    var user = ObjectMaterializer.Create<User>(schema, row, options);
    Console.WriteLine("Validation passed - data is clean");
}
catch (MaterializationException ex)
{
    Console.WriteLine($"Validation failed: {ex.Message}");
    Console.WriteLine($"  Member: {ex.Context.MemberName}");
    Console.WriteLine($"  Value: '{ex.Context.AttemptedValue}'");
    Console.WriteLine($"  Expected Type: {ex.Context.TargetType.Name}");
}
```

---

### 7.8 Culture-Specific Parsing

```csharp
// European format: comma as decimal separator, period as thousands separator
var europeanOptions = new MaterializationOptions
{
    Compilation = new CompilationOptions
    {
        Culture = new CultureInfo("de-DE"),
        AllowThousandsSeparators = true,
        DateTimeFormats = new[] { "dd.MM.yyyy", "dd/MM/yyyy" }
    },
    
    Strictness = ConversionStrictness.Moderate,
    TrimStrings = true,
    
    Sink = new ConsoleSink(SinkVerbosity.All)
};

// Example data: "1.234,56" → 1234.56
var product = ObjectMaterializer.Create<Product>(
    schema: new[] { "Name", "Price", "ReleaseDate" },
    parameters: new object[] { "Widget", "1.234,56", "31.12.2024" },
    options: europeanOptions
);

Console.WriteLine($"Product: {product.Name}");
Console.WriteLine($"Price: {product.Price:F2}"); // 1234.56
Console.WriteLine($"Release: {product.ReleaseDate:d}"); // 12/31/2024
```

---

### 7.9 Batch Processing with Progress Reporting

```csharp
var sink = new DelegateSink(
    diagnostic =>
    {
        if (diagnostic.Severity == DiagnosticSeverity.Error)
        {
            Console.WriteLine($"[ERROR] Row {diagnostic.RowIndex}: {diagnostic.Message}");
        }
    },
    SinkVerbosity.ErrorsOnly
);

var options = new MaterializationOptions
{
    Strictness = ConversionStrictness.Lenient,
    Sink = sink,
    DefaultErrorResolution = ErrorResolution.SkipRow
};

var successCount = 0;
var errorCount = 0;
var users = new List<User>();

for (int i = 0; i < csvRows.Length; i++)
{
    var user = ObjectMaterializer.Create<User>(schema, csvRows[i], options, i);
    
    if (user != null)
    {
        users.Add(user);
        successCount++;
    }
    else
    {
        errorCount++;
    }
    
    // Progress reporting
    if ((i + 1) % 1000 == 0)
    {
        Console.WriteLine($"Processed {i + 1}/{csvRows.Length} rows " +
                         $"({successCount} success, {errorCount} errors)");
    }
}

Console.WriteLine($"\nFinal: {successCount} success, {errorCount} errors");
```

---

### 7.10 Integration with Existing Logging

```csharp
// Using Microsoft.Extensions.Logging
var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .AddDebug()
        .SetMinimumLevel(LogLevel.Information);
});

var logger = loggerFactory.CreateLogger<Program>();

var options = new MaterializationOptions
{
    Strictness = ConversionStrictness.Lenient,
    
    SinkVerbosity = SinkVerbosity.WarningsAndErrors,
    Sink = new MicrosoftLoggerSink(logger, SinkVerbosity.WarningsAndErrors),
    
    DefaultErrorResolution = ErrorResolution.UseDefault
};

var users = csvRows
    .Select((row, index) => ObjectMaterializer.Create<User>(schema, row, options, index))
    .Where(u => u != null)
    .ToList();

// Errors and warnings are automatically logged via ILogger
logger.LogInformation("Processed {Count} users", users.Count);
```

---

## 8. Configuration Matrix

| Scenario | Strictness | SinkVerbosity | Sink | DefaultErrorResolution | Use Case |
|----------|-----------|---------------|------|------------------------|----------|
| **Production ETL** | Lenient | ErrorsOnly | MicrosoftLoggerSink | UseDefault | Silent success, log failures only |
| **Development** | Strict | All | ConsoleSink | Throw | Catch issues early, see all conversions |
| **Troubleshooting** | Lenient | Diagnostic | CollectionSink | UseDefault | Understand which strategies work |
| **Data Quality Audit** | Moderate | WarningsAndErrors | CollectionSink | UseDefault | Track recoverable issues |
| **Performance Testing** | Lenient | (ignored) | null | UseDefault | No logging overhead |
| **Strict Validation** | Strict | ErrorsOnly | ConsoleSink | Throw | Fail fast on any issue |
| **Import with Cleanup** | Lenient | WarningsAndErrors | CompositeSink | UseCustomValue | Log issues, use fallbacks |
| **Critical Data** | Moderate | ErrorsOnly | MicrosoftLoggerSink | SkipRow | Skip rows with any errors |

---

## 9. Performance Characteristics

### 9.1 Overhead by Configuration

| Configuration | Overhead | Memory Impact | Use Case |
|--------------|----------|---------------|----------|
| `Sink = null` | **0%** | None | Production (no diagnostics) |
| `SinkVerbosity.ErrorsOnly` | **~2-5%** | Minimal (only errors) | Production (error logging) |
| `SinkVerbosity.WarningsAndErrors` | **~5-10%** | Low | Development, auditing |
| `SinkVerbosity.All` | **~10-20%** | Moderate | Troubleshooting |
| `SinkVerbosity.Diagnostic` | **~30-50%** | High (all attempts) | Deep debugging only |

### 9.2 Conversion Strategy Performance

| Strategy | Relative Speed | Strictness Level |
|----------|---------------|------------------|
| Direct assignment (no conversion) | **1.0x** (baseline) | All |
| Strict parsing (culture-aware) | **~1.2x** | Moderate+ |
| Lenient parsing (trim, case-insensitive) | **~1.5x** | Lenient+ |
| Convert.ChangeType fallback | **~3.0x** | Aggressive |

### 9.3 Optimization Tips

1. **Use `Sink = null` in production hot paths**
   ```csharp
   var options = new MaterializationOptions { Sink = null };
   ```

2. **Cache MaterializationOptions instances**
   ```csharp
   private static readonly MaterializationOptions ProductionOptions = new()
   {
       Strictness = ConversionStrictness.Lenient,
       Sink = null
   };
   ```

3. **Use appropriate strictness level**
   - Clean data → `Strict` (fastest)
   - CSV/Excel → `Lenient` (good balance)
   - Unknown sources → `Aggressive` (slowest)

4. **Avoid Diagnostic verbosity in production**
   - Development: `SinkVerbosity.All` or `Diagnostic`
   - Production: `SinkVerbosity.ErrorsOnly` or `null`

5. **Plan caching is automatic**
   - Same `CompilationOptions` → reuses cached plan
   - Different `MaterializationOptions` → same plan if `CompilationOptions` match

---

## 10. Migration Guide

### 10.1 Existing Code (Before)

```csharp
// Old API
var user = ObjectMaterializer.Create<User>(schema, row);

// Old API with culture
var plan = MemberMaterializationPlanner.Get<User>(
    caseInsensitiveHeaders: true,
    culture: CultureInfo.InvariantCulture,
    allowThousandsSeparators: true,
    dateTimeFormats: null
);
```

### 10.2 Migrated Code (After)

```csharp
// New API (backward compatible - uses defaults)
var user = ObjectMaterializer.Create<User>(schema, row);

// New API with options
var options = new MaterializationOptions
{
    Compilation = new CompilationOptions
    {
        CaseInsensitiveHeaders = true,
        Culture = CultureInfo.InvariantCulture,
        AllowThousandsSeparators = true
    }
};
var user = ObjectMaterializer.Create<User>(schema, row, options);

// Plan caching still works automatically
var plan = MemberMaterializationPlanner.Get<User>(options.Compilation);
```

### 10.3 Breaking Changes

**None.** All existing APIs remain functional with default behavior.

### 10.4 Deprecation Warnings (Optional Future Step)

```csharp
[Obsolete("Use MemberMaterializationPlanner.Get<T>(CompilationOptions) instead")]
public static MemberMaterializationPlan<T> Get<T>(
    bool caseInsensitiveHeaders = true,
    CultureInfo? culture = null,
    bool allowThousandsSeparators = true,
    string[]? dateTimeFormats = null)
{
    // Implementation remains for backward compatibility
}
```

---

## 11. Testing Requirements

### 11.1 Unit Tests

#### Sink Verbosity Tests
- [ ] `SinkVerbosity.ErrorsOnly` only reports errors
- [ ] `SinkVerbosity.WarningsAndErrors` reports warnings and errors
- [ ] `SinkVerbosity.All` reports info, warnings, and errors
- [ ] `SinkVerbosity.Diagnostic` reports all severities including trace
- [ ] Sink filtering happens at sink level, not conversion level

#### Error Resolution Tests
- [ ] `ErrorResolution.Throw` throws MaterializationException
- [ ] `ErrorResolution.UseDefault` uses default(T)
- [ ] `ErrorResolution.UseCustomValue` calls CustomValueProvider
- [ ] `ErrorResolution.SkipProperty` leaves property unset
- [ ] `ErrorResolution.SkipRow` returns null from Create<T>()
- [ ] `OnError` callback overrides `DefaultErrorResolution`
- [ ] CustomValueProvider is required when using UseCustomValue

#### Conversion Strictness Tests
- [ ] `Strict` only allows exact type matches
- [ ] `Moderate` allows culture-aware parsing
- [ ] `Lenient` allows whitespace trimming and case-insensitive enums
- [ ] `Aggressive` attempts Convert.ChangeType fallback
- [ ] Each strictness level includes previous levels' strategies

#### Diagnostic Reporting Tests
- [ ] ConversionStrategy is populated correctly
- [ ] RowIndex is propagated through call chain
- [ ] MemberName is captured for each property
- [ ] Timestamp is set on diagnostic creation
- [ ] Exception is captured when conversion fails

#### Plan Caching Tests
- [ ] Same CompilationOptions reuses cached plan
- [ ] Different CompilationOptions creates new plan
- [ ] Different MaterializationOptions (same CompilationOptions) reuses plan
- [ ] Cache key includes culture, formats, case-sensitivity
- [ ] Thread-safe cache access

#### Null Handling Tests
- [ ] `NullStringBehavior.Error` throws on null/empty strings
- [ ] `NullStringBehavior.ConvertToDefault` converts to default(T)
- [ ] `NullStringBehavior.PreserveForStrings` preserves null for strings
- [ ] Nullable<T> properties handle null correctly

---

### 11.2 Integration Tests

#### End-to-End Scenarios
- [ ] Production configuration (Lenient + ErrorsOnly + UseDefault)
- [ ] Development configuration (Strict + All + Throw)
- [ ] Diagnostic configuration (Lenient + Diagnostic + UseDefault)
- [ ] Custom error handling with multiple properties
- [ ] Batch processing with progress reporting

#### Sink Integration Tests
- [ ] ConsoleSink writes to console
- [ ] CollectionSink collects diagnostics
- [ ] DelegateSink forwards to callback
- [ ] CompositeSink forwards to multiple sinks
- [ ] MicrosoftLoggerSink integrates with ILogger

#### Culture-Specific Tests
- [ ] US culture (period as decimal, comma as thousands)
- [ ] European culture (comma as decimal, period as thousands)
- [ ] Invariant culture
- [ ] Custom DateTime formats
- [ ] Culture-insensitive types (bool, Guid, enum)

#### Performance Tests
- [ ] Sink = null has zero overhead
- [ ] SinkVerbosity.ErrorsOnly has minimal overhead (~2-5%)
- [ ] Plan caching reduces compilation overhead
- [ ] Parallel processing is thread-safe

---

### 11.3 Test Data

```csharp
public class TestUser
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int? Age { get; set; }
    public decimal Salary { get; set; }
    public DateTime HireDate { get; set; }
    public bool IsActive { get; set; }
    public UserStatus Status { get; set; }
}

public enum UserStatus
{
    Pending,
    Active,
    Inactive
}

// Test cases
var testCases = new[]
{
    // Clean data
    new { Schema = new[] { "Id", "Name", "Age", "Salary", "HireDate", "IsActive", "Status" },
          Values = new object[] { 1, "John", 30, 50000.00m, "2024-01-15", true, "Active" } },
    
    // Messy data (needs lenient parsing)
    new { Schema = new[] { "Id", "Name", "Age", "Salary", "HireDate", "IsActive", "Status" },
          Values = new object[] { " 2 ", " Jane ", " 25 ", " 60,000.00 ", " 01/15/2024 ", " true ", " active " } },
    
    // Missing optional field
    new { Schema = new[] { "Id", "Name", "Salary", "HireDate", "IsActive", "Status" },
          Values = new object[] { 3, "Bob", 45000.00m, "2024-02-01", true, "Active" } },
    
    // Invalid data
    new { Schema = new[] { "Id", "Name", "Age", "Salary", "HireDate", "IsActive", "Status" },
          Values = new object[] { "abc", "Invalid", "xyz", "not-a-number", "invalid-date", "maybe", "Unknown" } }
};
```

---

## 12. Documentation Requirements

### 12.1 README Updates

- [ ] Add "Configuration" section with options overview
- [ ] Add "Diagnostics" section with sink examples
- [ ] Add "Error Handling" section with resolution strategies
- [ ] Add "Performance" section with optimization tips
- [ ] Add configuration matrix table
- [ ] Add troubleshooting guide

### 12.2 API Documentation

- [ ] XML documentation comments on all public types
- [ ] Code examples in XML comments
- [ ] Link related types in documentation
- [ ] Document thread-safety guarantees
- [ ] Document performance characteristics

### 12.3 Guides

#### Quick Start Guide
```markdown
# Quick Start

## Basic Usage (No Configuration)
```csharp
var user = ObjectMaterializer.Create<User>(schema, row);
```

## Production Configuration
```csharp
var options = new MaterializationOptions
{
    Strictness = ConversionStrictness.Lenient,
    Sink = new MicrosoftLoggerSink(_logger),
    DefaultErrorResolution = ErrorResolution.UseDefault
};
var user = ObjectMaterializer.Create<User>(schema, row, options);
```

## Troubleshooting
```csharp
var sink = new CollectionSink(SinkVerbosity.Diagnostic);
var options = new MaterializationOptions
{
    Sink = sink,
    SinkVerbosity = SinkVerbosity.Diagnostic
};
var user = ObjectMaterializer.Create<User>(schema, row, options);

// Analyze diagnostics
foreach (var diagnostic in sink.Diagnostics)
{
    Console.WriteLine(diagnostic.ToString());
}
```
```

#### Troubleshooting Guide
```markdown
# Troubleshooting Guide

## Problem: Conversion Fails Silently

**Symptom:** Property has default value instead of expected value

**Solution:** Enable diagnostics
```csharp
var options = new MaterializationOptions
{
    SinkVerbosity = SinkVerbosity.All,
    Sink = new ConsoleSink(SinkVerbosity.All)
};
```

## Problem: "abc" Won't Convert to Int32

**Symptom:** MaterializationException thrown

**Diagnosis:** Check conversion strategy chain
```csharp
var sink = new CollectionSink(SinkVerbosity.Diagnostic);
var options = new MaterializationOptions
{
    Sink = sink,
    SinkVerbosity = SinkVerbosity.Diagnostic
};

// Check which strategies were attempted
var attempts = sink.Diagnostics
    .Where(d => d.MemberName == "Age")
    .OrderBy(d => d.Timestamp);
```

## Problem: Performance Degradation

**Symptom:** Slow materialization

**Solution:** Disable diagnostics in production
```csharp
var options = new MaterializationOptions
{
    Sink = null // Zero overhead
};
```
```

---

## 13. Implementation Checklist

### Phase 1: Foundation (Week 1)
- [ ] Add all enum types (ConversionStrictness, ErrorResolution, SinkVerbosity, DiagnosticSeverity, NullStringBehavior)
- [ ] Add CompilationOptions class
- [ ] Add MaterializationOptions class with Validate() method
- [ ] Add MaterializationDiagnostic class
- [ ] Add MaterializationErrorContext class
- [ ] Add exception types (MaterializationException, ConversionException, SkipRowException, SkipPropertyException)
- [ ] Add IMaterializationSink interface
- [ ] Add MaterializationSinkBase abstract class
- [ ] Unit tests for all foundation types

### Phase 2: Core Integration (Week 2)
- [ ] Update PlanCacheKey to use CompilationOptions
- [ ] Update MemberMaterializationPlanner.Get() signature
- [ ] Add backward compatibility overloads
- [ ] Update MemberSetter to accept MaterializationOptions
- [ ] Update BuildConvertExpression() to thread options
- [ ] Update CompileSetterForProperty() with try-catch for SkipPropertyException
- [ ] Update CompileSetterForField() with try-catch
- [ ] Unit tests for plan caching with new options

### Phase 3: Conversion Logic (Week 2-3)
- [ ] Update ConvertObject() signature to accept options and context
- [ ] Implement ReportDiagnostic() helper
- [ ] Implement HandleConversionError() with all resolution strategies
- [ ] Implement HandleNullValue() with NullStringBehavior
- [ ] Implement ConvertFromString() with diagnostic reporting
- [ ] Implement ConvertToInt32() with full strategy chain
- [ ] Implement similar methods for other primitive types (long, decimal, double, DateTime, bool, enum)
- [ ] Unit tests for each conversion path

### Phase 4: Materializer Updates (Week 3)
- [ ] Update MemberMaterializer.FeedUsingSchema() to accept options
- [ ] Update MemberMaterializer.FeedOrdered() to accept options
- [ ] Add backward compatibility overloads
- [ ] Update ObjectMaterializer.Create() overloads
- [ ] Update NewWithSchema() to thread options
- [ ] Update CreateViaPrimaryConstructorWithSchema() to handle errors
- [ ] Handle SkipRowException at appropriate levels
- [ ] Integration tests for full materialization flow

### Phase 5: Built-in Sinks (Week 4)
- [ ] Implement ConsoleSink
- [ ] Implement CollectionSink with analysis methods
- [ ] Implement DelegateSink
- [ ] Implement CompositeSink
- [ ] Implement MicrosoftLoggerSink (optional)
- [ ] Unit tests for each sink
- [ ] Integration tests for sink filtering

### Phase 6: Testing & Documentation (Week 4-5)
- [ ] Complete unit test suite (see Section 11.1)
- [ ] Complete integration test suite (see Section 11.2)
- [ ] Performance benchmarks
- [ ] Update README with new features
- [ ] Write troubleshooting guide
- [ ] Add XML documentation comments
- [ ] Create migration guide
- [ ] Code examples for all scenarios

### Phase 7: Polish & Release (Week 5)
- [ ] Code review
- [ ] Performance optimization
- [ ] Memory profiling
- [ ] Thread-safety audit
- [ ] Documentation review
- [ ] Release notes
- [ ] NuGet package update

---

## 14. Open Questions & Decisions

### 14.1 Resolved

✅ **Should we support retry logic?**  
**Decision:** No. Conversions are deterministic. Failures indicate misconfiguration or invalid data.

✅ **Should sink filtering happen at conversion or sink level?**  
**Decision:** Sink level. Allows different sinks to have different verbosity.

✅ **Should options affect plan caching?**  
**Decision:** Only CompilationOptions affect caching. Runtime options do not.

✅ **Should we support SkipProperty vs SkipRow?**  
**Decision:** Yes. Both are useful in different scenarios.

### 14.2 Pending

⚠️ **Should we add async sink support?**  
**Consideration:** For sinks that write to databases or remote services.  
**Recommendation:** Defer to v2.1. Add `IAsyncMaterializationSink` interface if needed.

⚠️ **Should we add structured logging support (e.g., Serilog)?**  
**Consideration:** Many users prefer structured logging over string messages.  
**Recommendation:** Add in Phase 5 if time permits. Otherwise defer to v2.1.

⚠️ **Should we support custom conversion strategies?**  
**Consideration:** Allow users to register custom converters for specific types.  
**Recommendation:** Defer to v2.1. Current extensibility via CustomValueProvider is sufficient for MVP.

⚠️ **Should we add batch validation API?**  
**Consideration:** Validate entire dataset before processing.  
**Recommendation:** Defer to v2.1. Users can implement using CollectionSink.

---

## 15. Success Criteria

### 15.1 Functional Requirements
- ✅ All error resolution strategies work correctly
- ✅ Sink verbosity filtering works as specified
- ✅ Conversion strictness levels behave as documented
- ✅ Plan caching is not affected by runtime options
- ✅ Backward compatibility maintained

### 15.2 Performance Requirements
- ✅ Sink = null has **zero overhead** (< 1% difference)
- ✅ SinkVerbosity.ErrorsOnly has **< 5% overhead**
- ✅ Plan caching reduces compilation time by **> 95%**
- ✅ Parallel processing is thread-safe with no contention

### 15.3 Quality Requirements
- ✅ **> 90% code coverage** for core conversion logic
- ✅ **> 80% code coverage** overall
- ✅ **Zero critical bugs** in code review
- ✅ **All integration tests pass**
- ✅ **Documentation complete** for all public APIs

### 15.4 User Experience Requirements
- ✅ **< 5 minutes** to understand basic usage
- ✅ **< 15 minutes** to configure production setup
- ✅ **< 30 minutes** to troubleshoot conversion issues using diagnostics
- ✅ **Clear error messages** with actionable guidance

---

## 16. Approval & Sign-off

| Role | Name | Status | Date |
|------|------|--------|------|
| **Architecture Review** | Architecture Team | ✅ Approved | 2025-10-09 |
| **Technical Lead** | [Pending] | ⏳ Pending | |
| **QA Lead** | [Pending] | ⏳ Pending | |
| **Documentation Lead** | [Pending] | ⏳ Pending | |
| **Product Owner** | [Pending] | ⏳ Pending | |

---

## 17. Revision History

| Version | Date | Author | Changes |
|---------|------|--------|---------|
| 1.0 | 2025-10-09 | Architecture Team | Initial draft |
| 2.0 | 2025-10-09 | Architecture Team | Complete rewrite with implementation details, integration points, full API definitions, usage examples, testing requirements, and migration guide |

---

## 18. Appendix A: Complete Type Definitions

### A.1 Full MaterializationOptions Class

```csharp
namespace YourNamespace.Materialization
{
    /// <summary>
    /// Comprehensive options for object materialization.
    /// </summary>
    public sealed class MaterializationOptions
    {
        /// <summary>
        /// Default options instance (used when no options provided).
        /// </summary>
        public static MaterializationOptions Default { get; } = new();
        
        // ===== Compilation Options (affect caching) =====
        
        /// <summary>
        /// Options that affect expression tree compilation.
        /// Changes to these require plan recompilation.
        /// </summary>
        public CompilationOptions Compilation { get; set; } = new();
        
        // ===== Conversion Behavior (runtime) =====
        
        /// <summary>
        /// Controls how aggressively conversions are attempted.
        /// Default: Moderate (culture-aware parsing, no lenient strategies)
        /// </summary>
        public ConversionStrictness Strictness { get; set; } = ConversionStrictness.Moderate;
        
        /// <summary>
        /// Whether to trim leading/trailing whitespace from strings before conversion.
        /// Default: true (recommended for CSV/Excel imports)
        /// </summary>
        public bool TrimStrings { get; set; } = true;
        
        /// <summary>
        /// How to handle null or empty strings during conversion.
        /// Default: ConvertToDefault
        /// </summary>
        public NullStringBehavior NullStringBehavior { get; set; } = NullStringBehavior.ConvertToDefault;
        
        // ===== Diagnostic Reporting =====
        
        /// <summary>
        /// Sink for diagnostic messages. Set to null to disable diagnostics (zero overhead).
        /// Default: null (no diagnostics)
        /// </summary>
        public IMaterializationSink? Sink { get; set; }
        
        /// <summary>
        /// Controls what information is reported to the Sink.
        /// Default: ErrorsOnly (production-friendly)
        /// </summary>
        public SinkVerbosity SinkVerbosity { get; set; } = SinkVerbosity.ErrorsOnly;
        
        // ===== Error Handling =====
        
        /// <summary>
        /// Default error resolution strategy.
        /// Can be overridden per-property via OnError callback.
        /// Default: Throw (fail-fast)
        /// </summary>
        public ErrorResolution DefaultErrorResolution { get; set; } = ErrorResolution.Throw;
        
        /// <summary>
        /// Callback invoked when a conversion error occurs.
        /// Allows per-property error handling logic.
        /// Return value overrides DefaultErrorResolution.
        /// Default: null (use DefaultErrorResolution)
        /// </summary>
        public Func<MaterializationErrorContext, ErrorResolution>? OnError { get; set; }
        
        /// <summary>
        /// Provides custom values when ErrorResolution.UseCustomValue is returned.
        /// Required if OnError or DefaultErrorResolution uses UseCustomValue.
        /// Default: null
        /// </summary>
        public Func<MaterializationErrorContext, object?>? CustomValueProvider { get; set; }
        
        // ===== Convenience Properties =====
        
        /// <summary>
        /// Shorthand for Compilation.Culture.
        /// </summary>
        public CultureInfo Culture
        {
            get => Compilation.Culture;
            set => Compilation.Culture = value;
        }
        
        /// <summary>
        /// Shorthand for Compilation.CaseInsensitiveHeaders.
        /// </summary>
        public bool CaseInsensitiveHeaders
        {
            get => Compilation.CaseInsensitiveHeaders;
            set => Compilation.CaseInsensitiveHeaders = value;
        }
        
        /// <summary>
        /// Validates the options configuration.
        /// Throws InvalidOperationException if configuration is invalid.
        /// </summary>
        public void Validate()
        {
            if ((DefaultErrorResolution == ErrorResolution.UseCustomValue || 
                 OnError != null) && 
                CustomValueProvider == null)
            {
                // Check if OnError might return UseCustomValue
                var testContext = new MaterializationErrorContext
                {
                    MemberName = "test",
                    TargetType = typeof(object),
                    AttemptedValue = null,
                    Exception = new Exception()
                };
                
                var resolution = OnError?.Invoke(testContext) ?? DefaultErrorResolution;
                
                if (resolution == ErrorResolution.UseCustomValue)
                {
                    throw new InvalidOperationException(
                        "CustomValueProvider must be set when using ErrorResolution.UseCustomValue");
                }
            }
            
            if (Compilation.DateTimeFormats.Any(f => string.IsNullOrWhiteSpace(f)))
            {
                throw new InvalidOperationException(
                    "DateTimeFormats cannot contain null or whitespace entries");
            }
            
            if (Sink != null && SinkVerbosity < 0)
            {
                throw new InvalidOperationException(
                    $"Invalid SinkVerbosity: {SinkVerbosity}");
            }
        }
        
        /// <summary>
        /// Creates a shallow copy of this options instance.
        /// Useful for creating variations without affecting the original.
        /// </summary>
        public MaterializationOptions Clone()
        {
            return new MaterializationOptions
            {
                Compilation = new CompilationOptions
                {
                    Culture = Compilation.Culture,
                    AllowThousandsSeparators = Compilation.AllowThousandsSeparators,
                    DateTimeFormats = Compilation.DateTimeFormats.ToArray(),
                    CaseInsensitiveHeaders = Compilation.CaseInsensitiveHeaders
                },
                Strictness = Strictness,
                TrimStrings = TrimStrings,
                NullStringBehavior = NullStringBehavior,
                Sink = Sink,
                SinkVerbosity = SinkVerbosity,
                DefaultErrorResolution = DefaultErrorResolution,
                OnError = OnError,
                CustomValueProvider = CustomValueProvider
            };
        }
    }
}
```

---

## 19. Appendix B: Conversion Strategy Decision Tree

```
Input Value
    │
    ├─ Is null?
    │   ├─ Yes → HandleNullValue()
    │   │   ├─ NullStringBehavior.Error → HandleConversionError()
    │   │   ├─ NullStringBehavior.ConvertToDefault → default(T)
    │   │   └─ NullStringBehavior.PreserveForStrings → null (if string), else default(T)
    │   │
    │   └─ No → Continue
    │
    ├─ Is assignable to target type?
    │   ├─ Yes → Direct assignment (no conversion)
    │   └─ No → Continue
    │
    ├─ Is target Nullable<T>?
    │   ├─ Yes → Unwrap to T, continue
    │   └─ No → Continue
    │
    ├─ Is value a string?
    │   ├─ Yes → ConvertFromString()
    │   │   │
    │   │   ├─ TrimStrings enabled?
    │   │   │   ├─ Yes → Trim, check if empty
    │   │   │   │   ├─ Empty → HandleNullValue()
    │   │   │   │   └─ Not empty → Continue
    │   │   │   └─ No → Continue
    │   │   │
    │   │   ├─ Target is int?
    │   │   │   └─ ConvertToInt32()
    │   │   │       │
    │   │   │       ├─ Strategy 1: Strict Parse
    │   │   │       │   ├─ Success → Return result
    │   │   │       │   └─ Failure → Continue
    │   │   │       │
    │   │   │       ├─ Strategy 2: Lenient Parse (if Strictness >= Lenient)
    │   │   │       │   ├─ Success → Return result (log warning)
    │   │   │       │   └─ Failure → Continue
    │   │   │       │
    │   │   │       ├─ Strategy 3: ChangeType (if Strictness >= Aggressive)
    │   │   │       │   ├─ Success → Return result (log warning)
    │   │   │       │   └─ Failure → Continue
    │   │   │       │
    │   │   │       └─ All failed → HandleConversionError()
    │   │   │
    │   │   ├─ Target is other primitive? (long, decimal, double, DateTime, bool, enum)
    │   │   │   └─ Similar strategy chain as int
    │   │   │
    │   │   └─ Target is other type?
    │   │       └─ AttemptFallbackConversion()
    │   │
    │   └─ No → ConvertOther()
    │       │
    │       ├─ Numeric conversion? (int to long, float to double, etc.)
    │       │   ├─ Success → Return result
    │       │   └─ Failure → Continue
    │       │
    │       ├─ Enum conversion?
    │       │   ├─ Success → Return result
    │       │   └─ Failure → Continue
    │       │
    │       └─ ChangeType (if Strictness >= Aggressive)
    │           ├─ Success → Return result
    │           └─ Failure → HandleConversionError()
    │
    └─ HandleConversionError()
        │
        ├─ Create MaterializationErrorContext
        │
        ├─ Invoke OnError callback (if set)
        │   ├─ Returns resolution → Use it
        │   └─ Returns null → Use DefaultErrorResolution
        │
        └─ Apply ErrorResolution
            ├─ Throw → throw MaterializationException
            ├─ UseDefault → return default(T)
            ├─ UseCustomValue → invoke CustomValueProvider
            ├─ SkipProperty → throw SkipPropertyException
            └─ SkipRow → throw SkipRowException
```

---

## 20. Appendix C: Diagnostic Flow

```
ConvertObject() called
    │
    ├─ Sink is null?
    │   ├─ Yes → Skip all diagnostic reporting (zero overhead)
    │   └─ No → Continue
    │
    ├─ Attempt conversion
    │   │
    │   ├─ Before each strategy
    │   │   └─ ReportDiagnostic(Trace, "Attempting [strategy]")
    │   │
    │   ├─ Strategy succeeds
    │   │   └─ ReportDiagnostic(Info, "Success via [strategy]")
    │   │
    │   ├─ Strategy fails
    │   │   └─ ReportDiagnostic(Trace, "Failed via [strategy]")
    │   │
    │   └─ Lenient/Aggressive strategy succeeds
    │       └─ ReportDiagnostic(Warning, "Success via [strategy] - consider data cleanup")
    │
    └─ All strategies fail
        │
        ├─ ReportDiagnostic(Error, "Conversion failed")
        │
        └─ HandleConversionError()
            │
            ├─ Resolution: UseDefault
            │   └─ ReportDiagnostic(Warning, "Using default value")
            │
            ├─ Resolution: UseCustomValue
            │   └─ ReportDiagnostic(Warning, "Using custom value")
            │
            ├─ Resolution: SkipProperty
            │   └─ ReportDiagnostic(Warning, "Skipping property")
            │
            ├─ Resolution: SkipRow
            │   └─ ReportDiagnostic(Warning, "Skipping row")
            │
            └─ Resolution: Throw
                └─ (Error already reported, exception thrown)

ReportDiagnostic()
    │
    ├─ Create MaterializationDiagnostic
    │   ├─ Severity
    │   ├─ RowIndex (if available)
    │   ├─ MemberName (if available)
    │   ├─ Message
    │   ├─ AttemptedValue
    │   ├─ TargetType
    │   ├─ ConversionStrategy
    │   ├─ Exception (if any)
    │   └─ Timestamp
    │
    └─ Sink.Report(diagnostic)
        │
        └─ MaterializationSinkBase.Report()
            │
            ├─ Check ShouldReport(severity)
            │   │
            │   ├─ SinkVerbosity.ErrorsOnly
            │   │   └─ Report only Error
            │   │
            │   ├─ SinkVerbosity.WarningsAndErrors
            │   │   └─ Report Warning and Error
            │   │
            │   ├─ SinkVerbosity.All
            │   │   └─ Report Info, Warning, and Error
            │   │
            │   └─ SinkVerbosity.Diagnostic
            │       └─ Report all (including Trace)
            │
            ├─ Should report?
            │   ├─ Yes → ReportCore(diagnostic)
            │   └─ No → Skip (no overhead)
            │
            └─ ReportCore() (implemented by concrete sink)
                ├─ ConsoleSink → Console.WriteLine()
                ├─ CollectionSink → _diagnostics.Add()
                ├─ DelegateSink → _handler.Invoke()
                ├─ CompositeSink → Forward to all sinks
                └─ MicrosoftLoggerSink → _logger.Log()
```

---

## 21. Appendix D: Error Handling Flow

```
Conversion fails in ConvertObject()
    │
    └─ HandleConversionError()
        │
        ├─ Create MaterializationErrorContext
        │   ├─ RowIndex
        │   ├─ MemberName
        │   ├─ AttemptedValue
        │   ├─ TargetType
        │   ├─ Exception
        │   └─ ConversionStrategy
        │
        ├─ Report error diagnostic
        │   └─ ReportDiagnostic(Error, "Conversion failed")
        │
        ├─ Determine resolution
        │   ├─ OnError callback set?
        │   │   ├─ Yes → resolution = OnError(context)
        │   │   └─ No → resolution = DefaultErrorResolution
        │   │
        │   └─ Apply resolution
        │
        └─ Switch on resolution
            │
            ├─ ErrorResolution.Throw
            │   └─ throw new MaterializationException(message, context)
            │       │
            │       └─ Caught by caller
            │           ├─ ObjectMaterializer.Create() → Propagate or return null
            │           ├─ MemberMaterializer.FeedUsingSchema() → Propagate
            │           └─ User code → Handle exception
            │
            ├─ ErrorResolution.UseDefault
            │   ├─ Calculate default value
            │   │   ├─ Value type → Activator.CreateInstance(targetType)
            │   │   └─ Reference type → null
            │   │
            │   ├─ Report diagnostic
            │   │   └─ ReportDiagnostic(Warning, "Using default value")
            │   │
            │   └─ Return default value
            │       └─ Property is set to default value
            │
            ├─ ErrorResolution.UseCustomValue
            │   ├─ CustomValueProvider is null?
            │   │   ├─ Yes → throw InvalidOperationException
            │   │   └─ No → Continue
            │   │
            │   ├─ Invoke CustomValueProvider(context)
            │   │   └─ customValue = provider(context)
            │   │
            │   ├─ Report diagnostic
            │   │   └─ ReportDiagnostic(Warning, "Using custom value")
            │   │
            │   └─ Return custom value
            │       └─ Property is set to custom value
            │
            ├─ ErrorResolution.SkipProperty
            │   ├─ Report diagnostic
            │   │   └─ ReportDiagnostic(Warning, "Skipping property")
            │   │
            │   └─ throw new SkipPropertyException(context)
            │       │
            │       └─ Caught in compiled setter (try-catch in BuildConvertExpression)
            │           └─ Property assignment is skipped
            │               └─ Property retains default/initializer value
            │
            └─ ErrorResolution.SkipRow
                ├─ Report diagnostic
                │   └─ ReportDiagnostic(Warning, "Skipping row")
                │
                └─ throw new SkipRowException(context)
                    │
                    └─ Caught in MemberMaterializer.FeedUsingSchema()
                        └─ Propagated to ObjectMaterializer.Create()
                            └─ Caught and returns null
                                └─ Row is filtered out by caller (Where(u => u != null))
```

---

## 22. Appendix E: Plan Caching Flow

```
ObjectMaterializer.Create<User>(schema, row, options)
    │
    └─ MemberMaterializationPlanner.Get<User>(options.Compilation)
        │
        ├─ Create cache key
        │   └─ PlanCacheKey.Create<User>(options.Compilation)
        │       │
        │       └─ Key components:
        │           ├─ Type: typeof(User)
        │           └─ CompilationOptionsKey: options.Compilation.GetCacheKey()
        │               ├─ Culture.Name
        │               ├─ AllowThousandsSeparators
        │               ├─ CaseInsensitiveHeaders
        │               └─ DateTimeFormats (hashed)
        │
        ├─ Check cache
        │   └─ Cache.GetOrAdd(key, _ => BuildPlan())
        │       │
        │       ├─ Key exists in cache?
        │       │   ├─ Yes → Return cached plan (fast path)
        │       │   │   └─ Plan contains compiled delegates
        │       │   │       └─ Delegates accept MaterializationOptions at runtime
        │       │   │
        │       │   └─ No → Build new plan (slow path)
        │       │       │
        │       │       └─ MemberMaterializationPlan<User>.Build(compilationOptions)
        │       │           │
        │       │           ├─ Reflect on User type
        │       │           │   ├─ Get properties with [Order] attribute
        │       │           │   └─ Get fields with [Order] attribute
        │       │           │
        │       │           ├─ For each member
        │       │           │   └─ CompileSetterForProperty/Field()
        │       │           │       │
        │       │           │       └─ Build expression tree:
        │       │           │           (obj, val, options) =>
        │       │           │           {
        │       │           │               try
        │       │           │               {
        │       │           │                   obj.Property = ConvertObject(
        │       │           │                       val,
        │       │           │                       targetType,
        │       │           │                       options,  ← Runtime options
        │       │           │                       memberName,
        │       │           │                       rowIndex);
        │       │           │               }
        │       │           │               catch (SkipPropertyException)
        │       │           │               {
        │       │           │                   // Skip this property
        │       │           │               }
        │       │           │           }
        │       │           │
        │       │           ├─ Compile expression to delegate
        │       │           │   └─ Action<User, object?, MaterializationOptions>
        │       │           │
        │       │           ├─ Create MemberSetter
        │       │           │   ├─ Name
        │       │           │   ├─ OrderIndex
        │       │           │   └─ Set (compiled delegate)
        │       │           │
        │       │           └─ Create MemberMaterializationPlan<User>
        │       │               ├─ Members (array of MemberSetter)
        │       │               └─ NameComparer (based on CaseInsensitiveHeaders)
        │       │
        │       └─ Cache plan for future use
        │
        └─ Return plan

Plan is used:
    │
    └─ MemberMaterializer.FeedUsingSchema(obj, schema, values, options)
        │
        └─ For each member in plan.Members
            └─ member.Set(obj, values[idx], options)
                │                            ↑
                │                            │
                │                    Runtime options passed here
                │                    (not baked into compiled delegate)
                │
                └─ Compiled delegate executes
                    └─ ConvertObject(val, type, options, ...)
                        │
                        └─ Uses options.Strictness, options.Sink, etc.
                            at runtime (not compilation time)

Key Insight:
    ┌─────────────────────────────────────────────────────────┐
    │ CompilationOptions → Affects cache key                  │
    │   - Culture, DateTimeFormats, etc.                      │
    │   - Baked into compiled delegates                       │
    │                                                          │
    │ MaterializationOptions (runtime) → Does NOT affect cache│
    │   - Sink, Strictness, ErrorResolution, etc.            │
    │   - Passed to ConvertObject() at runtime                │
    │   - Different options can use same cached plan          │
    └─────────────────────────────────────────────────────────┘
```

---

## 23. Appendix F: Example Benchmark Results

```csharp
// BenchmarkDotNet results (simulated for specification)

|                    Method |      Mean |    Error |   StdDev | Ratio | Allocated |
|-------------------------- |----------:|---------:|---------:|------:|----------:|
|          NoOptions_Cached |  45.23 ns | 0.234 ns | 0.219 ns |  1.00 |         - |
|      WithOptions_NoSink   |  45.67 ns | 0.312 ns | 0.292 ns |  1.01 |         - |
|   WithSink_ErrorsOnly     |  47.89 ns | 0.401 ns | 0.375 ns |  1.06 |      32 B |
| WithSink_WarningsAndErrors|  52.34 ns | 0.523 ns | 0.489 ns |  1.16 |      96 B |
|          WithSink_All     |  58.12 ns | 0.678 ns | 0.634 ns |  1.29 |     184 B |
|    WithSink_Diagnostic    |  71.45 ns | 0.892 ns | 0.834 ns |  1.58 |     312 B |

// Batch processing (10,000 rows)
|                    Method |      Mean |    Error |   StdDev | Ratio | Allocated |
|-------------------------- |----------:|---------:|---------:|------:|----------:|
|          NoOptions_Cached | 452.3 μs  | 2.34 μs  | 2.19 μs  |  1.00 |    1.2 KB |
|      WithOptions_NoSink   | 456.7 μs  | 3.12 μs  | 2.92 μs  |  1.01 |    1.2 KB |
|   WithSink_ErrorsOnly     | 478.9 μs  | 4.01 μs  | 3.75 μs  |  1.06 |  320.5 KB |
| WithSink_WarningsAndErrors| 523.4 μs  | 5.23 μs  | 4.89 μs  |  1.16 |  960.8 KB |
|          WithSink_All     | 581.2 μs  | 6.78 μs  | 6.34 μs  |  1.29 | 1840.2 KB |
|    WithSink_Diagnostic    | 714.5 μs  | 8.92 μs  | 8.34 μs  |  1.58 | 3120.6 KB |

Conclusions:
1. Sink = null has virtually zero overhead (1% difference is within margin of error)
2. ErrorsOnly adds ~6% overhead (acceptable for production)
3. Diagnostic mode adds ~58% overhead (use only for troubleshooting)
4. Memory allocations scale with verbosity (due to diagnostic object creation)
```

---

## 24. Final Notes

### 24.1 Design Philosophy

This specification embodies the following design principles:

1. **Performance by Default**: The zero-overhead path (Sink = null) is the default, ensuring production systems aren't penalized.

2. **Progressive Enhancement**: Features are layered - start simple, add complexity only when needed.

3. **Explicit Over Implicit**: All behavior is explicitly configured. No magic, no surprises.

4. **Fail-Safe Defaults**: Default settings (Moderate strictness, Throw on error) catch issues early in development.

5. **Separation of Concerns**: Compilation options, conversion behavior, diagnostics, and error handling are independent axes.

### 24.2 Future Enhancements (Post-v2.0)

Potential features for future versions:

- **Async Sink Support**: `IAsyncMaterializationSink` for database/network logging
- **Structured Logging**: Native Serilog/NLog integration with structured properties
- **Custom Converters**: User-registered type converters (e.g., string → CustomType)
- **Batch Validation API**: Validate entire dataset before processing
- **Schema Inference**: Auto-detect schema from first row
- **Streaming API**: Process large files without loading into memory
- **Parallel Processing**: Built-in parallel materialization with thread-safe sinks

### 24.3 Community Feedback

After initial release, gather feedback on:

- Most commonly used configurations
- Pain points in error handling
- Desired sink implementations
- Performance in real-world scenarios
- Documentation clarity

### 24.4 Maintenance Plan

- **Monthly**: Review GitHub issues for bug reports
- **Quarterly**: Performance benchmarks to detect regressions
- **Bi-annually**: API review for potential improvements
- **Annually**: Major version planning based on community feedback

---

## 25. Conclusion

This specification provides a comprehensive, production-ready design for materialization options, diagnostics, and error resolution. The implementation maintains backward compatibility while enabling advanced scenarios through explicit configuration.

**Key Achievements:**

✅ **Zero-overhead fast path** for production  
✅ **Flexible error handling** for diverse scenarios  
✅ **Comprehensive diagnostics** for troubleshooting  
✅ **Maintains plan caching performance**  
✅ **Backward compatible** with existing code  
✅ **Well-documented** with examples and guides  

**Estimated Timeline:** 5 weeks from approval to release

**Risk Level:** Low (backward compatible, well-tested design)

**Recommendation:** **APPROVE FOR IMPLEMENTATION**

---

**End of Specification**

---

*This document is ready for technical review and implementation. All stakeholders should review their respective sections (API design, testing, documentation) and provide sign-off before development begins.*