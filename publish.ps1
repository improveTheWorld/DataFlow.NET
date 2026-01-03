# Publish NuGet packages to nuget.org
# Usage: .\publish.ps1 -ApiKey YOUR_API_KEY [-Source https://api.nuget.org/v3/index.json]

param(
    [Parameter(Mandatory=$true)]
    [string]$ApiKey,
    [string]$Source = "https://api.nuget.org/v3/index.json",
    [string]$PackageDir = ".\nupkgs"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host " DataFlow.NET NuGet Publisher" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Source: $Source"
Write-Host "Packages: $PackageDir"
Write-Host ""

# Check if packages exist
$packages = Get-ChildItem $PackageDir -Filter *.nupkg -ErrorAction SilentlyContinue
if (-not $packages) {
    Write-Host "No packages found in $PackageDir. Run .\pack.ps1 first." -ForegroundColor Red
    exit 1
}

Write-Host "Found $($packages.Count) package(s) to publish:" -ForegroundColor Yellow
$packages | ForEach-Object { Write-Host "  - $($_.Name)" -ForegroundColor White }
Write-Host ""

# Confirm
$confirm = Read-Host "Publish these packages to $Source? (y/N)"
if ($confirm -ne "y" -and $confirm -ne "Y") {
    Write-Host "Aborted." -ForegroundColor Yellow
    exit 0
}

# Publish each package
$success = 0
$failed = 0

foreach ($pkg in $packages) {
    Write-Host "Publishing $($pkg.Name)..." -ForegroundColor Yellow
    dotnet nuget push $pkg.FullName --api-key $ApiKey --source $Source --skip-duplicate
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  Published!" -ForegroundColor Green
        $success++
    } else {
        Write-Host "  Failed!" -ForegroundColor Red
        $failed++
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host " Results: $success succeeded, $failed failed" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
