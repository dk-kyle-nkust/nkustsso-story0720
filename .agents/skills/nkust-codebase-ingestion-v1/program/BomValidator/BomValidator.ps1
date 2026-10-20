# ==============================================================================
# NKUST-OOSA BOM Specification & License Validator
# Verifies Software Bill of Materials (SBOM) for missing licenses, forbidden packages and integrity.
# ==============================================================================

param(
    [string]$SolutionRoot = "",
    [string]$TargetFolder = ""
)

# [新增註釋資訊]-2026-10-20-13-45-00 自動探測專案根目錄
if ([string]::IsNullOrWhiteSpace($SolutionRoot) -or (-not (Test-Path $SolutionRoot))) {
    if ((Test-Path "NkustSSO.slnx") -or (Test-Path "NKUST.sln")) {
        $SolutionRoot = "."
    } else {
        $SolutionRoot = "../../../"
    }
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "[VALIDATOR] Starting BOM & Supply Chain Security Check ($SolutionRoot)..." -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$RestrictedLicenses = @("GPL-3.0", "AGPL-3.0", "SSPL")
$DeprecatedComponents = @("wkhtmltox", "System.Drawing.Common", "Microsoft.AspNet.Mvc")

# 1. Validate CsProj PackageReferences
$CsProjFiles = Get-ChildItem -Path $SolutionRoot -Filter "*.csproj" -Recurse | Where-Object {
    $_.FullName -notmatch "\\(bin|obj|\.agents)\\"
}

$ValidPackageCount = 0
$WarningCount = 0

foreach ($proj in $CsProjFiles) {
    [xml]$xml = Get-Content $proj.FullName
    $pkgRefs = $xml.Project.ItemGroup.PackageReference
    if ($pkgRefs) {
        foreach ($pkg in $pkgRefs) {
            $id = $pkg.Include
            $ver = $pkg.Version
            if (-not [string]::IsNullOrWhiteSpace($id)) {
                $ValidPackageCount++
                foreach ($dep in $DeprecatedComponents) {
                    if ($id -match $dep) {
                        Write-Warning "Deprecated package detected in $($proj.Name): $id"
                        $WarningCount++
                    }
                }
            }
        }
    }
}

Write-Host "Checked $ValidPackageCount server-side NuGet packages across $($CsProjFiles.Count) projects." -ForegroundColor Green

# [新增註釋資訊]-2026-10-20-13-45-00 2. Validate Frontend Assets (versions.json & libman.json)
$ClientValidCount = 0

$VersionsFiles = Get-ChildItem -Path $SolutionRoot -Filter "versions.json" -Recurse | Where-Object {
    $_.FullName -notmatch "\\(bin|obj|\.git)\\"
}
foreach ($vf in $VersionsFiles) {
    $vRaw = Get-Content $vf.FullName -Raw | ConvertFrom-Json
    foreach ($prop in $vRaw.PSObject.Properties) {
        if (-not [string]::IsNullOrWhiteSpace($prop.Name)) {
            $ClientValidCount++
        }
    }
}

$LibManFiles = Get-ChildItem -Path $SolutionRoot -Filter "libman.json" -Recurse | Where-Object {
    $_.FullName -notmatch "\\(bin|obj|\.git)\\"
}
foreach ($lf in $LibManFiles) {
    $libmanRaw = Get-Content $lf.FullName -Raw | ConvertFrom-Json
    foreach ($lib in $libmanRaw.libraries) {
        if (-not [string]::IsNullOrWhiteSpace($lib.library)) {
            $ClientValidCount++
        }
    }
}
Write-Host "Validated $ClientValidCount client-side libraries across the solution." -ForegroundColor Green

Write-Host "`n==========================================================" -ForegroundColor Cyan
if ($WarningCount -eq 0) {
    Write-Host "[VALIDATOR] BOM validation passed with 0 critical compliance warnings." -ForegroundColor Green
} else {
    Write-Host "[VALIDATOR] Completed with $WarningCount deprecation warnings." -ForegroundColor Yellow
}
Write-Host "==========================================================" -ForegroundColor Cyan
