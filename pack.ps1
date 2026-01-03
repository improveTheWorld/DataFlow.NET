# Build and pack NuGet packages for DataFlow.NET
# Usage: .\pack.ps1 [-Configuration Release] [-Version 1.0.0]

param(
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0",
    [string]$OutputDir = ".\nupkgs"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " DataFlow.NET NuGet Packer" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Configuration: $Configuration"
Write-Host "Version: $Version"
Write-Host "Output: $OutputDir"
Write-Host ""

# Clean output directory
if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir | Out-Null

# Build the solution first
Write-Host "[1/3] Building solution..." -ForegroundColor Yellow
dotnet build DataFlow.Net.sln -c $Configuration /p:Version=$Version
if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# Pack DataFlow.Data
Write-Host "[2/3] Packing DataFlow.Data..." -ForegroundColor Yellow
dotnet pack DataFlow.Data.Read\DataFlow.Data.Read.csproj -c $Configuration /p:Version=$Version -o $OutputDir --no-build
if ($LASTEXITCODE -ne 0) {
    Write-Host "Pack failed for DataFlow.Data!" -ForegroundColor Red
    exit 1
}

# Pack DataFlow.Net (meta-package)
Write-Host "[3/3] Packing DataFlow.Net..." -ForegroundColor Yellow
dotnet pack DataFlow.Net\DataFlow.Net.csproj -c $Configuration /p:Version=$Version -o $OutputDir --no-build
if ($LASTEXITCODE -ne 0) {
    Write-Host "Pack failed for DataFlow.Net!" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host " Packages created successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Get-ChildItem $OutputDir -Filter *.nupkg | ForEach-Object {
    Write-Host "  - $($_.Name)" -ForegroundColor White
}
Write-Host ""
Write-Host "To publish: .\publish.ps1 -ApiKey YOUR_API_KEY" -ForegroundColor Cyan
