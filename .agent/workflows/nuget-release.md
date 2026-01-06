# DataFlow.NET NuGet Release Workflow

> **CRITICAL**: Always test local package installation BEFORE publishing to NuGet.org!

## Quick Reference

```powershell
# 1. Pack
dotnet pack DataFlow.Net\DataFlow.Net.csproj -c Release -o nupkgs

# 2. Test locally (MANDATORY)
dotnet add TestProject package DataFlow.Net --version X.X.X --source c:\CodeSource\DataFlow\src\nupkgs

# 3. Publish (only after local test passes)
dotnet nuget push nupkgs\DataFlow.Net.X.X.X.nupkg --api-key YOUR_KEY --source https://api.nuget.org/v3/index.json
```

---

## The Fat Package Pattern (Required!)

### Problem (v1.0.0)
Using `ProjectReference` without special configuration creates NuGet **dependencies** that must also be published as separate packages. Users got errors like:
```
NU1101: Package DataFlow.Data.Write not found
```

### Solution (v1.0.1)
Configure `DataFlow.Net.csproj` to **embed all DLLs** into a single package:

```xml
<ItemGroup>
  <!-- PrivateAssets="all" prevents dependency declarations -->
  <ProjectReference Include="..\DataFlow.Data.Read\DataFlow.Data.Read.csproj" PrivateAssets="all" />
</ItemGroup>

<!-- Custom target to copy referenced DLLs into package -->
<PropertyGroup>
  <TargetsForTfmSpecificBuildOutput>$(TargetsForTfmSpecificBuildOutput);CopyProjectReferencesToPackage</TargetsForTfmSpecificBuildOutput>
</PropertyGroup>

<Target Name="CopyProjectReferencesToPackage" DependsOnTargets="ResolveReferences">
  <ItemGroup>
    <BuildOutputInPackage Include="@(ReferenceCopyLocalPaths->WithMetadataValue('ReferenceSourceTarget', 'ProjectReference'))" />
  </ItemGroup>
</Target>
```

---

## Release Checklist

### Pre-Release
- [ ] **BUMP VERSION**: Update `<Version>X.X.X</Version>` in `src/Directory.Build.props`
- [ ] **CLEAN**: `dotnet clean DataFlow.Net.sln` to remove old artifacts
- [ ] **PACK**: `dotnet pack DataFlow.Net\DataFlow.Net.csproj -c Release -o nupkgs`
- [ ] **CHECK SIZE**: Verify `DataFlow.Net.X.X.X.nupkg` is large (~470KB+ means DLLs are embedded)
- [ ] **TEST (Critical)**:
  1. Create temp folder: `mkdir TestRelease; cd TestRelease; dotnet new console`
  2. Clear local cache: `dotnet nuget locals all --clear`
  3. Install: `dotnet add package DataFlow.Net --version X.X.X --source c:\CodeSource\DataFlow\src\nupkgs`
  4. Run Verification (paste code below into `Program.cs`):

```csharp
using DataFlow;
using DataFlow.Parallel;
// 1. DataFlow.Data (Read API)
var methods = typeof(Read).GetMethods().Where(m => m.Name == "CsvSync");
Console.WriteLine($"Read API: {(methods.Any() ? "PASS" : "FAIL")}");

// 2. DataFlow.Extensions (Cases)
var list = new[] { 1, 2, 3 }.Cases(x => x > 1);
Console.WriteLine($"Cases Ext: {(list != null ? "PASS" : "FAIL")}");

// 3. DataFlow.Parallel (Type Check)
var type = typeof(ParallelAsyncQuery<int>);
Console.WriteLine($"Parallel Type: {(type != null ? "PASS" : "FAIL")}");

// 4. YAML (YamlDotNet bundled since v1.1.0)
var yamlType = Type.GetType("YamlDotNet.Serialization.Deserializer, YamlDotNet");
Console.WriteLine($"YAML (YamlDotNet): {(yamlType != null ? "PASS" : "FAIL")}");
```

### Publish
- [ ] `dotnet nuget push nupkgs\DataFlow.Net.X.X.X.nupkg --api-key KEY --source https://api.nuget.org/v3/index.json`
- [ ] Update README.md with new version in install commands
- [ ] Create git tag: `git tag -a vX.X.X -m "Release vX.X.X"`
- [ ] Push tag: `git push origin main --tags`

---

## Package Structure

| Package | Contents | License |
|---------|----------|---------|
| **DataFlow.Net** | All free components (14+ DLLs embedded, includes YamlDotNet) | Apache-2.0 |
| **DataFlow.Spark** | Spark LINQ translation (future) | Commercial |
| **DataFlow.Snowflake** | Snowflake LINQ translation (future) | Commercial |

---

## NuGet API Key

- **Glob Pattern**: `DataFlow.*`
- **Create at**: https://www.nuget.org/account/apikeys
- **Store securely** (never commit to git)

---

## Troubleshooting

### "Package X not found" after install
**Cause**: Package was published as meta-package with dependencies instead of fat package.
**Fix**: Ensure `PrivateAssets="all"` on all ProjectReferences and the `CopyProjectReferencesToPackage` target exists.

### Symbol package (.snupkg) fails
**Cause**: No PDB files in output.
**Impact**: Non-critical (debugging only). Main package still works.
**Future Fix**: Add `<DebugType>embedded</DebugType>` to `Directory.Build.props` to include symbols in the main DLLs, or configure `Microsoft.SourceLink.GitHub` properly to generate standalone .snupkg.

### Access denied during pack
**Fix**: Close Visual Studio and kill dotnet processes:
```powershell
taskkill /F /IM dotnet.exe
taskkill /F /IM VBCSCompiler.exe
```
