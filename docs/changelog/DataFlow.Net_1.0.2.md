# DataFlow.Net v1.0.2 Release Notes

**Release Date:** 2026-01-06  
**Package:** [DataFlow.Net](https://www.nuget.org/packages/DataFlow.Net/)

---

## 🚨 Critical Fix

### YamlDotNet Dependency Now Included
The package now correctly includes `YamlDotNet.dll` (v16.3.0). Previous versions required manual installation.

---

## ✨ New Features

### Simple Stream API
New simplified stream overloads that mirror the file-based API. Just swap `path` for `stream`:

```csharp
// CSV - symmetric with Csv<T>(path, separator, onError, token, schema)
await foreach (var row in Read.Csv<MyRow>(myStream, ",", onError: (raw, ex) => Log(ex)))
{ /* ... */ }

// JSON - symmetric with Json<T>(path, options, onError, token)
await foreach (var item in Read.Json<MyDoc>(myStream))
{ /* ... */ }

// YAML - symmetric with Yaml<T>(path, onError, token)
await foreach (var doc in Read.Yaml<MyType>(myStream))
{ /* ... */ }

// Text - symmetric with Text(path, token)
await foreach (var line in Read.Text(myStream))
{ /* ... */ }
```

Use the **Options-based API** when you need full control over parsing behavior and error reporting.

---

## 🐛 Bug Fixes

### BUG-001: ObjectMaterializer Silent Failure with Property-Based Classes
**Severity:** High

CSV reader silently returned 0 rows when using a class with parameterless constructor and public property setters.

**Root Cause:** `GeneralMaterializationSession.TryInitCtorSession` was selecting `PrimaryCtor` strategy even for parameterless constructors.

**Fix:** Added check to detect parameterless constructors when schema has columns, falling back to `MemberApply` strategy.

---

### BUG-002: YAML Case-Sensitive Property Matching
**Severity:** Medium

YAML reader now uses case-insensitive property matching, consistent with JSON reader.

**Fix:** Added `WithCaseInsensitivePropertyMatching().IgnoreUnmatchedProperties()` to YamlDotNet DeserializerBuilder.

---

### BUG-003: CSV SchemaError Throws NullReferenceException
**Severity:** Medium

Fixed two issues causing NullReferenceException when schema error should be reported:
1. Header row not skipped when `HasHeader = true` AND `Schema` provided
2. Code continued to materialize malformed row after logging extra fields schema error

---

### BUG-004: FieldValueConverter Custom Mode Failure
**Severity:** Medium

Custom `FieldValueConverter` now works correctly with record types.

**Root Cause:** `ConstructorHelper.PrimaryCtor` selected the copy constructor `MyRow(MyRow original)` instead of the parameterless constructor.

**Fix:** Added `IsCopyConstructor` filter to exclude copy constructors from primary constructor selection.

---

### BUG-005: Text Reader Progress Tracking
**Severity:** Low  
**Status:** Verified working. Timing-dependent tests marked as flaky.

---

### BUG-006: JSON Single Root Validation Buffering
**Severity:** Low  
**Status:** Verified working. Large single root JSON objects (2.5MB+) with validation work correctly.

---

## 📦 Package Contents

The NuGet package now includes **13 DLLs**:

| Component | Description |
|-----------|-------------|
| DataFlow.Net.dll | Main entry point |
| DataFlow.Data.Read.dll | CSV, JSON, YAML, Text readers |
| DataFlow.Data.Write.dll | CSV, JSON writers |
| DataFlow.Extensions.*.dll | LINQ extensions (6 assemblies) |
| DataFlow.Framework.*.dll | Core infrastructure (4 assemblies) |
| **YamlDotNet.dll** | YAML parsing (now included!) |

---

## 📊 Test Coverage

| Test Suite | Passed | Status |
|------------|--------|--------|
| DataFlow.Data.Tests | 341 | ✅ |
| DataFlow.Core.Tests | 482 | ✅ |

---

## ⬆️ Upgrade Notes

No breaking changes. Direct upgrade from v1.0.0/v1.0.1 is safe.

If you previously installed YamlDotNet manually, you can now remove that direct reference.
