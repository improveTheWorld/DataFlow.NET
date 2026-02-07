# DataFlow Known Bugs Registry

> **Auto-generated** by `scripts/sync_bug_registry.py`.
> Source of truth: `// BUG:` comments in test files.
> Discover all: `grep -rn "// BUG:" src/UnitTests/ src/IntegrationTests/`

## Open Bugs

| ID | Product | Severity | Title | Test Reference |
|----|---------|----------|-------|----------------|
| [NET-001](NET-001.md) | DataFlow.Net | 🟡 Medium | WithTimeout Not Enforced During Parallel Execution | `WithTimeout_ExceedsLimit_CancelsExecution` |
| [NET-002](NET-002.md) | DataFlow.Net | 🟡 Medium | Combined Cancellation Tokens Not Honored in ParallelAsyncQuery | `CombinedTokens_BothTokensSet_BothHonored` |
| [NET-005](NET-005.md) | DataFlow.Net | 🟢 Low | Schema Dictionary Is Case-Insensitive — Cannot Map Case-Variant Properties | `Create_WithCaseSensitiveModel_ShouldMapCorrectly` |
| [NET-007](NET-007.md) | DataFlow.Net | 🟡 Medium | UnifiedStream Allows Mutation During Active Enumeration | `MutatingAfterEnumerationStarts_Throws` |

## Fixed Bugs

| ID | Product | Fixed In | Title |
|----|---------|----------|-------|
| [NET-003](NET-003.md) | DataFlow.Net | ✅ Resolved (Not a Bug) | ObjectMaterializer Error Message Not Detailed for Missing Constructor |
| [NET-006](NET-006.md) | DataFlow.Net | ✅ Fixed | YAML List Deserialization Fails for Record Types |
