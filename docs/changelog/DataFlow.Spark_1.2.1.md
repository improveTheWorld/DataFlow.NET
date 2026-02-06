# Changelog — DataFlow.Spark v1.2.1

## [1.2.1] — 2026-02-04

### 🐛 Bug Fixes

> [!IMPORTANT]
> **Requires DataFlow.Net ≥ 1.2.0** for all fixes below.

#### Expression Translation Fixes (REG001-REG004)

| Bug | Issue | Fix |
|-----|-------|-----|
| **REG001** | Column resolution after DTO projection | Uses `ToSnakeCase()` for consistent column aliasing |
| **REG002** | GroupBy Count() cast fails | `UnaryExpression` (Convert) now handled in `TranslateArgument` |
| **REG003** | Ternary expressions (`?:`) not supported | `ConditionalExpression` → `When().Otherwise()` translation |
| **REG004** | Join requires anonymous types | `MemberInitExpression` now supported in Join result selectors |

#### v1.2.0 Bug Fixes (BUG-001 to BUG-006)

| Bug | Issue | Fix |
|-----|-------|-----|
| **BUG-001** | DateTime in Push() | Serializes as ISO 8601 strings |
| **BUG-002** | MemberInitExpression in Select | DTO projections now work |
| **BUG-003** | GroupBy + DTO | Proper materialization in grouped results |
| **BUG-004** | Count() type coercion | int↔long handled automatically |
| **BUG-005** | List\<T\> in Push() | JSON round-trip for collection properties |
| **BUG-006** | Anonymous types | Constructor-based materialization |

#### Additional Fixes

| Bug | Issue | Fix |
|-----|-------|-----|
| **REG005** | Where after GroupBy column resolution | PascalCase aliases preserved instead of snake_case |
| **REG006** | `First(predicate)` not implemented | Now supports `query.First(x => condition)` |
| **REG007** | `Count(predicate)` not implemented | Now supports `query.Count(x => condition)` |
| **REG008** | `FirstOrDefault(predicate)` not implemented | Now supports `query.FirstOrDefault(x => condition)` |
| **REG009** | String interpolation (`$"..."`) throws NullRef | `String.Format`/`String.Concat` → Spark `concat()` |
| **REG010** | Outer join ternary null checks fail | `IsNull()`/`IsNotNull()` for null comparisons |
| **REG011** | Left join ternary null checks fail | Fixed `FindRootParameter` for `ConditionalExpression` |
| **REG012** | Right join ternary null checks fail | Fixed `ParameterExpression` translation via column mapper |
| **REG013** | String `+` operator returns null | String concat uses `Functions.Concat()` |
| **REG014** | Null coalescing (`??`) not supported | `??` operator uses `Functions.Coalesce()` |
| **REG015** | Nested property `x.Address.City` fails | JSON→Spark reader path for struct schema inference |
| **REG016** | Multi-level nesting fails | Same fix as REG015 |

### 🛠 Improvements

- **Decimal handling**: Complete read/write round-trip (decimal→double→decimal)
- **DateTime handling**: Spark `Timestamp`→`DateTime` coercion for CSV/Parquet reads
- **Float handling**: `float/Single` now supported via double conversion
- **Type coercion** in `ConventionColumnMapper.MapFromRow()`:
  - `double → decimal` (Spark DecimalType)
  - `double → float` (Spark doesn't support float directly)
  - `long → int` / `int → long` (Spark integer types)
  - `Timestamp → DateTime` (Spark Timestamp type)
- **Test parallelization fix**: `SparkLicenseEnforcementTests` now uses `[Collection("Spark")]` to prevent race conditions

### ⚠️ Dependencies

Update your Enterprise project to use:
```xml
<PackageReference Include="DataFlow.Net" Version="1.2.0" />
```

### 🧪 Quality

- 202 integration tests PASS ✅
- 119 unit tests PASS ✅
- All REG001-REG016 regression tests included

### ✅ Resolved Limitations

These bugs from v1.2.0 Known Limitations are now fixed:
- ~~BUG-001: Anonymous types cannot be materialized with `ToList()`~~ → **FIXED**
- ~~BUG-004: Ternary expressions (`?:`) not supported in join selectors~~ → **FIXED**

---

See [DataFlow.Spark v1.2.0 Changelog](./DataFlow.Spark_1.2.0.md) for previous release notes.
