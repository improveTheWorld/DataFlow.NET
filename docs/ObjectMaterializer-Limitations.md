# ObjectMaterializer – Known Limitations

> **Version:** 1.2.x  
> **Planned Refactoring:** v2.0

This document catalogs the known limitations of `DataFlow.Framework.ObjectMaterializer` in its current form. These are targeted for resolution in the v2.x refactoring effort.

---

## 1. No Automatic Type Conversion in Constructor Path

**Impact:** High  
**Affects:** `CreateViaPrimaryConstructorWithSchema<T>`, `GeneralMaterializationSession<T>`

The compiled constructor delegates use direct `Expression.Convert` casts. When source values arrive as strings (e.g., from YAML, CSV without pre-conversion), the cast fails with `InvalidCastException`.

**Current Workaround:** Callers must pre-convert values to the correct types before calling `ObjectMaterializer.Create<T>()`. For YAML, this is handled in `Read.Yaml.cs` via `ConvertYamlValues<T>()`.

**v2.x Goal:** Integrate `Convert.ChangeType` / type-specific parsing directly into the compiled expression tree, eliminating the need for caller-side conversion.

---

## 2. Constructor Resolution Relies on Parameter Count Heuristic 

**Impact:** Medium  
**Affects:** `ConstructorHelper<T>.PrimaryCtor`

The "primary constructor" is identified by selecting the constructor with the most parameters. This works for standard C# records but can fail for:
- Types with multiple constructors of equal parameter count
- Types where the copy constructor has more parameters than the intended data constructor
- Types with explicit secondary constructors

**v2.x Goal:** Use `[ConstructorAttribute]` or convention-based resolution (e.g., prefer constructors matching schema names).

---

## 3. No Support for Nested Object YAML Deserialization

**Impact:** Medium  
**Affects:** Record path in `Read.Yaml.cs`

When YamlDotNet deserializes to `Dictionary<string, object>`, nested YAML mappings become `Dictionary<object, object>` rather than the target nested type. The current `ObjectMaterializer.Create<T>()` does not recursively materialize nested objects.

```yaml
# This works:
- Id: 1
  Name: Alice

# This does NOT work with record types:
- Id: 1
  Address:
    City: Paris
    Zip: 75001
```

**Current Workaround:** Use mutable classes (with parameterless constructors) for types containing nested objects — YamlDotNet handles these natively.

**v2.x Goal:** Recursive materialization support in ObjectMaterializer, detecting when a value is a `Dictionary` and materializing it into the corresponding nested type.

---

## 4. ~~Schema Matching is Case-Insensitive but Not Fuzzy~~ ✅ Resolved

**Status:** **No longer a limitation** as of v1.2.x.

`SchemaMemberResolver` implements a **5-pass resolution pipeline**:

| Pass | Strategy | Example |
|------|----------|---------|
| 1 | Exact (case-sensitive) | `Name` → `Name` |
| 2 | Case-insensitive | `name` → `Name` |
| 3 | Normalized (snake_case, camelCase, lowercase) | `first_name` → `FirstName` |
| 4 | Resemblance (prefix/suffix/contains) | `CustomerName` → `Name` |
| 5 | Levenshtein (≤2 edits) | `Nmae` → `Name` |

> **v1.2.1 Fix (NET-005):** `computeSchemaDict()` now auto-detects case-variant properties. If the schema has entries differing only by case (e.g., `Name`, `name`, `NAME`), it switches from `OrdinalIgnoreCase` to `Ordinal` to preserve distinct mappings.

---

## 5. No Nullable<T> Aware Construction

**Impact:** Low  
**Affects:** Constructor parameter binding

When a constructor parameter is `int?` and the source value is an empty string or null, the materializer may attempt `Convert.ChangeType` which throws instead of returning `null`.

**v2.x Goal:** Special-case `Nullable<T>` in the expression tree compilation to handle null/empty inputs gracefully.

---

## 6. No Async Materialization Path

**Impact:** Low (performance edge case)  
**Affects:** Streaming scenarios

`ObjectMaterializer.Create<T>()` is synchronous. In high-throughput async streaming pipelines, the synchronous constructor compilation and reflection can cause thread pool starvation under extreme load.

**v2.x Goal:** Evaluate whether async factory delegates are beneficial, or if the current approach is sufficient with the existing caching.

---

## Summary Table

| # | Limitation | Impact | Workaround Available |
|---|-----------|--------|---------------------|
| 1 | No auto type conversion in ctor path | High | ✅ Caller converts |
| 2 | Constructor heuristic by param count | Medium | ✅ Works for standard records |
| 3 | No nested object materialization | Medium | ✅ Use classes for nested types |
| 4 | ~~No fuzzy schema matching~~ | ✅ Resolved | 5-pass fuzzy matching implemented |
| 5 | Nullable\<T\> handling gaps | Low | ⚠️ Partial |
| 6 | No async path | Low | ✅ Not needed for most cases |
