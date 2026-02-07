# DataFlow Test Coverage Report

> **Generated:** February 2026  
> **Test Framework:** xUnit  
> **Coverage Tool:** Coverlet
> **Total Tests:** 868 (99.5% pass)

---

## Coverage Summary

> **Test Projects (January 2026):**

| Test Project | Tests | Pass | Fail | Skip |
|--------------|-------|------|------|------|
| `DataFlow.Core.Tests` | 404 | 404 | 0 | 7 |
| `DataFlow.Data.Tests` | 298 | 298 | 0 | 3 |
| `DataFlow.ParallelAsyncQuery.Tests` | 110 | 110 | 0 | 0 |
| `DataFlow.Data.Write.Tests` | 49 | 49 | 0 | 0 |
| `DataFlow.CasesOverloadTests` | 5 | 5 | 0 | 0 |
| **Total** | **868** | **864** | **4** | **12** |

**Pass Rate:** 99.5% (4 flaky PerformanceTests fail in batch, pass individually)  
**Overall Status:** ✅ Release Ready

---

## Query Provider Test Coverage

> **Note:** SparkQuery and SnowflakeQuery tests are in the **DataFlow.Enterprise** repository.

### SparkQuery Tests (Enterprise)

| Test Project | Total | Pass | Notes |
|--------------|-------|------|-------|
| `DataFlow.SparkQuery.UnitTests` | 54 | 54 | ✅ Fast, no Spark needed |
| `DataFlow.SparkQuery.IntegrationTests` | 155 | 155 | ✅ Requires Spark backend |
| **Spark Total** | **209** | **209** | ✅ 100% |

**Features Tested:**
- Math functions: `Abs`, `Round`, `Ceiling`, `Floor`, `Sqrt`, `Pow`
- String methods: `IndexOf`, `Replace`, `Length`, `Contains`, `StartsWith`, `EndsWith`
- DateTime properties: `Year`, `Month`, `Day`, `Hour`, `Minute`, `Second`
- Higher-order array functions: `Any`→`exists`, `All`→`forall`, `Where`→`filter`, `Select`→`transform`
- Cases pattern: Filter expression translation, SelectCase transforms, DataFrame integration
- Write API: `WriteParquet`, `WriteCsv`, `WriteJson`, `WriteTable`
- Window functions, Joins, Grouping, Set operations
- **v1.2.0:** Auto-UDF, Assembly Distribution, Adaptive Push, Decimal Conversion

### SnowflakeQuery Tests (Enterprise)

| Test Project | Total | Pass | Notes |
|--------------|-------|------|-------|
| `DataFlow.SnowflakeQuery.Tests` | 71 | 71 | ✅ All pass (SQL generation) |
| `DataFlow.SnowflakeQuery.IntegrationTests` | 47 | 47 | ✅ All pass (live Snowflake) |
| **Snowflake Total** | **118** | **118** | ✅ 100% |

**Features Tested:**
- SQL generation via `ToSql()` validation
- Basic queries: SELECT, WHERE, ORDER BY, LIMIT, OFFSET
- DateTime functions: `YEAR()`, `MONTH()`, `DAY()`, `HOUR()`
- String functions: `LENGTH()`, `POSITION()`, `LIKE`
- Math functions: `ABS()`, `ROUND()`, `CEIL()`, `FLOOR()`, `SQRT()`
- Higher-order array functions: `Any`→`FILTER`, `All`→`FILTER NOT`, `Where`→`FILTER`, `Select`→`TRANSFORM`
- Write API: `WriteTable`, `MergeTable`, `WriteTables` (Cases pattern)

### Licensing Tests (Enterprise)

| Test Project | Total | Pass | Notes |
|--------------|-------|------|-------|
| `DataFlow.Licensing.Tests` | 24 | 24 | ✅ All pass |
| `DataFlow.Licensing.IntegrationTests` | 10 | 10 | ✅ All pass |
| **Licensing Total** | **34** | **34** | ✅ 100% |


### Read Layer Tests (Updated - January 2026)

| Test Project | Tests | Status |
|--------------|-------|--------|
| `DataFlow.Data.Tests` | 298 | ✅ 294 pass, 1 fail, 3 skip |
| **Total** | **298** | ✅ |

**Features Tested:**
- Buffer boundary conditions in JSON/CSV streaming
- Error recovery with `ReaderErrorAction.Skip`
- Sync/Async API consistency
- MemoryStream-based YAML parsing (fixed hang issue)
- Edge cases: empty streams, quoted fields, large data sets

---

## Detailed Package Coverage

### Core Packages (High Priority)

| Package | Lines | Branches | Status |
|---------|-------|----------|--------|
| `DataFlow.Framework.UnifiedStream` | 91.5% | 88.9% | ✅ Excellent |
| `DataFlow.Framework.ObjectMaterialization` | 84.0% | 72.1% | ✅ Excellent |
| `DataFlow.Framework.SparkQuery` | 75%+ | - | ✅ Good |
| `DataFlow.Framework.ParallelAsyncQuery` | 70.1% | 60.2% | ✅ Good |
| `DataFlow.Data.Read` | 55.0% | 45.0% | ✅ Good |
| `ParallelQueryExtensions` | 54.7% | - | ✅ Good |
| `AsyncEnumerableExtensions` | 53.7% | - | ✅ Good |
| `EnumerableExtensions` | 52.8% | - | ✅ Good |
| `ParallelAsyncQueryExtensions` | 50.6% | - | ✅ Good |
| `DataFlow.Data.Write` | 87.3% | - | ✅ Excellent |

### Cases Pattern Extensions
| Test File | Tests | Status |
|-----------|-------|--------|
| `EnumerableCasesExtensionTests.cs` | 13 | ✅ Pass |
| `AsyncEnumerableCasesExtensionTests.cs` | 9 | ✅ Pass |
| `ParallelQueryCasesExtensionTests.cs` | 12 | ✅ Pass |
| `ParallelAsyncQueryCasesExtensionTests.cs` | 9 | ✅ Pass |
| `AllCasesFilteringTests.cs` | ~10 | ✅ Pass |
| `SparkQueryCasesExtensionTests.cs` | 10 | ✅ Pass (NEW) |
| **Subtotal** | **~63** | ✅ |

| Package | Lines | Status |
|---------|-------|--------|
| `StringExtensions` | 1.0% | 🔶 |
| `ArrayExtensions` | 0.0% | ❌ |
| `FileSystemExtensions` | 0.0% | ❌ |
| `SparkQueryExtensions` | ~40%+ | ✅ Good (NEW) |

### Zero Coverage (Planned for V1.1)

| Package | Notes |
|---------|-------|
| `StringExtensions` | Internal utility |
| `Guard` | Internal utility |
| `ArrayExtensions` | Internal utility |
| `FileSystemExtensions` | Internal utility |
| `SparkQueryExtensions` | Internal utility |
| `EnumerableExtentionsTest` | Internal utility |

---

## Industry Benchmarks

| Coverage Level | Industry Standard | DataFlow Status |
|----------------|-------------------|-----------------|
| Core API (80%+) | Critical | 🟡 ~50% (v1.2.0) |

---

## Coverage Targets

| Release | Target | Status |
|---------|--------|--------|
| **V1.0** | 65%+ for core packages | ✅ Achieved |
| **V1.0.1** | 55%+ for Read layer | ✅ Achieved (Dec 2025) |
| **V1.1** | 60%+ for Read layer | 🔜 Planned |

> See [Read-Coverage-70-Plan.md](Read-Coverage-70-Plan.md) for the V1.1 coverage improvement plan.

---

## How to Run Coverage Locally

```bash
# Run all tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Generate HTML report (requires reportgenerator-globaltool)
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:**/coverage.cobertura.xml -targetdir:coverage/html
```

---

## Coverage Methodology

### What Do the Metrics Mean?

| Metric | Description |
|--------|-------------|
| **Line Coverage** | Percentage of executable code lines that were run during tests. Higher is better. |
| **Branch Coverage** | Percentage of decision branches (if/else, switch cases) that were tested. More rigorous than line coverage. |
| **"-" (dash)** | Branch coverage not measured or not applicable (e.g., simple extension methods with no conditionals). |

### Types of Tests

| Type | Description | External Dependencies |
|------|-------------|----------------------|
| **Unit Tests** | Test isolated logic with mocked dependencies. Fast, no external services needed. | None |
| **Integration Tests** | Test real interactions with external systems. Validate end-to-end behavior. | Requires backend (Spark, Snowflake) |

### SparkQuery Test Requirements

SparkQuery integration tests require a running Spark JVM backend:
- **Unit tests** (`ColumnMapperTests.cs`): Run without Spark - test column mapping logic only
- **Integration tests** (all others): Require Spark backend on port 5567

```powershell
# Start Spark backend first
.\scripts\Start-SparkBackend.ps1 -Background

# Then run tests
dotnet test src/UnitTests/DataFlow.SparkQuery.Tests
```

### SnowflakeQuery Test Approach

SnowflakeQuery tests validate **SQL generation** via `ToSql()` without connecting to Snowflake:
- Tests verify correct SQL syntax is produced
- No Snowflake account or credentials required
- Fast execution (~68ms for 21 tests)

### Coverage Tool

We use **Coverlet** - an open-source cross-platform code coverage library for .NET:
- Outputs Cobertura XML format
- Integrates with `dotnet test`
- Compatible with reportgenerator for HTML reports

---

## How to Update This Report

This report is manually maintained. To update:

1. Run tests with coverage: `dotnet test src/UnitTests --collect:"XPlat Code Coverage"`
2. Generate HTML report with `reportgenerator`
3. Update the coverage percentages in this document based on the report

---

*Last updated: February 7, 2026 (v1.2.1 — +27 ParallelAsyncQuery cancellation/timeout tests)*
