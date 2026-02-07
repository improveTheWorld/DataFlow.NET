# DataFlow.Net v1.2.1

**Release Date:** February 7, 2026  
**Previous Version:** v1.2.0

---

## 🎯 Highlights

- **Smart Decimal Auto-Detection** — CSV/Text readers now auto-detect international decimal formats (`1234,56`, `1.234,56`, `1 234,56`) without requiring culture configuration
- **Culture-Aware Parsing** — New `CsvReadOptions.FormatProvider` and `TextParsingOptions.SmartDecimalParsing` options for full control
- **Write API Harmonization** — Unified 6-overload pattern across all 4 formats (24 write methods), each with optional `XxxWriteOptions?`
- **YAML Record Support** — C# positional records & `{ get; init; }` properties now deserialize correctly
- **Audit Report 2** — 5 documentation corrections identified and verified against source code

---

## ✨ New Features

### Smart Decimal Auto-Detection
- **Zero-config international parsing** — `TextParser` auto-detects decimal separators using heuristics: both separators present → last one is decimal; single separator with ≠3 trailing digits → decimal; multiple identical separators → thousands
- **Formats supported**: `1234.56` (US), `1234,56` (EU), `1.234,56` (DE), `1,234.56` (US), `1 234,56` (FR), `1.234.567,89` (DE), `1,234,567.89` (US)
- **Ambiguous fallback**: `1,234` / `1.234` (single separator + 3 digits) gracefully falls back to `FormatProvider`
- Enabled by default via `TextParsingOptions.SmartDecimalParsing = true`

### CsvReadOptions.FormatProvider
- New `FormatProvider` property (defaults to `InvariantCulture`) for explicit culture override when needed
- Wired through `TextParser.Infer()`, `TextParser.TryParse()`, and `ConvertFieldValue()` for all numeric types

### Write API Harmonization
- **Unified 6-overload matrix** per format: `IEnumerable` sync/async + `IAsyncEnumerable` async × file path + stream target
- Every overload accepts an optional `XxxWriteOptions?` parameter (encoding, append mode, metrics, format-specific settings)
- All async overloads accept an optional `CancellationToken`
- **New overloads added:** sync stream writers for all 4 formats (`WriteTextSync(stream)`, `WriteCsvSync(stream)`, `WriteJsonSync(stream)`, `WriteYamlSync(stream)`)
- Legacy convenience overloads (`WriteCsv(path, withHeader, separator)`, etc.) kept for beginner DX

### ReadOptions.OnError Property (FEAT-001)
- **Convenience delegate** — `OnError = ex => errors.Add(ex)` on any `ReadOptions` subclass auto-sets `ErrorAction = Skip` and wires `DelegatingErrorSink` internally
- Works on `CsvReadOptions`, `JsonReadOptions<T>`, and `YamlReadOptions<T>`
- Closes GitHub [#8](https://github.com/improveTheWorld/DataFlow.NET/issues/8)

---

## 🐛 Bug Fixes

### Numeric Parsing (TextParser)
- **FormatProvider wired through** — `int`, `long`, `decimal`, `double` parsers now respect `TextParsingOptions.FormatProvider` instead of hardcoding `CultureInfo.InvariantCulture` (DateTime already did)

### YAML Reader (NET-006)
- **Record type deserialization** — C# positional records and `{ get; init; }` properties now work via Dictionary→ObjectMaterializer bridge with `ConvertYamlValues<T>()` type conversion (int, long, double, decimal, bool, DateTime, Guid, enums)
- Materialization errors from partial data (e.g., after security filter skip) are silently absorbed

### ObjectMaterializer (NET-005)
- **Case-sensitive schema auto-detection** — `computeSchemaDict()` now detects when schema entries differ only by case (e.g., `Name`, `name`, `NAME`) and switches from `OrdinalIgnoreCase` to `Ordinal` to preserve distinct property mappings
- Normal case-insensitive matching preserved for standard use cases (`name` → `Name`)

### ParallelAsyncQuery (NET-001)
- **WithTimeout enforcement in parallel mode** — `OperationTimeout` is now enforced as an overall operation timeout via `CancelAfter()` in the unified `BuildCombinedCts` helper, replacing the previous `CreateLinkedCts` which never applied the timeout
- Applied consistently across all query types: `Source`, `Select`, `Where`, `Take`, `SelectMany`

### ParallelAsyncQuery (NET-002)
- **Combined cancellation token linking** — `WithCancellation()` now links the new token with any existing settings token instead of replacing it, ensuring both `AsParallel(settings)` and `await foreach ... .WithCancellation(callToken)` tokens are honored
- Added explicit `ThrowIfCancellationRequested()` in source iteration loops for sources that don't support `[EnumeratorCancellation]`

### UnifiedStream (NET-007)
- **Mutation guard race condition** — `_frozen` flag was set inside the async iterator body, which is lazily evaluated (deferred until first `MoveNextAsync()`). Callers could call `Unify()`/`Unlisten()` between `GetAsyncEnumerator()` and the first iteration without `InvalidOperationException`. Fixed by splitting into eager wrapper + lazy core iterator.

### ObjectMaterializer (NET-008)
- **snake_case schema normalization silently failed** — `BuildSchemaAction` and `CreateViaPrimaryConstructorWithSchema` bypassed the 5-pass `SchemaMemberResolver`, using only direct dictionary lookup (Pass 1-2). Schema columns like `first_name` silently produced default values instead of mapping to `FirstName`. Fixed by routing both paths through `SchemaMemberResolver.ResolveSchemaToMembers<T>()` which enables all 5 passes (exact → case-insensitive → normalized → resemblance → Levenshtein).
- Added 5 unit tests in `10_SnakeCaseNormalization.cs` covering: basic snake_case, Snowflake columns, records, single-word, and mixed scenarios.

### Data.Write (NET-009)
- **JsonLinesFormat option had no effect** — `WriteJsonSync(string path)`, `WriteJson(IEnumerable, string path)`, and `WriteJsonSync(stream)` used `JsonSerializer.Serialize` directly, always producing a JSON array. They never checked `options.JsonLinesFormat`. Fixed by adding NDJSON branch (one JSON object per line) to all 3 overloads. The `IAsyncEnumerable` and async stream overloads already worked correctly.
- Added 4 unit tests in `WritersTests.cs` covering: IEnumerable file path, IAsyncEnumerable file path, sync file path, and stream-based.

### Known Issues
- 4 × `PerformanceTests.*` + `Fix8` may fail in batch runs (timing-dependent, pass individually)

---

## 🔗 GitHub Issues Closed

| # | Title | Resolution |
|---|-------|------------|
| [#2](https://github.com/improveTheWorld/DataFlow.NET/issues/2) | BUG-001: Anonymous Type Materialization | Fixed in v1.2.0 (ObjectMaterializer refactor) |
| [#7](https://github.com/improveTheWorld/DataFlow.NET/issues/7) | DOC-001: Separator char vs string | False positive — doc was already correct |
| [#8](https://github.com/improveTheWorld/DataFlow.NET/issues/8) | FEAT-001: OnError callback on ReadOptions | Implemented — `OnError` property added |
| [#9](https://github.com/improveTheWorld/DataFlow.NET/issues/9) | DOC-002: CsvReadOptions generic parameter | Already fixed — no instances in current docs |
| [#10](https://github.com/improveTheWorld/DataFlow.NET/issues/10) | DOC-003: MergeOrdered signature | Fixed — comparer parameter now shown |

---

## 📝 Documentation

### Updated
- **Materialization-Quick-Reference.md** — Corrected YAML record support (positional ✅, init-only ✅), added nested object limitation note
- **NET-006.md** [NEW] — Bug report documenting YAML record deserialization failure and fix

### New
- **ObjectMaterializer-Limitations.md** — 6 known limitations documented for v2.x roadmap (no auto type conversion, constructor heuristic, no nested YAML, no fuzzy matching, nullable gaps, no async path)
- **DataFlow-Data-Writing-Infrastructure.md** — Rewritten to document unified Write API matrix (24 methods, 6-per-format pattern)

### Audit Report 2 — Documentation Discrepancies (5 confirmed)

| # | Issue | Doc File | Status |
|---|-------|----------|--------|
| 1 | `Until()` is inclusive — not stated explicitly | Extension-Methods-API-Reference.md | ✅ Fixed |
| 2 | "Current culture" claim — actually InvariantCulture | Data-Reading-Infrastructure.md L1094 | ✅ Fixed + Smart auto-detect added |
| 3 | JSON null→non-nullable types gotcha undocumented | Data-Reading-Infrastructure.md §4.7 | ✅ Fixed (added §4.7 reference table) |
| 4 | `WriteJson` path API has no options overload | Data-Writing-Infrastructure.md | ✅ Fixed (Write API Harmonization) |
| 5 | `Spy()` string-only convenience overload not clarified | Extension-Methods-API-Reference.md §3 | ✅ Already documented (L129) |

False positives retracted: `BuildString` return type (doc correct), `Separator` is `char` (actually `string`, doc correct).

---

## 📦 Package Info

```
dotnet add package DataFlow.Net --version 1.2.1
```

| Metric | Value |
|--------|-------|
| Tests | 859 passing (99.6%) |
| Coverage | 60% |
| Framework | .NET 8.0 |
