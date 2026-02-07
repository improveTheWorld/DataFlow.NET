# DataFlow.Net v1.2.0 Final Audit Report

## Results: 43 passed, 3 issues (1 bug, 1 edge case, 1 retracted)

---

## 🔴 Confirmed Bug (1)

### Locale Decimal Parsing — Silent Wrong Values

`1234,50` in fr-FR locale with `;` separator → parsed as **`123450`** (wrong, no error raised).

**Why this is a real bug:**
- [DataFlow-Data-Reading-Infrastructure.md](file:///c:/CodeSource/DataFlow/src/docs/DataFlow-Data-Reading-Infrastructure.md#L1094) states: *"uses current culture Parse methods"*
- But actual behavior uses `InvariantCulture` (comma = thousands separator → silently drops it)
- Result: **silent data corruption** — no error, no skip, just wrong numbers for European users

**Doc reference:** [Known Limitations, Section 8](file:///c:/CodeSource/DataFlow/src/docs/DataFlow-Data-Reading-Infrastructure.md#L1090-L1107)

---

## 🟡 Edge Case (1)

### JSON null → non-nullable decimal

`"Amount": null` throws `JsonException` when target is `decimal`. This is `System.Text.Json` standard behavior. Fix: use `decimal?`.

Should be documented as a gotcha in [DataFlow-Data-Reading-Infrastructure.md, Section 4](file:///c:/CodeSource/DataFlow/src/docs/DataFlow-Data-Reading-Infrastructure.md#L764-L800).

---

## Retracted Findings

| Originally Reported | Verdict |
|---|---|
| CSV RFC 4180 `""` parsing | ✅ Works. Test error. |
| Until() off-by-one | ✅ By design. Inclusive semantics, consistent across all types. |

---

## 📝 Documentation Improvements

| What | Where |
|------|-------|
| **Until() is inclusive** — must state explicitly | [Extension-Methods-API-Reference.md:L72-79](file:///c:/CodeSource/DataFlow/src/docs/Extension-Methods-API-Reference.md#L72-L79) |
| **Fix "current culture" claim** — actually InvariantCulture | [Data-Reading-Infrastructure.md:L1094](file:///c:/CodeSource/DataFlow/src/docs/DataFlow-Data-Reading-Infrastructure.md#L1094) |
| **JSON null→non-nullable types** gotcha | [Data-Reading-Infrastructure.md:L764](file:///c:/CodeSource/DataFlow/src/docs/DataFlow-Data-Reading-Infrastructure.md#L764) |
| `MergeOrdered` signature | [Extension-Methods-API-Reference.md:L67-70](file:///c:/CodeSource/DataFlow/src/docs/Extension-Methods-API-Reference.md#L67-L70) |
| `Spy()` string-only | [Extension-Methods-API-Reference.md:L122-127](file:///c:/CodeSource/DataFlow/src/docs/Extension-Methods-API-Reference.md#L122-L127) |
| `BuildString` returns `StringBuilder` | [Extension-Methods-API-Reference.md:L94-99](file:///c:/CodeSource/DataFlow/src/docs/Extension-Methods-API-Reference.md#L94-L99) |
| `WriteJson` path API no options | [Data-Writing-Infrastructure.md:L112-123](file:///c:/CodeSource/DataFlow/src/docs/DataFlow-Data-Writing-Infrastructure.md#L112-L123) |
| `Separator` is `char` not `string` | [Data-Writing-Infrastructure.md:L58-67](file:///c:/CodeSource/DataFlow/src/docs/DataFlow-Data-Writing-Infrastructure.md#L58-L67) |

---

## ✅ Verified Working (43 tests)

CSV (11), JSON (6), YAML+Security (5), Text (1), Extensions (9), Cases (2), Parallel (2), Write APIs (6), RFC 4180 (1)

---

*Feb 7, 2026 | DataFlow.Net 1.2.0 | .NET 8.0*
