# Feature Specification: Materialization Options, Diagnostics, and Error Resolution

**Version:** 4.0  
**Date:** 2025-10-09  
**Status:** Ready for Implementation  
**Author:** Architecture Team

---

## 1. Executive Summary

This specification defines a comprehensive configuration and diagnostics system for the ObjectMaterializer framework. It introduces:

1.  **Separation of compilation-time and runtime options** to maintain caching performance.
2.  **An ambient context pattern** for passing runtime options to converters, ensuring zero overhead for cached plans.
3.  **Flexible error resolution strategies** for handling conversion failures.
4.  **Configurable diagnostic reporting** via pluggable sinks with verbosity controlled at the sink level.
5.  **Simplified conversion strictness levels** to balance flexibility and safety.

The design maintains backward compatibility while enabling advanced scenarios like troubleshooting, data quality auditing, and production monitoring.

---

## 2. Core Principles

### 2.1 Performance First
- **Compilation options** (culture, formats) affect plan caching.
- **Runtime options** (sinks, error handling) are passed explicitly as parameters and do not affect caching.
- **Conversion logic** is centralized in `TypeConverter` class with ambient context using `AsyncLocal<T>` for async-safety.
- **Sink verbosity filtering** happens at the sink level only (removed from MaterializationOptions).
- **Context management** uses RAII pattern (`ConversionContextGuard`) to ensure proper cleanup.

### 2.2 Deterministic Behavior
- **No retry logic** - conversion either succeeds or fails deterministically.
- **Failures indicate misconfiguration or invalid data**, not transient issues.
- **All error resolution strategies are explicit and predictable**.

### 2.3 Separation of Concerns
- **Compilation options** (culture, formats) affect plan caching.
- **Runtime options** (sinks, error handling) are passed explicitly as parameters and do not affect caching.
- **Conversion logic** is centralized in the `TypeConverter` class with ambient context using AsyncLocal, and a simplified public `Convert(object? value, Type targetType)` API that **returns sentinels** for SkipProperty/SkipRow. **Internally, `SkipPropertyException` and `SkipRowException` are thrown by error handlers and caught within `Convert()` to return the appropriate sentinel.**
- **Sink verbosity filtering** happens at the sink level only (removed from MaterializationOptions).

### 2.4 Explicit Over Implicit
- **Most dependencies are passed as parameters** - MaterializationOptions flows explicitly through the call chain.
- **Conversion context uses AsyncLocal<T>** for ambient flow to keep expression trees clean while maintaining async-safety.
- **Context lifetime is managed via RAII pattern** (`ConversionContextGuard`) to prevent leaks.
- **Easy to test** - context can be set/cleared explicitly in tests.
- **Async-safe by design** - `AsyncLocal<T>` flows across await boundaries, unlike `ThreadStatic`.

**Trade-off Rationale:**
We use ambient context for member-level details (memberName, rowIndex) because:
1. Expression trees would become significantly more complex with 4+ parameters
2. `AsyncLocal<T>` provides async-safety without performance penalty
3. Context is always paired with explicit MaterializationOptions parameter
4. RAII guard ensures deterministic cleanup

---

## 3. Architecture Overview

### 3.1 Options Hierarchy

```
MaterializationOptions (runtime, passed explicitly)
├── CompilationOptions (affects caching)
│   ├── Culture
│   ├── AllowThousandsSeparators
│   ├── DateTimeFormats
│   └── CaseInsensitiveHeaders
│
├── ConversionOptions (runtime behavior)
│   ├── AllowChangeTypeFallback
│   ├── TrimStrings
│   ├── CaseInsensitiveEnums
│   └── NullStringBehavior
│
├── DiagnosticOptions (observability)
│   └── Sink (with verbosity configured on the sink itself)
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
ObjectMaterializer.Create<T>(schema, values, options, rowIndex)
    ↓
MemberMaterializer.FeedUsingSchema(obj, schema, values, options, rowIndex)
    │
    ├─ MemberMaterializationPlan<T> (cached by CompilationOptions)
    │   ↓
    │   Compiled Setter: member.Set(obj, value, options) // ✅ Options passed explicitly
    │       ↓
    │       TypeConverter.Convert(value, targetType)  // uses ambient context and sentinels
    │           ↓
    │           [Debug Reporting] → options.Sink?.Report(debug)
    │           ↓
    │           [Error Handling] → ErrorResolution strategy
    │           ↓
    │           Converted Value or throw SkipRowException
    │       ↓
    │   Expression Tree assigns converted value
    ↓
Converted Object or null (if SkipRowSentinel was thrown)
```

---

## 4. API Definitions

### 4.1 Enumerations

#### 4.1.1 ErrorResolution

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

#### 4.1.2 SinkVerbosity

```csharp
/// <summary>
/// Controls the verbosity level of diagnostic reporting to sinks.
/// This is independent of ConversionStrictness and is configured per-sink.
/// </summary>
public enum SinkVerbosity
{
    /// <summary>
    /// Report only errors (conversions that failed completely).
    /// - Minimal overhead (~2-5%)
    /// - Default for production environments
    /// </summary>
    ErrorsOnly = 0,
    
    /// <summary>
    /// Report warnings (recoverable issues) and errors.
    /// - Low overhead (~5-10%)
    /// - Useful for data quality auditing
    /// </summary>
    WarningsAndErrors = 1,
    
    /// <summary>
    /// Report all conversions, including successful ones.
    /// - Moderate overhead (~10-20%)
    /// - Useful for development and validation
    /// </summary>
    SuccessAndFailures = 2,
    
    /// <summary>
    /// Report every conversion attempt, including intermediate failures.
    /// - High overhead (~30-50%)
    /// - Useful for troubleshooting and deep debugging
    /// </summary>
    Debug = 3
}
```

#### 4.1.3 DiagnosticSeverity

```csharp
/// <summary>
/// Severity level of a materialization diagnostic.
/// </summary>
public enum DiagnosticSeverity
{
    /// <summary>
    /// Trace-level information (individual conversion attempts).
    /// </summary>
    Trace = 0,
    
    /// <summary>
    /// Informational message (successful conversions).
    /// </summary>
    Info = 1,
    
    /// <summary>
    /// Warning about a recoverable issue.
    /// </summary>
    Warning = 2,
    
    /// <summary>
    /// Error (conversion failed completely).
    /// </summary>
    Error = 3
}
```

#### 4.1.4 NullStringBehavior

```csharp
/// <summary>
/// Defines how null or empty strings are handled during conversion.
/// </summary>
public enum NullStringBehavior
{
    /// <summary>
    /// Treats null or empty strings as a conversion error.
    /// </summary>
    Error = 0,
    
    /// <summary>
    /// Converts null/empty strings to default(T).
    /// - For string: null
    /// - For string?: null
    /// - For int: 0
    /// - For int?: null
    /// </summary>
    ConvertToDefault = 1,
    
    /// <summary>
    /// Preserves empty strings ("") for string and string? types.
    /// For all other types, behaves like ConvertToDefault.
    /// 
    /// Examples:
    /// - string property + "" input → "" (preserved)
    /// - string? property + "" input → "" (preserved)
    /// - int property + "" input → 0 (default)
    /// - int? property + "" input → null (default)
    /// </summary>
    PreserveEmptyStrings = 2
}
```


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
/// Properties use simple auto-property setters to allow natural object initializer syntax.
/// All validation is performed in the Validate() method, which should be called before use.
/// </summary>
public sealed class MaterializationOptions
{
    /// <summary>
    /// Default options instance (used when no options provided).
    /// </summary>
    public static MaterializationOptions Default { get; } = new();
    
    // ===== Compilation Options (affect caching) =====
    
    /// <summary>
    /// Options that affect expression tree compilation and plan caching.
    /// Changing these values requires recompiling the materialization plan.
    /// Default: new CompilationOptions()
    /// </summary>
    public CompilationOptions Compilation { get; set; } = new();
    
    // ===== Conversion Behavior (runtime) =====
    
    /// <summary>
    /// Allow Convert.ChangeType as a fallback conversion strategy.
    /// This is slower and more permissive than standard parsing.
    /// Default: false
    /// </summary>
    public bool AllowChangeTypeFallback { get; set; } = false;
    
    /// <summary>
    /// Trim leading/trailing whitespace from strings before conversion.
    /// Recommended for CSV/Excel imports.
    /// Default: true
    /// </summary>
    public bool TrimStrings { get; set; } = true;
    
    /// <summary>
    /// Parse enum values case-insensitively.
    /// Default: true
    /// </summary>
    public bool CaseInsensitiveEnums { get; set; } = true;
    
    /// <summary>
    /// How to handle null or empty strings during conversion.
    /// Default: ConvertToDefault
    /// </summary>
    public NullStringBehavior NullStringBehavior { get; set; } = NullStringBehavior.ConvertToDefault;
    
    // ===== Debug Reporting =====
    
    /// <summary>
    /// Sink for diagnostic messages. Set to null to disable diagnostics (minimal overhead).
    /// Verbosity is configured on the sink itself, not here.
    /// Default: null (no diagnostics)
    /// </summary>
    public IMaterializationSink? Sink { get; set; }
    
    // ===== Error Handling =====
    
    /// <summary>
    /// Default error resolution strategy.
    /// Can be overridden per-property via OnError callback.
    /// Default: Throw (fail-fast)
    /// </summary>
    /// <remarks>
    /// This is a simple auto-property to allow natural object initializer syntax:
    /// <code>
    /// var options = new MaterializationOptions
    /// {
    ///     DefaultErrorResolution = ErrorResolution.UseCustomValue,
    ///     CustomValueProvider = ctx => -1  // Can be set in any order
    /// };
    /// </code>
    /// Validation of the relationship between DefaultErrorResolution and CustomValueProvider
    /// is performed in the Validate() method.
    /// </remarks>
    public ErrorResolution DefaultErrorResolution { get; set; } = ErrorResolution.Throw;
    
    /// <summary>
    /// Callback invoked when a conversion error occurs.
    /// Allows per-property error handling logic.
    /// Return value overrides DefaultErrorResolution for that specific error.
    /// Default: null (use DefaultErrorResolution)
    /// </summary>
    /// <remarks>
    /// This callback is NOT invoked during Validate(). It is only called at runtime
    /// when an actual conversion error occurs.
    /// 
    /// Example - Skip rows with critical field failures:
    /// <code>
    /// OnError = ctx => ctx.MemberName == "Id" 
    ///     ? ErrorResolution.SkipRow 
    ///     : ErrorResolution.UseDefault
    /// </code>
    /// </remarks>
    public Func<MaterializationErrorContext, ErrorResolution>? OnError { get; set; }
    
    /// <summary>
    /// Provides custom values when ErrorResolution.UseCustomValue is used.
    /// Required if DefaultErrorResolution is UseCustomValue.
    /// If using OnError, this is only required if OnError can return UseCustomValue.
    /// Default: null
    /// </summary>
    /// <remarks>
    /// Example - Provide fallback values by field name:
    /// <code>
    /// CustomValueProvider = ctx => ctx.MemberName switch
    /// {
    ///     "Age" => 0,
    ///     "Country" => "Unknown",
    ///     _ => null
    /// }
    /// </code>
    /// </remarks>
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
    
    // ===== Validation =====
    private bool _validated = false;
    
    /// <summary>
    /// Validates options if not already validated.
    /// Thread-safe via Interlocked.CompareExchange.
    /// </summary>
    internal void EnsureValidated()
    {
        if (_validated) return; // ✅ Fast path
        
        lock (this) // Rare path (first call only)
        {
            if (_validated) return; // Double-check
            Validate();
            _validated = true;
        }
    }

    /// <summary>
    /// Validates the options configuration.
    /// Throws InvalidOperationException if configuration is invalid.
    /// </summary>
    /// <remarks>
    /// This method performs STATIC validation only. It does not invoke callbacks
    /// like OnError or CustomValueProvider, as those may have side effects.
    /// 
    /// Runtime validation (e.g., checking if OnError returns UseCustomValue without
    /// a provider) happens during actual conversion in TypeConverter.HandleConversionError.
    /// 
    /// Call this method explicitly before using options, or use MaterializationOptionsBuilder.Build()
    /// which calls it automatically.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    /// Thrown when:
    /// - DefaultErrorResolution is UseCustomValue but CustomValueProvider is null
    /// - Compilation.DateTimeFormats contains null or whitespace entries
    /// - Compilation.Culture is null
    /// </exception>
    public void Validate()
    {
        // Validate error handling configuration
        if (DefaultErrorResolution == ErrorResolution.UseCustomValue && CustomValueProvider == null)
        {
            throw new InvalidOperationException(
                "CustomValueProvider must be set when DefaultErrorResolution is UseCustomValue. " +
                "Either set CustomValueProvider, or use a different DefaultErrorResolution.");
        }
        
        // Validate compilation options
        if (Compilation.Culture == null)
        {
            throw new InvalidOperationException(
                "Compilation.Culture cannot be null. Use CultureInfo.InvariantCulture for culture-neutral parsing.");
        }
        
        if (Compilation.DateTimeFormats.Any(f => string.IsNullOrWhiteSpace(f)))
        {
            throw new InvalidOperationException(
                "Compilation.DateTimeFormats cannot contain null or whitespace entries. " +
                "Remove invalid entries or use an empty array to use culture defaults.");
        }
        
        // Performance warnings (non-fatal)
        if (AllowChangeTypeFallback && Sink == null)
        {
            Trace.TraceWarning(
                "[MaterializationOptions] AllowChangeTypeFallback is enabled without a diagnostic sink. " +
                "Consider enabling diagnostics to monitor fallback usage and identify performance bottlenecks.");
        }
		
    }

    // ===== Static Factory Methods =====
    // using Microsoft.Extensions.Logging; // Required namespace
    /// <summary>
    /// Production-ready configuration: minimal overhead, log errors only, continue on failure.
    /// </summary>
    /// <param name="logger">ILogger instance for error reporting</param>
    /// <returns>Configured MaterializationOptions for production use</returns>
    /// <remarks>
    /// Configuration:
    /// - AllowChangeTypeFallback: false (strict parsing)
    /// - DefaultErrorResolution: UseDefault (graceful degradation)
    /// - Sink: MicrosoftLoggerSink with ErrorsOnly verbosity (~2-5% overhead)
    /// - TrimStrings: true (handle whitespace in data)
    /// </remarks>
    public static MaterializationOptions ForProduction(ILogger logger)
    {
        if (logger == null) throw new ArgumentNullException(nameof(logger));
        
        return new MaterializationOptions
        {
            AllowChangeTypeFallback = false,
            DefaultErrorResolution = ErrorResolution.UseDefault,
            Sink = new MicrosoftLoggerSink(logger, SinkVerbosity.ErrorsOnly)
        };
    }

    /// <summary>
    /// Development configuration: strict parsing, fail-fast, verbose output.
    /// </summary>
    /// <returns>Configured MaterializationOptions for development use</returns>
    /// <remarks>
    /// Configuration:
    /// - AllowChangeTypeFallback: false (strict parsing)
    /// - DefaultErrorResolution: Throw (fail-fast for debugging)
    /// - Sink: ConsoleSink with SuccessAndFailures verbosity (~10-20% overhead)
    /// - Useful for identifying data quality issues early
    /// </remarks>
    public static MaterializationOptions ForDevelopment() => new()
    {
        AllowChangeTypeFallback = false,
        DefaultErrorResolution = ErrorResolution.Throw,
        Sink = new ConsoleSink(SinkVerbosity.SuccessAndFailures)
    };

    /// <summary>
    /// Validation configuration: strict parsing, treat empty strings as errors, fail-fast.
    /// </summary>
    /// <returns>Configured MaterializationOptions for data validation</returns>
    /// <remarks>
    /// Configuration:
    /// - AllowChangeTypeFallback: false (strict parsing)
    /// - NullStringBehavior: Error (reject empty/null strings)
    /// - DefaultErrorResolution: Throw (fail on any error)
    /// - Sink: ConsoleSink with ErrorsOnly verbosity
    /// - Use for validating data before import
    /// </remarks>
    public static MaterializationOptions ForValidation() => new()
    {
        AllowChangeTypeFallback = false,
        NullStringBehavior = NullStringBehavior.Error,
        DefaultErrorResolution = ErrorResolution.Throw,
        Sink = new ConsoleSink(SinkVerbosity.ErrorsOnly)
    };

    /// <summary>
    /// Auditing configuration: collect all warnings and errors for analysis.
    /// Returns the sink instance via out parameter for easy access to diagnostics.
    /// </summary>
    /// <param name="sink">CollectionSink instance for retrieving diagnostics</param>
    /// <returns>Configured MaterializationOptions for auditing</returns>
    /// <remarks>
    /// Configuration:
    /// - AllowChangeTypeFallback: false (strict parsing)
    /// - DefaultErrorResolution: UseDefault (continue processing)
    /// - Sink: CollectionSink with WarningsAndErrors verbosity (~5-10% overhead)
    /// 
    /// Example usage:
    /// <code>
    /// var options = MaterializationOptions.ForAuditing(out var sink);
    /// var results = ProcessData(data, options);
    /// var errors = sink.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error);
    /// </code>
    /// </remarks>
    public static MaterializationOptions ForAuditing(out CollectionSink sink)
    {
        sink = new CollectionSink(SinkVerbosity.WarningsAndErrors);
        return new MaterializationOptions
        {
            AllowChangeTypeFallback = false,
            Sink = sink,
            DefaultErrorResolution = ErrorResolution.UseDefault
        };
    }

    /// <summary>
    /// CSV import configuration: lenient parsing, trim strings, preserve empty strings.
    /// </summary>
    /// <param name="logger">Optional ILogger for warning/error reporting</param>
    /// <returns>Configured MaterializationOptions for CSV import</returns>
    /// <remarks>
    /// Configuration:
    /// - TrimStrings: true (handle whitespace in CSV data)
    /// - NullStringBehavior: PreserveEmptyStrings (distinguish null vs empty)
    /// - DefaultErrorResolution: UseDefault (continue on errors)
    /// - Sink: MicrosoftLoggerSink with WarningsAndErrors if logger provided
    /// - Recommended for importing user-generated CSV files
    /// </remarks>
    public static MaterializationOptions ForCsvImport(ILogger? logger = null) => new()
    {
        TrimStrings = true,
        NullStringBehavior = NullStringBehavior.PreserveEmptyStrings,
        DefaultErrorResolution = ErrorResolution.UseDefault,
        Sink = logger != null
            ? new MicrosoftLoggerSink(logger, SinkVerbosity.WarningsAndErrors)
            : null
    };

    /// <summary>
    /// Excel import configuration: similar to CSV but with stricter date parsing.
    /// </summary>
    /// <param name="logger">Optional ILogger for warning/error reporting</param>
    /// <returns>Configured MaterializationOptions for Excel import</returns>
    /// <remarks>
    /// Configuration:
    /// - TrimStrings: true (handle whitespace)
    /// - NullStringBehavior: ConvertToDefault (Excel null cells → default values)
    /// - DefaultErrorResolution: UseDefault (continue on errors)
    /// - DateTimeFormats: Common Excel date formats (yyyy-MM-dd, MM/dd/yyyy, dd/MM/yyyy)
    /// - Sink: MicrosoftLoggerSink with WarningsAndErrors if logger provided
    /// </remarks>
    public static MaterializationOptions ForExcelImport(ILogger? logger = null) => new()
    {
        TrimStrings = true,
        NullStringBehavior = NullStringBehavior.ConvertToDefault,
        DefaultErrorResolution = ErrorResolution.UseDefault,
        Compilation = new CompilationOptions
        {
            DateTimeFormats = new[] { "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy" }
        },
        Sink = logger != null
            ? new MicrosoftLoggerSink(logger, SinkVerbosity.WarningsAndErrors)
            : null
    };

    /// <summary>
    /// API deserialization: strict parsing, fail-fast on errors.
    /// </summary>
    /// <returns>Configured MaterializationOptions for API request deserialization</returns>
    /// <remarks>
    /// Configuration:
    /// - TrimStrings: false (preserve exact input)
    /// - NullStringBehavior: Error (reject empty strings)
    /// - DefaultErrorResolution: Throw (fail-fast for invalid requests)
    /// - AllowChangeTypeFallback: false (strict type checking)
    /// - No sink (minimal overhead, exceptions provide error details)
    /// - Use for deserializing API request bodies where strict validation is required
    /// </remarks>
    public static MaterializationOptions ForApiDeserialization() => new()
    {
        TrimStrings = false,
        NullStringBehavior = NullStringBehavior.Error,
        DefaultErrorResolution = ErrorResolution.Throw,
        AllowChangeTypeFallback = false
    };
	
	 /// <summary>
    /// Creates a validated copy of these options.
    /// Throws if configuration is invalid.
    /// Use this when you want fail-fast validation at construction time.
    /// </summary>
    public MaterializationOptions Validated()
    {
        Validate(); // Throws if invalid
        return this; // Return self for fluent chaining
    }
    
    /// <summary>
    /// Creates a validated copy with modifications.
    /// Useful for deriving configurations from base templates.
    /// </summary>
    public MaterializationOptions With(Action<MaterializationOptions> configure)
    {
        var copy = new MaterializationOptions
        {
            Compilation = new CompilationOptions
            {
                Culture = this.Compilation.Culture,
                AllowThousandsSeparators = this.Compilation.AllowThousandsSeparators,
                DateTimeFormats = this.Compilation.DateTimeFormats,
                CaseInsensitiveHeaders = this.Compilation.CaseInsensitiveHeaders
            },
            AllowChangeTypeFallback = this.AllowChangeTypeFallback,
            TrimStrings = this.TrimStrings,
            CaseInsensitiveEnums = this.CaseInsensitiveEnums,
            NullStringBehavior = this.NullStringBehavior,
            Sink = this.Sink,
            DefaultErrorResolution = this.DefaultErrorResolution,
            OnError = this.OnError,
            CustomValueProvider = this.CustomValueProvider
        };
        
        configure(copy);
        copy.Validate();
        return copy;
    }
}
```

**Validation Usage :**
```csharp
// Fail-fast validation
var options = new MaterializationOptions
{
    DefaultErrorResolution = ErrorResolution.UseCustomValue
    // Oops, forgot CustomValueProvider
}.Validated(); // ❌ Throws immediately

// Derive from template
var baseOptions = MaterializationOptions.ForProduction(logger);
var strictOptions = baseOptions.With(opt => 
{
    opt.DefaultErrorResolution = ErrorResolution.Throw;
    opt.NullStringBehavior = NullStringBehavior.Error;
});
```
#### 4.2.3 MaterializationOptionsBuilder

```csharp
/// <summary>
/// Fluent builder for constructing MaterializationOptions with validation.
/// Ensures options are validated before use and prevents reuse of builder instances.
/// </summary>
/// <remarks>
/// Example usage:
/// <code>
/// var options = new MaterializationOptionsBuilder()
///     .ForCsvImport(logger)
///     .WithCriticalFields("Id", "Email")
///     .WithSink(new ConsoleSink(SinkVerbosity.ErrorsOnly))
///     .Build();
/// </code>
/// </remarks>
public sealed class MaterializationOptionsBuilder
{
    private readonly MaterializationOptions _options = new();
    private bool _built = false;

    /// <summary>
    /// Ensures the builder hasn't been used yet.
    /// Builders are single-use to prevent accidental reuse and mutation.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown if Build() has already been called</exception>
    private void EnsureNotBuilt()
    {
        if (_built)
        {
            throw new InvalidOperationException(
                "This builder has already been used to create a MaterializationOptions instance. " +
                "Create a new builder for each options instance.");
        }
    }
    
    /// <summary>
    /// Sets the default error resolution strategy.
    /// </summary>
    /// <param name="resolution">Error resolution strategy to use</param>
    /// <returns>This builder for method chaining</returns>
    public MaterializationOptionsBuilder WithErrorResolution(ErrorResolution resolution)
    {
        EnsureNotBuilt();
        _options.DefaultErrorResolution = resolution;
        return this;
    }
    
    /// <summary>
    /// Sets the custom value provider for UseCustomValue error resolution.
    /// </summary>
    /// <param name="provider">Function that provides custom values based on error context</param>
    /// <returns>This builder for method chaining</returns>
    public MaterializationOptionsBuilder WithCustomValueProvider(
        Func<MaterializationErrorContext, object?> provider)
    {
        EnsureNotBuilt();
        _options.CustomValueProvider = provider;
        return this;
    }
    
    /// <summary>
    /// Adds a sink to the options. Can be called multiple times to create a composite sink.
    /// </summary>
    /// <param name="sink">Sink to add for diagnostic reporting</param>
    /// <returns>This builder for method chaining</returns>
    /// <remarks>
    /// Calling this method multiple times automatically creates a CompositeSink:
    /// <code>
    /// builder
    ///     .WithSink(new ConsoleSink(SinkVerbosity.ErrorsOnly))
    ///     .WithSink(new CollectionSink(SinkVerbosity.SuccessAndFailures))
    ///     // Results in a CompositeSink containing both sinks
    /// </code>
    /// </remarks>
    public MaterializationOptionsBuilder WithSink(IMaterializationSink sink)
    {
        EnsureNotBuilt();
        
        if (sink == null)
            throw new ArgumentNullException(nameof(sink));
        
        if (_options.Sink == null)
        {
            _options.Sink = sink;
        }
        else if (_options.Sink is CompositeSink composite)
        {
            composite.AddSink(sink);
        }
        else
        {
            _options.Sink = new CompositeSink(_options.Sink, sink);
        }
        
        return this;
    }

    /// <summary>
    /// Configures critical fields that will trigger SkipRow if they fail conversion.
    /// Other fields will use the DefaultErrorResolution.
    /// </summary>
    /// <param name="fields">Names of critical fields (case-insensitive)</param>
    /// <returns>This builder for method chaining</returns>
    /// <remarks>
    /// Example - Skip entire row if Id or Email conversion fails:
    /// <code>
    /// builder
    ///     .WithCriticalFields("Id", "Email")
    ///     .WithErrorResolution(ErrorResolution.UseDefault) // For non-critical fields
    /// </code>
    /// </remarks>
    public MaterializationOptionsBuilder WithCriticalFields(params string[] fields)
    {
        EnsureNotBuilt();
        
        if (fields == null || fields.Length == 0)
            throw new ArgumentException("At least one critical field must be specified", nameof(fields));
        
        var criticalSet = new HashSet<string>(fields, StringComparer.OrdinalIgnoreCase);
        _options.OnError = ctx => criticalSet.Contains(ctx.MemberName)
            ? ErrorResolution.SkipRow
            : _options.DefaultErrorResolution;
        
        return this;
    }

    /// <summary>
    /// Configures for CSV import: trim strings, preserve empty strings, lenient parsing.
    /// </summary>
    /// <param name="logger">Optional ILogger for warning/error reporting</param>
    /// <returns>This builder for method chaining</returns>
    public MaterializationOptionsBuilder ForCsvImport(ILogger? logger = null)
    {
        EnsureNotBuilt();
        _options.TrimStrings = true;
        _options.NullStringBehavior = NullStringBehavior.PreserveEmptyStrings;
        _options.DefaultErrorResolution = ErrorResolution.UseDefault;
        
        if (logger != null)
        {
            WithSink(new MicrosoftLoggerSink(logger, SinkVerbosity.WarningsAndErrors));
        }
        
        return this;
    }

    /// <summary>
    /// Configures for Excel import: trim strings, stricter date parsing.
    /// </summary>
    /// <param name="logger">Optional ILogger for warning/error reporting</param>
    /// <returns>This builder for method chaining</returns>
    public MaterializationOptionsBuilder ForExcelImport(ILogger? logger = null)
    {
        EnsureNotBuilt();
        _options.TrimStrings = true;
        _options.NullStringBehavior = NullStringBehavior.ConvertToDefault;
        _options.DefaultErrorResolution = ErrorResolution.UseDefault;
        _options.Compilation.DateTimeFormats = new[] { "yyyy-MM-dd", "MM/dd/yyyy", "dd/MM/yyyy" };
        
        if (logger != null)
        {
            WithSink(new MicrosoftLoggerSink(logger, SinkVerbosity.WarningsAndErrors));
        }
        
        return this;
    }

    /// <summary>
    /// Sets the culture for number and date parsing.
    /// </summary>
    /// <param name="culture">Culture to use for parsing</param>
    /// <returns>This builder for method chaining</returns>
    public MaterializationOptionsBuilder WithCulture(CultureInfo culture)
    {
        EnsureNotBuilt();
        _options.Compilation.Culture = culture ?? throw new ArgumentNullException(nameof(culture));
        return this;
    }

    /// <summary>
    /// Sets whether to allow thousands separators in numeric parsing.
    /// </summary>
    /// <param name="allow">True to allow thousands separators (e.g., "1,234")</param>
    /// <returns>This builder for method chaining</returns>
    public MaterializationOptionsBuilder WithThousandsSeparators(bool allow = true)
    {
        EnsureNotBuilt();
        _options.Compilation.AllowThousandsSeparators = allow;
        return this;
    }

    /// <summary>
    /// Sets explicit DateTime format strings for parsing.
    /// </summary>
    /// <param name="formats">Format strings (e.g., "yyyy-MM-dd", "MM/dd/yyyy")</param>
    /// <returns>This builder for method chaining</returns>
    public MaterializationOptionsBuilder WithDateTimeFormats(params string[] formats)
    {
        EnsureNotBuilt();
        _options.Compilation.DateTimeFormats = formats ?? throw new ArgumentNullException(nameof(formats));
        return this;
    }

    /// <summary>
    /// Sets whether to trim strings before conversion.
    /// </summary>
    /// <param name="trim">True to trim leading/trailing whitespace</param>
    /// <returns>This builder for method chaining</returns>
    public MaterializationOptionsBuilder WithTrimStrings(bool trim = true)
    {
        EnsureNotBuilt();
        _options.TrimStrings = trim;
        return this;
    }

    /// <summary>
    /// Sets the null/empty string handling behavior.
    /// </summary>
    /// <param name="behavior">Behavior to use for null/empty strings</param>
    /// <returns>This builder for method chaining</returns>
    public MaterializationOptionsBuilder WithNullStringBehavior(NullStringBehavior behavior)
    {
        EnsureNotBuilt();
        _options.NullStringBehavior = behavior;
        return this;
    }

    /// <summary>
    /// Builds and validates the MaterializationOptions instance.
    /// This builder cannot be reused after calling Build().
    /// </summary>
    /// <returns>Validated MaterializationOptions instance</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if:
    /// - Build() has already been called on this builder
    /// - Options configuration is invalid (see MaterializationOptions.Validate())
    /// </exception>
    public MaterializationOptions Build()
    {
        EnsureNotBuilt();
        _built = true;
        _options.Validate(); // Throws if invalid
        return _options;
    }
}
```
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

### 4.4 Sink Interfaces

#### 4.4.1 IMaterializationSink
```csharp
/// <summary>
/// Indicates that a sink implementation is thread-safe and can be used
/// in parallel materialization scenarios.
/// </summary>
/// <remarks>
/// Thread-safe sinks can be used with <see cref="ObjectMaterializer.CreateBatchParallel{T}"/>
/// without external synchronization. Non-thread-safe sinks will throw an exception
/// if used in parallel scenarios.
/// 
/// <para><strong>Thread-Safe Sinks:</strong></para>
/// <list type="bullet">
///   <item><see cref="ConsoleSink"/> - Console.WriteLine is thread-safe</item>
///   <item><see cref="CollectionSink"/> - Uses ConcurrentBag internally</item>
///   <item><see cref="MicrosoftLoggerSink"/> - ILogger implementations are thread-safe</item>
///   <item><see cref="CompositeSink"/> - Thread-safe if all children are thread-safe</item>
/// </list>
/// 
/// <para><strong>Custom Sink Implementation:</strong></para>
/// <code>
/// [ThreadSafe] // Add this if your sink is thread-safe
/// public class MyCustomSink : MaterializationSinkBase
/// {
///     private readonly ConcurrentQueue&lt;MaterializationDiagnostic&gt; _queue = new();
///     
///     protected override void ReportCore(MaterializationDiagnostic diagnostic)
///     {
///         _queue.Enqueue(diagnostic); // Thread-safe operation
///     }
/// }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class ThreadSafeAttribute : Attribute
{
}

/// <summary>
/// Interface for receiving materialization diagnostics.
/// </summary>
/// <remarks>
/// Implementations must be thread-safe if they will be used with
/// <see cref="ObjectMaterializer.CreateBatchParallel{T}"/>.
/// Mark thread-safe implementations with <see cref="ThreadSafeAttribute"/>.
/// </remarks>
public interface IMaterializationSink
{
    /// <summary>
    /// Reports a diagnostic message.
    /// Implementations should be thread-safe if used in parallel scenarios.
    /// </summary>
    /// <param name="diagnostic">The diagnostic to report</param>
    void Report(MaterializationDiagnostic diagnostic);
}

// ===== Built-in Sink Implementations =====

/// <summary>
/// Thread-safe sink that writes diagnostics to the console.
/// Uses Console.WriteLine which is internally synchronized.
/// </summary>
[ThreadSafe]
public sealed class ConsoleSink : MaterializationSinkBase 
{ 
    // Implementation in Section 5.6
}

/// <summary>
/// Thread-safe sink that collects diagnostics in memory.
/// Uses ConcurrentBag for thread-safe collection.
/// </summary>
[ThreadSafe]
public sealed class CollectionSink : MaterializationSinkBase 
{ 
    // Implementation in Section 5.6
}

/// <summary>
/// Thread-safe sink that writes diagnostics to ILogger.
/// ILogger implementations are required to be thread-safe.
/// </summary>
[ThreadSafe]
public sealed class MicrosoftLoggerSink : MaterializationSinkBase 
{ 
    // Implementation in Section 5.6
}

/// <summary>
/// Composite sink that broadcasts to multiple child sinks.
/// Thread-safe only if all child sinks are thread-safe.
/// </summary>
/// <remarks>
/// This sink does NOT have the [ThreadSafe] attribute because its thread-safety
/// depends on its children. Use <see cref="ObjectMaterializer.IsThreadSafe"/> to check.
/// </remarks>
public sealed class CompositeSink : IMaterializationSink
{
    /// <summary>
    /// Read-only collection of child sinks.
    /// </summary>
    public IReadOnlyList<IMaterializationSink> Sinks { get; }
    
    // Implementation in Section 5.6
}
```
#### 4.4.2 MaterializationSinkBase
Add predicate-based filtering to sinks:

```csharp
/// <summary>
/// Base class for materialization sinks with built-in verbosity filtering.
/// </summary>
public abstract class MaterializationSinkBase : IMaterializationSink, IDisposable
{
    private readonly SinkVerbosity _verbosity;
	private readonly Func<MaterializationDiagnostic, bool>? _filter;

    /// <summary>
    /// Creates a sink with the specified verbosity level.
    /// </summary>
    protected MaterializationSinkBase(
        SinkVerbosity verbosity = SinkVerbosity.ErrorsOnly,
        Func<MaterializationDiagnostic, bool>? filter = null)
    {
        _verbosity = verbosity;
        _filter = filter;
    }

    /// <summary>
    /// Reports a diagnostic if it meets the verbosity threshold.
    /// </summary>
    public void Report(MaterializationDiagnostic diagnostic)
    {
        if (ShouldReport(diagnostic.Severity) && (_filter == null || _filter(diagnostic)))
        {
            ReportCore(diagnostic);
        }
    }

    /// <summary>
    /// Clears internal state and releases memory.
    /// The sink remains usable after calling Clear().
    /// </summary>
    public virtual void Clear() { }
    
    /// <summary>
    /// Disposes the sink and releases all resources.
    /// After disposal, the sink cannot be used.
    /// </summary>
    public virtual void Dispose() { }

    /// <summary>
    /// Determines if a diagnostic should be reported based on verbosity settings.
    /// </summary>
    private bool ShouldReport(DiagnosticSeverity severity)
    {
        return _verbosity switch
        {
            SinkVerbosity.ErrorsOnly => severity == DiagnosticSeverity.Error,
            SinkVerbosity.WarningsAndErrors => severity >= DiagnosticSeverity.Warning,
            SinkVerbosity.SuccessAndFailures => severity >= DiagnosticSeverity.Info,
            SinkVerbosity.Debug => true,
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
**Usage:**

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
##### 4.4.2.1 MaterializationOptions.ErrorHandling (Helper Class)

Add nested static class for common error handling patterns:

```csharp
public sealed partial class MaterializationOptions
{
    /// <summary>
    /// Helper methods for common error handling patterns.
    /// </summary>
    public static class ErrorHandling
    {
        /// <summary>
        /// Skip rows where critical fields fail, use defaults for optional fields.
        /// </summary>
        public static Func<MaterializationErrorContext, ErrorResolution> CriticalFieldsOnly(
            params string[] criticalFields)
        {
            var criticalSet = new HashSet<string>(criticalFields, StringComparer.OrdinalIgnoreCase);
            
            return ctx => criticalSet.Contains(ctx.MemberName)
                ? ErrorResolution.SkipRow
                : ErrorResolution.UseDefault;
        }
        
        /// <summary>
        /// Use custom values for specific fields, throw for others.
        /// </summary>
        public static Func<MaterializationErrorContext, ErrorResolution> CustomValuesFor(
            Dictionary<string, object?> fieldDefaults)
        {
            return ctx => fieldDefaults.ContainsKey(ctx.MemberName)
                ? ErrorResolution.UseCustomValue
                : ErrorResolution.Throw;
        }
        
        /// <summary>
        /// Skip rows where any field in the specified list fails.
        /// </summary>
        public static Func<MaterializationErrorContext, ErrorResolution> SkipRowOnAnyOf(
            params string[] fields)
        {
            var fieldSet = new HashSet<string>(fields, StringComparer.OrdinalIgnoreCase);
            return ctx => fieldSet.Contains(ctx.MemberName)
                ? ErrorResolution.SkipRow
                : ErrorResolution.UseDefault;
        }
    }
}
```

**Usage:**
```csharp
var options = new MaterializationOptions
{
    OnError = MaterializationOptions.ErrorHandling.CriticalFieldsOnly("Id", "Email"),
    CustomValueProvider = ctx => ctx.MemberName switch
    {
        "Age" => 0,
        "Country" => "Unknown",
        _ => null
    }
};
```
#### 4.3.3 Preset Configurations
Add common configuration presets as static properties:

```csharp
public sealed partial class MaterializationOptions
{
    /// <summary>
    /// Strict validation preset: fail-fast on any error.
    /// Equivalent to ForValidation() but without console sink.
    /// </summary>
    public static MaterializationOptions Strict { get; } = new()
    {
        AllowChangeTypeFallback = false,
        NullStringBehavior = NullStringBehavior.Error,
        DefaultErrorResolution = ErrorResolution.Throw,
        TrimStrings = false
    };
    
    /// <summary>
    /// Lenient preset: continue on errors, use defaults.
    /// Useful for importing user-generated data with quality issues.
    /// </summary>
    public static MaterializationOptions Lenient { get; } = new()
    {
        AllowChangeTypeFallback = true,
        NullStringBehavior = NullStringBehavior.ConvertToDefault,
        DefaultErrorResolution = ErrorResolution.UseDefault,
        TrimStrings = true,
        CaseInsensitiveEnums = true
    };
}
```

**Usage:**

```csharp
// Quick strict validation
var result = ObjectMaterializer.Create<Person>(schema, values, MaterializationOptions.Strict);

// Lenient import with logging
var options = MaterializationOptions.Lenient.With(opt => 
{
    opt.Sink = new MicrosoftLoggerSink(logger, SinkVerbosity.WarningsAndErrors);
});
```
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
#### 4.5.2 MemberSetter
```csharp
/// <summary>
/// Represents a compiled setter for a single property or field.
/// Encapsulates the conversion and assignment logic for one member of type T.
/// </summary>
/// <typeparam name="T">The target type containing this member</typeparam>
public readonly struct MemberSetter
{
    /// <summary>
    /// Name of the property or field.
    /// Used for schema matching and diagnostic reporting.
    /// </summary>
    public readonly string Name;
    
    /// <summary>
    /// Order index from [Order] attribute, or -1 if not specified.
    /// Used for positional materialization (FeedOrdered).
    /// </summary>
    public readonly int OrderIndex;
    
    /// <summary>
    /// The target type of this member (e.g., int, string, DateTime?).
    /// Used for diagnostic reporting and runtime type checks.
    /// </summary>
    public readonly Type TargetType;
    
    /// <summary>
    /// Compiled setter function that converts and assigns a value to this member.
    /// </summary>
    /// <param name="obj">The object instance to modify</param>
    /// <param name="value">The raw value to convert and assign (typically from CSV/database)</param>
    /// <param name="options">Runtime materialization options (error handling, sinks, etc.)</param>
    /// <returns>
    /// The converted value, or a sentinel object:
    /// - <see cref="TypeConverter.SkipPropertySentinel"/> if property should be skipped
    /// - <see cref="TypeConverter.SkipRowSentinel"/> if entire row should be skipped
    /// - Actual converted value on success
    /// </returns>
    /// <remarks>
    /// This delegate is generated via expression tree compilation and includes:
    /// <list type="bullet">
    ///   <item>Type conversion logic (string → int, etc.)</item>
    ///   <item>Null handling based on <see cref="MaterializationOptions.NullStringBehavior"/></item>
    ///   <item>Error resolution (UseDefault, SkipProperty, SkipRow, etc.)</item>
    ///   <item>Diagnostic reporting via <see cref="MaterializationOptions.Sink"/></item>
    /// </list>
    /// 
    /// <para><strong>Sentinel Return Values:</strong></para>
    /// The return value is checked via <see cref="object.ReferenceEquals"/> in the materializer:
    /// <code>
    /// var result = member.Set(obj, value, options);
    /// if (ReferenceEquals(result, TypeConverter.SkipRowSentinel))
    ///     return default; // Skip entire row
    /// if (ReferenceEquals(result, TypeConverter.SkipPropertySentinel))
    ///     continue; // Skip this property only
    /// // Otherwise, assignment succeeded
    /// </code>
    /// 
    /// <para><strong>Performance:</strong></para>
    /// This delegate is compiled once per type and cached. Invocation overhead is ~5-10ns
    /// (comparable to direct property access), making it suitable for high-throughput scenarios.
    /// </remarks>
    public readonly Func<T, object?, MaterializationOptions, object?> Set;
    
    /// <summary>
    /// Creates a new MemberSetter instance.
    /// </summary>
    /// <param name="name">Property or field name</param>
    /// <param name="orderIndex">Order attribute value, or -1 if not specified</param>
    /// <param name="targetType">The type of this member (e.g., typeof(int))</param>
    /// <param name="set">Compiled setter delegate</param>
    public MemberSetter(
        string name, 
        int orderIndex,
        Type targetType,
        Func<T, object?, MaterializationOptions, object?> set)
    {
        Name = name;
        OrderIndex = orderIndex;
        TargetType = targetType;
        Set = set;
    }
    
    /// <summary>
    /// Returns a string representation for debugging.
    /// </summary>
    public override string ToString() 
        => $"{Name} ({TargetType.Name})" + (OrderIndex >= 0 ? $" [Order={OrderIndex}]" : "");
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
        : base($"Row skipped due to error in member '{context.MemberName}'")
    {
        Context = context;
    }
}


// In FeedUsingSchema:
catch (SkipRowException ex)
{
    if (options.Sink != null)
    {
        options.Sink.Report(new MaterializationDiagnostic
        {
            Severity = DiagnosticSeverity.Warning,
            RowIndex = rowIndex,
            MemberName = ex.Context.MemberName, // ✅ Get from exception
            // ...
        });
    }
    throw; // or return default
}

```

---

#### 4.5.4 SkipPropertyException (Internal) 

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

#### 4.5.5 ConversionException

```csharp
/// <summary>
/// Exception thrown when a type conversion fails.
/// Used internally before error resolution is applied.
/// </summary>
internal sealed class ConversionException : Exception
{
    public object? Value { get; }
    public Type TargetType { get; }
    
    public ConversionException(string message, object? value, Type targetType)
        : base(message)
    {
        Value = value;
        TargetType = targetType;
    }
}
```

---
### 4.6 The TypeConverter API
```csharp
/// <summary>
/// Central type conversion engine (internal use only).
/// </summary>
internal static class TypeConverter
{
    /// <summary>
    /// Sentinel object indicating a property should be skipped.
    /// Checked via ReferenceEquals in expression trees.
    /// </summary>
    public static readonly object SkipPropertySentinel;
    
    /// <summary>
    /// Sentinel object indicating the entire row should be skipped.
    /// Checked via ReferenceEquals in expression trees.
    /// </summary>
    public static readonly object SkipRowSentinel;
    
    /// <summary>
    /// RAII guard for setting conversion context.
    /// Must be used in a using statement.
    /// </summary>
    public readonly struct ConversionContextGuard : IDisposable
    {
        public ConversionContextGuard(
            MaterializationOptions options, 
            string? memberName, 
            int? rowIndex);
        
        public void Dispose();
    }
}
```
### 4.7 ObjectMaterializer API
```csharp
/// <summary>
/// Batch materialization APIs for processing multiple rows.
/// </summary>
public static class ObjectMaterializer
{
    // ... existing Create methods ...
    
    /// <summary>
    /// Materializes a batch of rows sequentially (lazy evaluation).
    /// Skipped rows (ErrorResolution.SkipRow) are omitted from results.
    /// </summary>
    public static IEnumerable<T> CreateBatch<T>(
        string[] schema,
        IEnumerable<object[]> rows,
        MaterializationOptions? options = null,
        int startRowIndex = 0);
    
    /// <summary>
    /// Materializes rows from an async stream (async-safe).
    /// </summary>
    public static IAsyncEnumerable<T> CreateStream<T>(
        string[] schema,
        IAsyncEnumerable<object[]> rows,
        MaterializationOptions? options = null,
        int startRowIndex = 0,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Materializes rows in parallel (requires thread-safe sink).
    /// </summary>
    public static IReadOnlyList<T> CreateBatchParallel<T>(
        string[] schema,
        IReadOnlyList<object[]> rows,
        MaterializationOptions? options = null,
        int startRowIndex = 0,
        int degreeOfParallelism = -1);

    /// <summary>
    /// Checks if a sink is thread-safe for use with CreateBatchParallel.
    /// </summary>
    /// <param name="sink">The sink to check</param>
    /// <returns>True if the sink can be safely used in parallel scenarios</returns>
    /// <remarks>
    /// A sink is considered thread-safe if:
    /// <list type="bullet">
    ///   <item>It has the [ThreadSafe] attribute, OR</item>
    ///   <item>It's a built-in thread-safe sink (ConsoleSink, CollectionSink, MicrosoftLoggerSink), OR</item>
    ///   <item>It's a CompositeSink where all children are thread-safe</item>
    /// </list>
    /// </remarks>
    public static bool IsThreadSafe(IMaterializationSink? sink);
}
```
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
### 5.2 The TypeConverter Class

```csharp
internal static class TypeConverter
{
    // ==========================================
    // SECTION 1: SENTINEL OBJECTS (PUBLIC)
    // ==========================================
    
    /// <summary>
    /// Sentinel object indicating a property should be skipped.
    /// Checked via ReferenceEquals in expression trees.
    /// </summary>
    public static readonly object SkipPropertySentinel = new();
    
    /// <summary>
    /// Sentinel object indicating the entire row should be skipped.
    /// Checked via ReferenceEquals in expression trees.
    /// </summary>
    public static readonly object SkipRowSentinel = new();

    // ==========================================
    // SECTION 2: AMBIENT CONTEXT (ASYNC-SAFE)
    // ==========================================
    
    /// <summary>
    /// Ambient context stack for nested conversions.
    /// Uses AsyncLocal for async-safety and thread isolation.
    /// 
    /// Performance: ~5-10ns per Push/Pop operation.
    /// For 1M rows × 10 columns: ~0.5-1s total overhead (0.1% of import time).
    /// </summary>
    private static readonly AsyncLocal<Stack<ConversionContext>?> _contextStack = new();

    private sealed record ConversionContext(
        MaterializationOptions Options,
        string? MemberName,
        int? RowIndex);

    /// <summary>
    /// Pushes a new conversion context onto the stack.
    /// Supports nested conversions (e.g., complex object graphs).
    /// </summary>
    public static void PushContext(MaterializationOptions options, string? memberName, int? rowIndex)
    {
        _contextStack.Value ??= new Stack<ConversionContext>();
        _contextStack.Value.Push(new ConversionContext(options, memberName, rowIndex));
    }

    /// <summary>
    /// Pops the current conversion context from the stack.
    /// </summary>
    public static void PopContext()
    {
        var stack = _contextStack.Value;
        if (stack == null || stack.Count == 0)
        {
            // Defensive: log warning but don't throw (allows graceful degradation)
            Trace.TraceWarning("[TypeConverter] PopContext called with empty stack");
            return;
        }
        stack.Pop();
    }

    /// <summary>
    /// Gets the current conversion context.
    /// Throws if no context is set (indicates materializer bug).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ConversionContext GetContext()
    {
        var stack = _contextStack.Value;
        if (stack == null || stack.Count == 0)
            ThrowContextNotSet();
        return stack!.Peek();// ✅ Peek, not Pop
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static ConversionContext ThrowContextNotSet()
    {
        throw new InvalidOperationException(
            "TypeConverter.Convert called without context. " +
            "This indicates a bug in the materializer. " +
            "Ensure ConversionContextGuard is used before calling Convert.");
    }

    /// <summary>
    /// Checks if a context is currently set.
    /// Used for validation in ConversionContextGuard.
    /// </summary>
    public static bool HasContext() => _contextStack.Value?.Count > 0;

    // ==========================================
    // SECTION 3: CONTEXT GUARD (RAII PATTERN)
    // ==========================================
    
    /// <summary>
    /// RAII-style context guard that ensures proper cleanup.
    /// Supports nested conversions via stack-based context management.
    /// </summary>
    /// <example>
    /// <code>
    /// using var guard = new TypeConverter.ConversionContextGuard(options, "Age", rowIndex: 42);
    /// var result = TypeConverter.Convert(value, typeof(int));
    /// </code>
    /// </example>
    public readonly struct ConversionContextGuard : IDisposable
{
    public ConversionContextGuard(MaterializationOptions options, string? memberName, int? rowIndex)
    {
        TypeConverter.PushContext(options, memberName, rowIndex);
    }
    
    public void Dispose() => TypeConverter.PopContext();
}


    // ==========================================
    // SECTION 4: PUBLIC CONVERSION API
    // ==========================================
    
    /// <summary>
    /// Converts a value to the target type using the current ambient context.
    /// Returns sentinels for SkipProperty/SkipRow (checked via ReferenceEquals).
    /// </summary>
    /// <param name="value">The value to convert. Can be null.</param>
    /// <param name="targetType">The target type for conversion.</param>
    /// <returns>
    /// - Converted value on success
    /// - SkipPropertySentinel if property should be skipped
    /// - SkipRowSentinel if entire row should be skipped
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when called without setting context via ConversionContextGuard.
    /// </exception>
    /// <exception cref="MaterializationException">
    /// Thrown when ErrorResolution.Throw is used and conversion fails.
    /// </exception>
	public static object? Convert(object? value, Type targetType)
    {
        try
        {
            var context = GetContext();
            return ConvertCore(value, targetType, context.Options, context.MemberName, context.RowIndex);
        }
        catch (SkipPropertyException)
        {
            return SkipPropertySentinel;
        }
        catch (SkipRowException)
        {
            return SkipRowSentinel;
        }
    }


    // ==========================================
    // SECTION 5: CORE CONVERSION LOGIC
    // ==========================================
    
    private static object? ConvertCore(
        object? value, 
        Type targetType, 
        MaterializationOptions options, 
        string? memberName, 
        int? rowIndex)
    {
        // Helper for diagnostic reporting
        void ReportDiagnostic(DiagnosticSeverity severity, string strategy, string message, Exception? exception = null)
        {
            if (options.Sink == null) return;
            
            options.Sink.Report(new MaterializationDiagnostic
            {
                Severity = severity,
                RowIndex = rowIndex,
                MemberName = memberName,
                Message = message,
                AttemptedValue = value,
                TargetType = targetType,
                ConversionStrategy = strategy,
                Exception = exception,
                Timestamp = DateTimeOffset.UtcNow
            });
        }

        // Null/empty string handling
        if (value is null || (value is string s && string.IsNullOrEmpty(s)))
        {
            return HandleNullValue(value, targetType, options, memberName, rowIndex, ReportDiagnostic);
        }

        var vType = value.GetType();
        
        // Fast path: already assignable
        if (targetType.IsAssignableFrom(vType))
        {
            ReportDiagnostic(DiagnosticSeverity.Info, "DirectAssignment", 
                $"Value is already assignable to {targetType.Name}");
            return value;
        }

        // Handle Nullable<T>
        var nullable = Nullable.GetUnderlyingType(targetType);
        if (nullable != null)
            targetType = nullable;

        // String conversions
        if (value is string str)
        {
            return ConvertFromString(str, targetType, options, memberName, rowIndex, ReportDiagnostic);
        }

        // Other conversions (numeric widening, etc.)
        return ConvertOther(value, targetType, options, memberName, rowIndex, ReportDiagnostic);
    }

    // ==========================================
    // SECTION 6: SPECIALIZED CONVERTERS
    // ==========================================
    
    private static object? HandleNullValue(
        object? value,
        Type targetType,
        MaterializationOptions options,
        string? memberName,
        int? rowIndex,
        Action<DiagnosticSeverity, string, string, Exception?> reportDiagnostic)
    {
        if (options.NullStringBehavior == NullStringBehavior.Error)
        {
            return HandleConversionError(
                value, targetType, options, memberName, rowIndex,
                "Null or empty string not allowed (NullStringBehavior.Error)",
                "NullValidation", reportDiagnostic);
        }

        // Handle PreserveEmptyStrings for string types
        if (value is string && string.IsNullOrEmpty((string)value))
        {
            if (targetType == typeof(string) && 
                options.NullStringBehavior == NullStringBehavior.PreserveEmptyStrings)
            {
                reportDiagnostic(DiagnosticSeverity.Info, "PreserveEmpty", 
                    "Preserving empty string for string property", null);
                return string.Empty;
            }
        }

        // Convert to default
        var defaultValue = targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
        reportDiagnostic(DiagnosticSeverity.Info, "NullToDefault", 
            $"Converting null/empty to default: {defaultValue ?? "(null)"}", null);
        return defaultValue;
    }

    private static object? HandleConversionError(
        object? value,
        Type targetType,
        MaterializationOptions options,
        string? memberName,
        int? rowIndex,
        string errorMessage,
        string strategy,
        Action<DiagnosticSeverity, string, string, Exception?> reportDiagnostic,
        Exception? innerException = null)
    {
        var context = new MaterializationErrorContext
        {
            RowIndex = rowIndex,
            MemberName = memberName ?? string.Empty,
            AttemptedValue = value,
            TargetType = targetType,
            Exception = innerException ?? new ConversionException(errorMessage, value, targetType),
            ConversionStrategy = strategy
        };

        reportDiagnostic(DiagnosticSeverity.Error, strategy, errorMessage, context.Exception);

        // Determine resolution (callback overrides default)
        var resolution = options.OnError?.Invoke(context) ?? options.DefaultErrorResolution;

        // Runtime validation: if OnError returns UseCustomValue, provider must exist
        if (resolution == ErrorResolution.UseCustomValue && options.CustomValueProvider == null)
        {
            throw new InvalidOperationException(
                $"OnError callback returned UseCustomValue for member '{context.MemberName}', " +
                $"but CustomValueProvider is null. Either set CustomValueProvider or return a different resolution.");
        }

        switch (resolution)
        {
            case ErrorResolution.Throw:
                throw new MaterializationException(errorMessage, context, innerException);

            case ErrorResolution.UseDefault:
                var defaultValue = targetType.IsValueType ? Activator.CreateInstance(targetType) : null;
                reportDiagnostic(DiagnosticSeverity.Warning, "UseDefault",
                    $"Using default value: {defaultValue ?? "(null)"}", null);
                return defaultValue;

            case ErrorResolution.UseCustomValue:
                var customValue = options.CustomValueProvider!.Invoke(context);
                reportDiagnostic(DiagnosticSeverity.Warning, "UseCustomValue",
                    $"Using custom value: {customValue ?? "(null)"}", null);
                return customValue;

            case ErrorResolution.SkipProperty:
                reportDiagnostic(DiagnosticSeverity.Warning, "SkipProperty",
                    "Property will be skipped (retain default/initial value)", null);
                throw new SkipPropertyException(context);

            case ErrorResolution.SkipRow:
                reportDiagnostic(DiagnosticSeverity.Warning, "SkipRow",
                    "Entire row will be skipped", null);
                throw new SkipRowException(context);

            default:
                throw new InvalidOperationException($"Unknown ErrorResolution: {resolution}");
        }
    }

    private static object? ConvertFromString(
        string str,
        Type targetType,
        MaterializationOptions options,
        string? memberName,
        int? rowIndex,
        Action<DiagnosticSeverity, string, string, Exception?> reportDiagnostic)
    {
        // Trim if configured
        if (options.TrimStrings)
        {
            str = str.Trim();
            if (string.IsNullOrEmpty(str))
            {
                return HandleNullValue(str, targetType, options, memberName, rowIndex, reportDiagnostic);
            }
        }

        // Get compilation options from ambient context
        var culture = options.Compilation.Culture;
        var allowThousands = options.Compilation.AllowThousandsSeparators;
        var dateTimeFormats = options.Compilation.DateTimeFormats;

        // Integer types
        var intStyles = NumberStyles.Integer | (allowThousands ? NumberStyles.AllowThousands : 0);

        if (targetType == typeof(int))
        {
            if (int.TryParse(str, intStyles, culture, out var i))
            {
                reportDiagnostic(DiagnosticSeverity.Info, "Int32Parse", "Successfully parsed as int", null);
                return i;
            }
            return HandleConversionError(str, targetType, options, memberName, rowIndex,
                $"Cannot parse '{str}' as int", "Int32Parse", reportDiagnostic);
        }

        // ... (rest of your existing conversion logic for other types)

        // Enum support
        if (targetType.IsEnum)
        {
            if (Enum.TryParse(targetType, str, ignoreCase: options.CaseInsensitiveEnums, out var enumVal))
            {
                reportDiagnostic(DiagnosticSeverity.Info, "EnumParse", 
                    $"Successfully parsed as {targetType.Name}", null);
                return enumVal;
            }
            return HandleConversionError(str, targetType, options, memberName, rowIndex,
                $"Cannot parse '{str}' as {targetType.Name}", "EnumParse", reportDiagnostic);
        }

        // Fallback: Convert.ChangeType (if allowed)
        if (options.AllowChangeTypeFallback)
        {
            try
            {
                var result = System.Convert.ChangeType(str, targetType, culture);
                reportDiagnostic(DiagnosticSeverity.Warning, "ChangeTypeFallback",
                    "Conversion succeeded via Convert.ChangeType (slower path)", null);
                return result;
            }
            catch (Exception ex)
            {
                return HandleConversionError(str, targetType, options, memberName, rowIndex,
                    $"Convert.ChangeType failed: {ex.Message}", "ChangeTypeFallback", reportDiagnostic, ex);
            }
        }

        // No conversion strategy worked
        return HandleConversionError(str, targetType, options, memberName, rowIndex,
            $"No conversion strategy available for '{str}' → {targetType.Name}", "NoStrategy", reportDiagnostic);
    }

    private static object? ConvertOther(
        object value,
        Type targetType,
        MaterializationOptions options,
        string? memberName,
        int? rowIndex,
        Action<DiagnosticSeverity, string, string, Exception?> reportDiagnostic)
    {
        var culture = options.Compilation.Culture;

        // Numeric widening conversions, etc.
        // ... (your existing logic)

        // Fallback: Convert.ChangeType (if allowed)
        if (options.AllowChangeTypeFallback)
        {
            try
            {
                var result = System.Convert.ChangeType(value, targetType, culture);
                reportDiagnostic(DiagnosticSeverity.Warning, "ChangeTypeFallback",
                    "Conversion succeeded via Convert.ChangeType", null);
                return result;
            }
            catch (Exception ex)
            {
                return HandleConversionError(value, targetType, options, memberName, rowIndex,
                    $"Convert.ChangeType failed: {ex.Message}", "ChangeTypeFallback", reportDiagnostic, ex);
            }
        }

        return HandleConversionError(value, targetType, options, memberName, rowIndex,
            $"Cannot convert {value.GetType().Name} to {targetType.Name}", "NoStrategy", reportDiagnostic);
    }
}

```
### 5.3 Expression Tree Updates

### 5.3.1 Updated BuildConvertExpression

```csharp
// ✅ Return sentinel for SkipRow
private static Expression BuildConvertExpression(
    ParameterExpression input,
    Type targetType,
    string memberName)
{
    var convertMethod = typeof(TypeConverter).GetMethod(nameof(TypeConverter.Convert))!;
    var convertedValue = Expression.Variable(typeof(object), "converted");
    
    return Expression.Block(
        new[] { convertedValue },
        Expression.Assign(convertedValue, 
            Expression.Call(convertMethod, input, Expression.Constant(targetType))),
        
        // ✅ Check sentinels BEFORE converting to targetType
        Expression.Condition(
            Expression.Call(typeof(object).GetMethod(nameof(ReferenceEquals))!,
                convertedValue, Expression.Constant(TypeConverter.SkipPropertySentinel)),
            Expression.Constant(TypeConverter.SkipPropertySentinel, typeof(object)), // ✅ Keep as object
            
            Expression.Condition(
                Expression.Call(typeof(object).GetMethod(nameof(ReferenceEquals))!,
                    convertedValue, Expression.Constant(TypeConverter.SkipRowSentinel)),
                Expression.Constant(TypeConverter.SkipRowSentinel, typeof(object)), // ✅ Keep as object
                
                // ✅ Only convert to targetType if not a sentinel
                Expression.Convert(convertedValue, targetType)
            )
        )
    );
}

```
### 5.3.2 Updated CompileSetterForProperty

```csharp

private static Func<T, object?, MaterializationOptions, object?> CompileSetterForProperty(
    PropertyInfo p,
    CompilationOptions compilationOptions)
{
     var obj = Expression.Parameter(typeof(T), "obj");
    var val = Expression.Parameter(typeof(object), "val");
    var opts = Expression.Parameter(typeof(MaterializationOptions), "opts");

    var convertedValue = Expression.Variable(typeof(object), "converted"); // ✅ Keep as object
    var convertCall = BuildConvertExpression(val, p.PropertyType, p.Name);

    var body = Expression.Block(
        new[] { convertedValue },
        Expression.Assign(convertedValue, convertCall),
        
        // ✅ Check sentinels (already object type)
        Expression.Condition(
            Expression.Call(typeof(object).GetMethod(nameof(ReferenceEquals))!,
                convertedValue, Expression.Constant(TypeConverter.SkipRowSentinel)),
            convertedValue, // Return sentinel as-is
            
            Expression.Condition(
                Expression.Call(typeof(object).GetMethod(nameof(ReferenceEquals))!,
                    convertedValue, Expression.Constant(TypeConverter.SkipPropertySentinel)),
                convertedValue, // Return sentinel as-is
                
                // ✅ Only assign if not a sentinel
                Expression.Block(
                    Expression.Assign(
                        Expression.Property(obj, p),
                        Expression.Convert(convertedValue, p.PropertyType)),
                    convertedValue // Return for caller inspection
                )
            )
        )
    );
    
    return Expression.Lambda<Func<T, object?, MaterializationOptions, object?>>(
        body, obj, val, opts).Compile();
}

```


```csharp
private static Func<T, object?, MaterializationOptions, object?> CompileSetterForField(
    FieldInfo f,
    CompilationOptions compilationOptions)
{
    var obj = Expression.Parameter(typeof(T), "obj");
    var val = Expression.Parameter(typeof(object), "val");
    var opts = Expression.Parameter(typeof(MaterializationOptions), "opts");

    var convertedValue = Expression.Variable(typeof(object), "converted");
    var convertCall = Expression.Call(
        typeof(TypeConverter).GetMethod(nameof(TypeConverter.Convert))!,
        val,
        Expression.Constant(f.FieldType));

    var body = Expression.Block(
        new[] { convertedValue },
        Expression.Assign(convertedValue, convertCall),
        
        // Check sentinels (same logic as property)
        Expression.Condition(
            Expression.Call(typeof(object).GetMethod(nameof(ReferenceEquals))!,
                convertedValue, Expression.Constant(TypeConverter.SkipRowSentinel)),
            convertedValue,
            
            Expression.Condition(
                Expression.Call(typeof(object).GetMethod(nameof(ReferenceEquals))!,
                    convertedValue, Expression.Constant(TypeConverter.SkipPropertySentinel)),
                convertedValue,
                
                Expression.Block(
                    Expression.Assign(
                        Expression.Field(obj, f),
                        Expression.Convert(convertedValue, f.FieldType)),
                    convertedValue
                )
            )
        )
    );
    
    return Expression.Lambda<Func<T, object?, MaterializationOptions, object?>>(
        body, obj, val, opts).Compile();
}

```
### 5.4 MemberMaterializer Updates 
#### 5.4.1 FeedUsingSchema Update

```csharp
public static T FeedUsingSchema<T>(
    T obj,
    Dictionary<string, int> schemaDict,
    object?[] values,
    MaterializationOptions? options = null,
    int? rowIndex = null)
{
    if (obj == null) throw new ArgumentNullException(nameof(obj));
    if (values == null) throw new ArgumentNullException(nameof(values));

    options ??= MaterializationOptions.Default;
    options.EnsureValidated(); // ✅ Only validates once

    var plan = MemberMaterializationPlanner.Get<T>(options.Compilation);

    // Normalize schema dictionary to match plan's comparer
    Dictionary<string, int> normalizedSchema = schemaDict;
    if (schemaDict.Comparer != plan.NameComparer)
    {
        normalizedSchema = new Dictionary<string, int>(schemaDict, plan.NameComparer);
    }

    foreach (ref readonly var member in plan.Members.AsSpan())
    {
         if (normalizedSchema.TryGetValue(member.Name, out var idx))
        {
            using var guard = new TypeConverter.ConversionContextGuard(
                options, member.Name, rowIndex);
            
            var convertedValue = member.Set(obj, values[idx], options);
            
            // Check sentinels (reference equality)
            if (ReferenceEquals(convertedValue, TypeConverter.SkipRowSentinel))
            {
                // Log and return null/default
                 options.Sink?.Report(new MaterializationDiagnostic
                {
                    Severity = DiagnosticSeverity.Warning,
                    RowIndex = rowIndex,
                    MemberName = member.Name,
                    Message = $"Row skipped due to critical field '{member.Name}' conversion failure",
                    AttemptedValue = values[idx],
                    TargetType = member.TargetType,
                    ConversionStrategy = "SkipRow",
                    Timestamp = DateTimeOffset.UtcNow
                });
                return default;  // ✅ Return null/default for skipped row
            }
            
            if (ReferenceEquals(convertedValue, TypeConverter.SkipPropertySentinel))
            {
                continue; // Skip this property
            }
        }
    }
    
    return obj;
}
```
#### 5.4.2 FeedOrdered Update

```csharp
public static T FeedOrdered<T>(
    T obj, 
    object?[] values,
    MaterializationOptions? options = null,
    int? rowIndex = null)
{
    if (obj == null) throw new ArgumentNullException(nameof(obj));
    if (values == null) throw new ArgumentNullException(nameof(values));

    options ??= MaterializationOptions.Default;
    options.Validate();

    var plan = MemberMaterializationPlanner.Get<T>(options.Compilation);
    
    int vIndex = 0;
    for (int i = 0; i < plan.Members.Length && vIndex < values.Length; i++)
    {
        var m = plan.Members[i];
        if (m.OrderIndex >= 0)
        {
            // Set ambient context for this member conversion
            using var guard = new TypeConverter.ConversionContextGuard(
                options, m.Name, rowIndex);
            
            var convertedValue = m.Set(obj, values[vIndex++], options);
    
            if (ReferenceEquals(convertedValue, TypeConverter.SkipRowSentinel))
            {
                options.Sink?.Report(new MaterializationDiagnostic { ... });
                return default;
            }
            
            if (ReferenceEquals(convertedValue, TypeConverter.SkipPropertySentinel))
            {
                continue;
            }
        }
    }
    
    return obj;
}
```
### 5.5 ObjectMaterializer Updates
```csharp

public static T? Create<T>(
    object[] parameters,
    MaterializationOptions? options = null,
    int? rowIndex = null)
{
    if (TryCreateViaBestConstructor<T>(parameters, out var instance))
        return instance;
    return NewUsingInternalOrder<T>(parameters, options, rowIndex);
}

public static T? Create<T>(
    string[] schema,
    object[] parameters,
    MaterializationOptions? options = null,
    int? rowIndex = null)
{
    options ??= MaterializationOptions.Default;
    options.Validate();
    
    // ✅ No try-catch needed - FeedUsingSchema returns default for skipped rows
    return NewWithSchema<T>(schema, parameters, options, rowIndex);
}

// ✅ 1. Sequential lazy evaluation (memory efficient)
public static IEnumerable<T> CreateBatch<T>(
    string[] schema,
    IEnumerable<object[]> rows,
    MaterializationOptions? options = null,
    int startRowIndex = 0)
{
    if (schema == null) throw new ArgumentNullException(nameof(schema));
    if (rows == null) throw new ArgumentNullException(nameof(rows));

    options ??= MaterializationOptions.Default;
    options.EnsureValidated();
    
    int currentIndex = startRowIndex;
    
    foreach (var row in rows)
    {
        T? obj;
        try
        {
            obj = Create<T>(schema, row, options, currentIndex);
        }
        catch
        {
            TypeConverter.ClearContext();
            throw;
        }
        
        if (obj != null) yield return obj;
        currentIndex++;
    }
}

// ✅ 2. Async streaming (with context safety)
public static async IAsyncEnumerable<T> CreateStream<T>(
    string[] schema,
    IAsyncEnumerable<object[]> rows,
    MaterializationOptions? options = null,
    int startRowIndex = 0,
    [EnumeratorCancellation] CancellationToken cancellationToken = default)
{
    if (schema == null) throw new ArgumentNullException(nameof(schema));
    if (rows == null) throw new ArgumentNullException(nameof(rows));

    options ??= MaterializationOptions.Default;
    options.EnsureValidated();
    
    int currentIndex = startRowIndex;
    
    await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false))
    {
        T? obj;
        try
        {
            obj = Create<T>(schema, row, options, currentIndex);
        }
        catch
        {
            TypeConverter.ClearContext();
            throw;
        }
        
        if (obj != null)
            yield return obj;
        
        currentIndex++;
    }
}

// ✅ 3. Parallel processing (optimized, thread-safe)
public static IReadOnlyList<T> CreateBatchParallel<T>(
    string[] schema,
    IReadOnlyList<object[]> rows,
    MaterializationOptions? options = null,
    int startRowIndex = 0,
    int degreeOfParallelism = -1)
{
    if (schema == null) throw new ArgumentNullException(nameof(schema));
    if (rows == null) throw new ArgumentNullException(nameof(rows));

    options ??= MaterializationOptions.Default;
    options.EnsureValidated();
    
    if (options.Sink != null && !IsThreadSafe(options.Sink))
    {
        throw new InvalidOperationException(
            "Parallel materialization requires a thread-safe sink. " +
            "Use CollectionSink, ConsoleSink, or LoggerSink.");
    }
    
    var parallelOptions = new ParallelOptions
    {
        MaxDegreeOfParallelism = degreeOfParallelism > 0 
            ? degreeOfParallelism 
            : Environment.ProcessorCount
    };
    
    var allResults = new List<List<T>>();
    var lockObj = new object();
    
    Parallel.For(
        0, 
        rows.Count, 
        parallelOptions,
        () => new List<T>(rows.Count / Environment.ProcessorCount + 1),
        (i, state, threadLocalList) =>
        {
            try
            {
                var obj = Create<T>(schema, rows[i], options, i + startRowIndex);
                if (obj != null)
                    threadLocalList.Add(obj);
            }
            catch
            {
                TypeConverter.ClearContext();
                throw;
            }
            
            return threadLocalList;
        },
        threadLocalList =>
        {
            if (threadLocalList.Count > 0)
            {
                lock (lockObj)
                    allResults.Add(threadLocalList);
            }
        }
    );
    
    var totalCount = allResults.Sum(list => list.Count);
    var finalResults = new List<T>(totalCount);
    
    foreach (var list in allResults)
        finalResults.AddRange(list);
    
    return finalResults;
}

/// <summary>
/// Checks if a sink is thread-safe for parallel materialization.
/// </summary>
/// <param name="sink">Sink to check</param>
/// <returns>True if sink can be used with CreateBatchParallel</returns>
    public static bool IsThreadSafe(object sink)
    {
        if (sink.GetType().GetCustomAttribute<ThreadSafeAttribute>() != null)
            return true;
        
        return sink switch
        {
            CollectionSink => true,
            ConsoleSink => true,
            MicrosoftLoggerSink => true,
            CompositeSink composite => composite.Sinks.All(s => IsThreadSafe(s)),
            _ => false
        };
    }
    
// Internal helpers updated to pass options/rowIndex
private static T NewUsingInternalOrder<T>(
    object?[] parameters,
    MaterializationOptions? options = null,
    int? rowIndex = null)
{
    if (!TryGetParameterlessFactory<T>(out var factory))
    {
        throw new InvalidOperationException(
            $"Type {typeof(T).FullName} has no public parameterless constructor.");
    }

    T instance = (T)factory();
    return MemberMaterializer.FeedUsingInternalOrder(
        instance, parameters, options, rowIndex);
}

private static T NewWithSchema<T>(
    string[] schema,
    object?[] parameters,
    MaterializationOptions? options = null,
    int? rowIndex = null)
{
    if (schema == null) throw new ArgumentNullException(nameof(schema));
    options ??= MaterializationOptions.Default;

    if (!TryGetParameterlessFactory<T>(out var factory))
    {
        return CreateViaPrimaryConstructorWithSchema<T>(
            schema, parameters, options, rowIndex);
    }

    T instance = (T)factory();
    var dict = MemberMaterializer.GetSchemaDictionary(
        schema, 
        options.Compilation.CaseInsensitiveHeaders 
            ? StringComparer.OrdinalIgnoreCase 
            : StringComparer.Ordinal);
    
    return MemberMaterializer.FeedUsingSchema(
        instance, dict, parameters, options, rowIndex);
}
 ```
### 5.6 Sinks
 
Add three built-in sink implementations:

ConsoleSink.csCollectionSink 
```csharp
[ThreadSafe] // ✅ Console.WriteLine is thread-safe
public sealed class ConsoleSink : MaterializationSinkBase
{
    public ConsoleSink(SinkVerbosity verbosity = SinkVerbosity.ErrorsOnly)
        : base(verbosity) { }

    protected override void ReportCore(MaterializationDiagnostic diagnostic)
    {
        var color = diagnostic.Severity switch
        {
            DiagnosticSeverity.Error => ConsoleColor.Red,
            DiagnosticSeverity.Warning => ConsoleColor.Yellow,
            DiagnosticSeverity.Info => ConsoleColor.Cyan,
            _ => ConsoleColor.Gray
        };

        var originalColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.WriteLine(diagnostic.ToString());
        Console.ForegroundColor = originalColor;
    }
}
```

CollectionSink.cs
```csharp
[ThreadSafe] // ✅ ConcurrentBag is thread-safe
public sealed class CollectionSink : MaterializationSinkBase
{
    private readonly ConcurrentBag<MaterializationDiagnostic> _diagnostics = new();
    private bool _disposed = false;

    public CollectionSink(
        SinkVerbosity verbosity = SinkVerbosity.WarningsAndErrors,
        Func<MaterializationDiagnostic, bool>? filter = null)
        : base(verbosity, filter) { }

    public IReadOnlyCollection<MaterializationDiagnostic> Diagnostics => _diagnostics;

    protected override void ReportCore(MaterializationDiagnostic diagnostic)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _diagnostics.Add(diagnostic);
    }

    /// <summary>
    /// Clears all collected diagnostics and releases memory.
    /// The sink remains usable after calling Clear().
    /// </summary>
    public override void Clear() // ✅ Override base
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _diagnostics.Clear();
        // Note: ConcurrentBag doesn't have TrimExcess()
    }

    /// <summary>
    /// Disposes the sink and releases all resources.
    /// After disposal, the sink cannot be used.
    /// </summary>
    public override void Dispose() // ✅ Override base
    {
        if (!_disposed)
        {
            _diagnostics.Clear();
            _disposed = true;
        }
    }
}
```

MicrosoftLoggerSink.cs
```csharp
[ThreadSafe] // ✅ Add this (ILogger is thread-safe)
public sealed class MicrosoftLoggerSink : MaterializationSinkBase
{
    private readonly ILogger _logger;

    public MicrosoftLoggerSink(ILogger logger, 
        SinkVerbosity verbosity = SinkVerbosity.ErrorsOnly)
        : base(verbosity)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override void ReportCore(MaterializationDiagnostic diagnostic)
    {
        var logLevel = diagnostic.Severity switch
        {
            DiagnosticSeverity.Error => LogLevel.Error,
            DiagnosticSeverity.Warning => LogLevel.Warning,
            DiagnosticSeverity.Info => LogLevel.Information,
            _ => LogLevel.Trace
        };

        _logger.Log(logLevel, diagnostic.Exception, diagnostic.ToString());
    }
}
```

CompositeSink.cs

```csharp
/// <summary>
/// Sink that broadcasts to multiple child sinks.
/// Useful for logging to both console and file, or collecting diagnostics
/// while also logging to ILogger.
/// </summary>
// ⚠️ Conditionally thread-safe (only if all children are)
public sealed class CompositeSink : IMaterializationSink
{
     private readonly List<IMaterializationSink> _sinks;
    
    // <summary>
    /// Read-only collection of child sinks.
    /// Used by IsThreadSafe to validate thread-safety of all children.
    /// </summary>
    public IReadOnlyList<IMaterializationSink> Sinks => _sinks;
    
    public CompositeSink(params IMaterializationSink[] sinks)
    {
        _sinks = new List<IMaterializationSink>(sinks ?? throw new ArgumentNullException(nameof(sinks)));
    }
    
    public void Report(MaterializationDiagnostic diagnostic)
    {
        foreach (var sink in _sinks)
        {
            try
            {
                sink.Report(diagnostic);
            }
            catch (Exception ex)
            {
                // Don't let one sink failure break others
                Trace.TraceError($"Sink {sink.GetType().Name} failed: {ex.Message}");
            }
        }
    }
    
    /// <summary>
    /// Adds a sink to the composite. Useful for fluent builder pattern.
    /// </summary>
    internal void AddSink(IMaterializationSink sink)
    {
        _sinks.Add(sink);
    }
}
```

 ---
### 5.7 Member and Plan Signatures
 
Update the compiled member setter signature to include MaterializationOptions and expose order index.

```csharp
public readonly struct MemberSetter
{
    public readonly string Name;
    public readonly int OrderIndex;
    public readonly Type TargetType; 
    public readonly Func<T, object?, MaterializationOptions, object?> Set;
    
    public MemberSetter(
        string name, 
        int orderIndex,
        Type targetType,
        Func<T, object?, MaterializationOptions, object?> set) // ✅ Func
    {
        Name = name;
        OrderIndex = orderIndex;
        TargetType = targetType;
        Set = set;
    }
}
```
### 5.8 ObjectMaterializer: consistency and surface area
 
Existing Create overloads are standardized to the sentinel-based TypeConverter flow and context guard, with explicit options passing and row index propagation for diagnostics and error handling. The new overloads are added without removing existing public APIs.
 
 - New overloads:
   - Create<T>(object[] parameters, MaterializationOptions? options = null, int? rowIndex = null)
   - Create<T>(string[] schema, object[] parameters, MaterializationOptions? options = null, int? rowIndex = null)
 - Internal helpers updated to thread options/rowIndex to MemberMaterializer.
 - SkipRow handling returns default/null and reports through Sink if available.
### 5.9 TypeConverter documentation and ambient context guard
 
Add an explicit note that Convert must be called within a ConversionContextGuard lifetime. The guard enforces RAII cleanup, ensures no nested contexts, and provides a safe default with a debug assert and a trace warning if context is missing.

Additionally, clarify sentinel contract:
- ConversionResult.SkipProperty and ConversionResult.SkipRow are singletons compared by reference in expression trees.
- Expression tree throws a parameterless SkipRowException when the SkipRow sentinel is observed; FeedUsingSchema and ObjectMaterializer catch and process it, logging context via TypeConverter.GetSkipRowContext.
 
 ---
## 6. Files Added/Modified (final enumeration)
 
	New files:
 - ConsoleSink.cs
 - CollectionSink.cs
 - MicrosoftLoggerSink.cs
 
	Modified files:

 - MemberMaterializationPlan.cs
 - MemberMaterializer.cs
 - ObjectMaterializer.cs
 - MaterializationOptions.cs
 - TypeConverter.cs (new central conversion)
 
 ---
## 7. Backward Compatibility Notes
 
 - Plan caching remains keyed by CompilationOptions only; runtime options flow explicitly or via ambient context and do not affect caching.
 - Existing Create APIs continue to function; new overloads add optional options/rowIndex without breaking signatures.
 - If code previously depended on exceptions for SkipProperty/SkipRow bubbling from conversion, it must now honor sentinel-based control flow via the updated expression trees. Public behavior remains consistent at higher layers: rows can still be skipped, properties left at default, or exceptions thrown per options.
 
 ---
## 8. Testing Strategy
### 8.1 Unit Tests
#### 8.1.1 TypeConverter Tests (Priority: Critical)
- ✅ Context lifecycle: set, get, clear, nested detection
- ✅ Sentinel returns for each ErrorResolution
- ✅ Null handling across all NullStringBehavior modes
- ✅ All primitive types: int, long, short, byte, decimal, double, float
- ✅ DateTime/DateTimeOffset with explicit formats
- ✅ TimeSpan, Guid, char, bool
- ✅ Enum parsing with CaseInsensitiveEnums true/false
- ✅ ChangeType fallback behavior (enabled/disabled)
- ✅ Diagnostic reporting at each severity level
- ✅ AsyncLocal context isolation across threads
#### 8.1.2 Expression Tree Tests (Priority: Critical)
- ✅ BuildConvertExpression for reference types
- ✅ BuildConvertExpression for value types
- ✅ BuildConvertExpression for Nullable<T>
- ✅ Sentinel handling: SkipProperty → default(T), SkipRow → throw
- ✅ Null input handling for nullable vs non-nullable
#### 8.1.3 Sink Tests (Priority: High)
- ✅ Verbosity filtering for each level
- ✅ Thread safety: parallel reporting from multiple threads
- ✅ CompositeSink error isolation (one sink fails, others continue)
- ✅ CollectionSink: accumulation, Clear(), thread-safe access
- ✅ MicrosoftLoggerSink: LogLevel mapping, exception propagation
- ✅ ConsoleSink: color output (manual verification)
#### 8.1.4 Options Tests (Priority: High)
- ✅ Validation: all invalid configurations throw
- ✅ Static presets: ForProduction, ForDevelopment, ForCsvImport, etc.
- ✅ Builder pattern: fluent API, build-once enforcement
- ✅ Cache key generation: identical options → same key
### 8.2 Integration Tests
#### 8.2.1 End-to-End Scenarios (Priority: Critical)
- ✅ CSV import: 10,000 rows with mixed data quality
- ✅ Mixed error resolutions: some skip, some default, some throw
- ✅ Parallel batch processing with AsyncLocal context
- ✅ Record types with primary constructors
- ✅ Classes with parameterless constructors
- ✅ Schema-based materialization vs ordered materialization
#### 8.2.2 Error Handling Flows (Priority: High)
- ✅ OnError callback overrides DefaultErrorResolution
- ✅ CustomValueProvider invoked correctly
- ✅ SkipRow stops processing and returns null
- ✅ SkipProperty leaves member at default value
- ✅ Diagnostic reporting during error resolution
### 8.3 Performance Tests
#### 8.3.1 Regression Tests (Priority: Critical)
- ✅ Baseline vs materialized: < 5% overhead (10K rows)
- ✅ Diagnostic overhead by verbosity level (see Section 10.2)
- ✅ AsyncLocal access overhead: < 0.1% for 10K conversions
- ✅ Cache hit rate: > 99% for repeated schemas
#### 8.3.2 Benchmark Suite (Priority: High)
Use BenchmarkDotNet for:
- ✅ Direct property assignment (baseline)
- ✅ Materialization without options
- ✅ Materialization with ErrorsOnly diagnostics
- ✅ Materialization with All diagnostics
- ✅ Parallel processing (1, 4, 8 threads)
### 8.4 Edge Case Tests
#### 8.4.1 Schema Variations (Priority: Medium)
- ✅ Empty schema (zero columns)
- ✅ Duplicate column names (case variations)
- ✅ Schema with null/whitespace column names
- ✅ Values array shorter than schema
- ✅ Values array longer than schema
#### 8.4.2 Data Variations (Priority: Medium)
- ✅ Null values in non-nullable properties
- ✅ Whitespace-only strings
- ✅ Culture-specific numbers: "1.234,56" vs "1,234.56"
- ✅ DateTime with ambiguous formats: "01/02/03"
- ✅ Enum values with spaces: "Active User"
- ✅ Numeric overflow: "999999999999" → int
- ✅ Leading/trailing whitespace with TrimStrings true/false
#### 8.4.3 Concurrency Tests (Priority: High)
- ✅ Parallel.ForEach with 1000 rows
- ✅ Task.WhenAll with 100 concurrent tasks
- ✅ AsyncLocal context isolation verification
- ✅ Sink thread safety under load
  
---
### 9. Developer Migration Notes
 
 - Member setter delegates now require Action<T, object?, MaterializationOptions>. Update any custom plan injection or test doubles accordingly.
 - Replace any direct ConvertObject invocations with TypeConverter.Convert within contexts where expression trees are not used; ensure using ConversionContextGuard around such calls to set options/memberName/rowIndex.
 - When handling SkipRow at call sites, use TypeConverter.GetSkipRowContext() to access details for logging, then call TypeConverter.ClearSkipRowContext() after processing to avoid leaking state across rows.

---
## 10. Performance Benchmarks & Expectations

### 10.1 Baseline Performance Targets
- **No Options:** < 5% overhead vs direct property assignment
- **Measurement Tool:** BenchmarkDotNet with 10,000 row dataset
- **Regression Tests:** CI pipeline must fail if overhead exceeds thresholds

### 10.2 Diagnostic Overhead by Verbosity

| Verbosity Level | Expected Overhead | Recommended Use Case |
|----------------|-------------------|----------------------|
| ErrorsOnly | 2-5% | Production environments |
| WarningsAndErrors | 5-10% | Data quality auditing |
| SuccessAndFailures | 10-20% | Development/validation |
| Debug | 30-50% | Troubleshooting only |

### 10.3 AsyncLocal<T> Overhead
- **Measured Access Time:** ~5-10ns per access
- **Impact on Batch Processing:** < 0.1% for 10,000 rows
- **Conclusion:** Negligible overhead, async-safety benefit outweighs cost

### 10.4 Cache Hit Rate Expectations
- **Target:** > 99% for repeated schemas
- **Measurement:** Log cache misses in development
- **Optimization:** Pre-warm cache for known schemas in production startup

### 10.5 Performance Regression Tests

Add to test suite:

```csharp
[Fact]
public void Performance_NoOptions_UnderFivePercentOverhead()
{
    var baseline = MeasureDirectAssignment(10_000);
    var withMaterializer = MeasureMaterializerWithoutOptions(10_000);
    
    var overhead = (withMaterializer - baseline) / baseline;
    Assert.True(overhead < 0.05, 
        $"Overhead {overhead:P2} exceeds 5% threshold. Baseline: {baseline}ms, Actual: {withMaterializer}ms");
}

[Theory]
[InlineData(SinkVerbosity.ErrorsOnly, 0.05)]
[InlineData(SinkVerbosity.WarningsAndErrors, 0.10)]
[InlineData(SinkVerbosity.SuccessAndFailures, 0.20)]
public void Performance_WithDiagnostics_MeetsOverheadTarget(SinkVerbosity verbosity, double maxOverhead)
{
    var baseline = MeasureDirectAssignment(10_000);
    var withDiagnostics = MeasureMaterializerWithDiagnostics(10_000, verbosity);
    
    var overhead = (withDiagnostics - baseline) / baseline;
    Assert.True(overhead < maxOverhead, 
        $"Overhead {overhead:P2} exceeds {maxOverhead:P2} threshold for {verbosity}");
}
```
## 11. Migration Guide (v2.x → v3.0)

### 11.1 Breaking Changes

#### Change 1: Member Setter Signature
**Impact:** Custom plan injection, test doubles

**Before (v2.x):**
```csharp
public readonly Action<T, object?> Set;
```

**After (v3.0):**
```csharp
public readonly Func<T, object?, MaterializationOptions, object?> Set;
```

**Migration:**
```csharp
// Before
var setter = (obj, val) => obj.Property = (int)val;

// After
var setter = (obj, val, opts) => obj.Property = (int)val;
```

#### Change 2: ConvertObject Method Removed
**Impact:** Direct conversion calls (rare)

**Before (v2.x):**
```csharp
var result = MemberMaterializationPlan<T>.ConvertObject(value, targetType, culture, allowThousands, formats);
```

**After (v3.0):**
```csharp
using var guard = new TypeConverter.ConversionContextGuard(options, memberName, rowIndex);
var result = TypeConverter.Convert(value, targetType);
```

### 11.2 Non-Breaking Enhancements

All existing `Create` overloads accept optional parameters:

```csharp
// v2.x - still works
var obj = ObjectMaterializer.Create<Person>(schema, values);

// v3.0 - enhanced (optional)
var obj = ObjectMaterializer.Create<Person>(schema, values, options, rowIndex: 42);
```

### 11.3 Recommended Upgrade Path

**Phase 1: Update (Day 1)**
- Update NuGet package to v3.0
- Run existing tests (should pass with zero changes)

**Phase 2: Add Diagnostics (Week 1)**
- Enable diagnostics in development/staging:
  ```csharp
  var options = MaterializationOptions.ForDevelopment();
  var obj = ObjectMaterializer.Create<T>(schema, values, options);
  ```

**Phase 3: Monitor & Tune (Week 2-3)**
- Review diagnostic output for data quality issues
- Identify patterns in conversion failures
- Configure error handling strategies:
  ```csharp
  var options = new MaterializationOptions
  {
      DefaultErrorResolution = ErrorResolution.UseDefault,
      OnError = ctx => ctx.MemberName == "Id" 
          ? ErrorResolution.SkipRow 
          : ErrorResolution.UseDefault
  };
  ```

**Phase 4: Production Rollout (Week 4)**
- Enable ErrorsOnly diagnostics in production:
  ```csharp
  var options = MaterializationOptions.ForProduction(logger);
  ```
- Monitor for unexpected conversion errors
- Adjust error handling based on production data

### 11.4 Rollback Plan

If issues arise, v3.0 is backward compatible:
1. Remove all `MaterializationOptions` parameters (use defaults)
2. Revert to v2.x behavior automatically
3. No code changes required for basic usage
```

---
## 12. Usage Examples

### 12.1 Basic CSV Import

```csharp
public class CsvImporter
{
    private readonly ILogger _logger;
    
    public List<Person> ImportCsv(string filePath)
    {
        var options = MaterializationOptions.ForCsvImport(_logger);
        var results = new List<Person>();
        
        foreach (var (row, index) in ReadCsvRows(filePath).Select((r, i) => (r, i)))
        {
            var person = ObjectMaterializer.Create<Person>(
                schema: row.Headers,
                parameters: row.Values,
                options: options,
                rowIndex: index);
            
            if (person != null) // null if SkipRow was triggered
            {
                results.Add(person);
            }
        }
        
        return results;
    }
}
```

### 12.2 Critical Fields with Auditing

```csharp
public class DataValidator
{
    public (List<Order> valid, List<MaterializationDiagnostic> issues) ValidateOrders(
        string[] schema, 
        List<object[]> rows)
    {
        var options = MaterializationOptions.ForAuditing(out var sink);
        options.OnError = MaterializationOptions.ErrorHandling.CriticalFieldsOnly(
            "OrderId", "CustomerId", "TotalAmount");
        
        var valid = new List<Order>();
        
        for (int i = 0; i < rows.Count; i++)
        {
            var order = ObjectMaterializer.Create<Order>(schema, rows[i], options, rowIndex: i);
            if (order != null)
            {
                valid.Add(order);
            }
        }
        
        return (valid, sink.Diagnostics.ToList());
    }
}
```

### 12.3 Custom Error Handling

```csharp
public class FlexibleImporter
{
    public List<Product> ImportProducts(string[] schema, List<object[]> rows)
    {
        var options = new MaterializationOptions
        {
            DefaultErrorResolution = ErrorResolution.UseDefault,
            OnError = ctx => ctx.MemberName switch
            {
                "ProductId" => ErrorResolution.SkipRow, // Critical
                "Price" when ctx.AttemptedValue is string s && s.Contains("$") 
                    => ErrorResolution.UseCustomValue, // Recoverable
                _ => ErrorResolution.UseDefault
            },
            CustomValueProvider = ctx => ctx.MemberName switch
            {
                "Price" => ParsePriceWithDollarSign(ctx.AttemptedValue),
                "Stock" => 0,
                "Category" => "Uncategorized",
                _ => null
            },
            Sink = new CompositeSink(
                new ConsoleSink(SinkVerbosity.WarningsAndErrors),
                new MicrosoftLoggerSink(_logger, SinkVerbosity.ErrorsOnly))
        };
        
        return rows.Select((row, i) => 
            ObjectMaterializer.Create<Product>(schema, row, options, i))
            .Where(p => p != null)
            .ToList();
    }
    
    private decimal? ParsePriceWithDollarSign(object? value)
    {
        if (value is string s && decimal.TryParse(
            s.Replace("$", "").Trim(), 
            out var price))
        {
            return price;
        }
        return null;
    }
}
```

### 12.4 Parallel Processing

```csharp
public class ParallelProcessor
{
    public List<Record> ProcessLargeDataset(string[] schema, List<object[]> rows)
    {
        var options = MaterializationOptions.ForProduction(_logger);
        var results = new ConcurrentBag<Record>();
        
        Parallel.ForEach(rows, new ParallelOptions { MaxDegreeOfParallelism = 4 }, 
            (row, state, index) =>
        {
            // Each thread gets its own AsyncLocal context
            var record = ObjectMaterializer.Create<Record>(
                schema, row, options, rowIndex: (int)index);
            
            if (record != null)
            {
                results.Add(record);
            }
        });
        
        return results.ToList();
    }
}
```
---
## 13. Troubleshooting Guide

---

### 14. Glossary

Define key terms: materialization, ambient context, sentinel, sink, verbosity, etc.
