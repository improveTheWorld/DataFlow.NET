# DataFlow.Net v1.2.0

**Release Date:** February 2026  
**Previous Version:** v1.1.0

---

## 🎯 Highlights

- **RSA Licensing** — Asymmetric cryptography for secure license verification
- **60% Code Coverage** — Weighted line-by-line coverage (up from previous baseline)
- **Test Reorganization** — Consolidated into `UnitTests/`, `IntegrationTests/`, `PackageTests/`

---

## ✨ Features

- **RSA-based licensing system** for enterprise features
- **Fat package improvements** — All DLLs bundled, no external dependencies
- **GitHub Sponsors** — Added funding link for project support

---

## 🔄 API Harmonization

### Data.Read Layer
- **Separator parameter** — Changed from `char` (via `.FirstOrDefault()`) to `string?` for consistency
- **CSV/Excel API alignment** — Unified parameter handling across all Read methods
- **Documentation arrows** — Standardized to Unicode arrows in XML comments

---

## 📝 Documentation

- Updated LINQ-to-Spark documentation with v1.2.0 features
- Updated LINQ-to-Snowflake documentation for v1.2.0 release
- Standardized Write API examples with context parameter
- Aligned async method naming: `ToListAsync` → `ToList`, etc.
- Updated factory methods: `Read.SnowflakeTable` → `Snowflake.Table`
- Added ForEach distributed execution documentation
- Updated EF Core clarification and migration guides

---

## 🐛 Bug Fixes

### ObjectMaterializer
- **Skip readonly fields** — Now skips `IsInitOnly` fields (e.g., anonymous type backing fields)
- **Anonymous/Record type support** — Uses constructor parameter names for schema resolution when no settable members exist
- **JSON collection deserialization** — Automatic parsing of `List<T>` and complex types serialized as JSON (for Spark compatibility)

### Data Readers
- Fixed test files to use `ToList()` extension instead of custom helper
- Removed obsolete documentation files

---

## 📦 Package Info

```
dotnet add package DataFlow.Net --version 1.2.0
```

| Metric | Value |
|--------|-------|
| Tests | 832 passing |
| Coverage | 60% |
| Framework | .NET 8.0 |
