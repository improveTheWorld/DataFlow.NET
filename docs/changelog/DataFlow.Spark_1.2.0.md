# Changelog — DataFlow.Spark

All notable changes to the DataFlow.Spark package.

## [1.2.0] — 2026-02-03

### ✨ New Features

#### � ForEach with Universal Field Synchronization (Delta Reflection Protocol)
- **Static Methods**: `query.ForEach(MyClass.StaticMethod).Do()` - static fields sync back to driver
- **Lambda Closures**: `query.ForEach(x => count++).Do()` - captured variables sync back
- **Instance Methods (NEW!)**: `query.ForEach(processor.Process).Do()` - instance fields sync back
  ```csharp
  var processor = new OrderProcessor();
  query.ForEach(processor.Process).Do();
  // processor.Count and processor.Total are synchronized! 🔥
  ```
- Pure UDF-based approach - no Java/Scala helper JARs needed
- IL analysis detects field writes automatically (`stsfld`/`stfld` opcodes)

#### 🚀 Automatic Assembly Distribution
- Assemblies are distributed to workers automatically on `Spark.Connect()`
- No more `--archives` or manual packaging!
- Uses `SparkContext.AddFile()` internally
- Disable via `opts.AutoDistributeAssemblies = false`

#### 🧙 Seamless UDF Auto-Registration
- Static methods in expressions are automatically registered as Spark UDFs
- Supports 1-10 parameter methods
- Primitive types: `int`, `long`, `double`, `float`, `short`, `string`, `bool`
- No manual registration required!

#### 📦 Adaptive Push() with O(1) Memory
- `Push()` now automatically batches large data
- Small data (≤ batch size): Fast in-memory path
- Large data (> batch size): Streams to temp Parquet, then reads back
- Default batch size: 10,000 rows (configurable)

#### 🔬 Direct JVM Accumulator Access
- `context.LongAccumulator("name")` - long aggregation
- `context.DoubleAccumulator("name")` - double aggregation
- Bypasses Microsoft.Spark's missing API via `Reference.Invoke()` on JVM objects

#### Other Features
- **Decimal Auto-Conversion**: `System.Decimal` → `double` at write time
- **Pull() Streaming**: `SparkQuery<T>.Pull()` returns `IAsyncEnumerable<T>` for local processing

#### 🔍 Roslyn Analyzer (Build-Time Warnings)
- Bundled analyzer provides compile-time guardrails for Spark patterns
- **DFSP001**: Warns about string fields in ForEach (non-deterministic order)
- **DFSP002**: Warns about collection fields in ForEach (not synchronized)
- **DFSP003**: Info about distributed execution (disabled by default)
- **DFSP004**: Detects UDFs in Where/Select (performance awareness)
- **DFSP005**: Error for instance methods in Where/Select (not supported)
- **DFSP006**: Warns about multiple UDFs in single expression

### 🛠 Improvements
- `StaticFieldAnalyzer.GetWrittenInstanceFields()` - public API for instance field detection
- Simplified `Spark.Connect()` - no JAR loading logic
- **ConvertValueForSpark()**: Type compatibility across all write operations
- **AssemblyDistributor**: Automatic DLL discovery and distribution

### 🧪 Quality
- 113 unit tests (including 8 analyzer tests)
- 11 integration tests for ForEach (all patterns verified)
- All ForEach patterns work via Delta Reflection Protocol

### ⚠️ Known Limitations
- BUG-001: Anonymous types cannot be materialized with `ToList()` - use `Count()` or concrete classes
- BUG-002: CSV date columns inferred as Spark Date type cannot convert to C# DateTime
- BUG-004: Ternary expressions (`?:`) not supported in join selectors

---


## [1.1.0] — 2026-01-28

### ✨ New Features
- **Unified Context API**: `Spark.Connect(master, appName)` factory method with EF Core-like DX
- **O(1) Memory File Writers**: `WriteParquet()`, `WriteCsv()`, `WriteJson()`, `WriteOrc()` with configurable `bufferSize` and `flushInterval`
- **Two-Phase Batched Consolidation**: Refactored `WriteTable` and `MergeTable` for O(1) driver memory
- **Push() Method**: `context.Push<T>(data)` creates SparkQuery from in-memory collections
  - Extension method `data.Push(context)` enables fluent chaining
  - Async support for `IAsyncEnumerable<T>`
- **TypedWindowContext**: Expression-based window aggregates via `WithWindowTyped()`
  - `w.Sum(e => e.Salary)` — no Microsoft.Spark import needed!
  - Supports `Sum`, `Avg`, `Max`, `Min`, `Count` with expression selectors
- **Async Timeout Flush**: `flushInterval` support for `IAsyncEnumerable` sources

### 🔧 Improvements
- **Parameter-First Design**: `bufferSize` and `flushInterval` are now method parameters for better IntelliSense
- **GroupBy().Select() Fix**: Changed `GroupBy()` return type to expose `Select()` method
- **Column Naming Convention**: Write API now uses ToSnakeCase for consistency with Read API
- **Ntile Fix**: Improved method lookup for window functions with constant parameters

### ✅ Quality
- 182 integration tests passing (100%)
- Added regression tests for Write/Read API consistency
- Microsoft.Spark is now invisible to .NET developers for common operations

---

## [1.0.3] — 2026-01-20
- Updated all URLs to `get-dataflow.net` domain

## [1.0.2] — 2026-01-20
- Domain update and version bump

## [1.0.1] — 2026-01-20
- Fixed email typo in LICENSE.txt

## [1.0.0] — 2026-01-19
- Initial release: LINQ-native Apache Spark integration with distributed execution
- Product-specific licensing, free developer tier (1,000 row limit)
