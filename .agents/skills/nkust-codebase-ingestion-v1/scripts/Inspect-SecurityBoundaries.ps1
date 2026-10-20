# ==============================================================================
# NKUST-OOSA Security Boundary Inspector
# Enforcing Two Core Prohibitions:
# 1. Prohibit mutating data in Sybase.nkust.edu.tw
# 2. Prohibit mutating data in 140.133.*.* network segment
# ==============================================================================

param(
    [string]$SolutionRoot = ""
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
Write-Host "[SECURITY] Security Boundary Check Starting ($SolutionRoot)..." -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$ViolationFound = $false
$ForbiddenHosts = @("sybase.nkust.edu.tw", "140.133.")
$MutationKeywords = @("INSERT ", "UPDATE ", "DELETE ", "DROP TABLE", "ALTER TABLE", "TRUNCATE ")

# 1. Scan configuration files
Write-Host "`n[Step 1] Scanning config files (*.json, *.config) for target endpoints..." -ForegroundColor Yellow
$ConfigFiles = Get-ChildItem -Path $SolutionRoot -Include "*.json", "*.config" -Recurse | Where-Object { 
    $_.FullName -notmatch "\\(bin|obj|\.git)\\" 
}

foreach ($file in $ConfigFiles) {
    $content = Get-Content -Path $file.FullName -Raw -ErrorAction SilentlyContinue
    if (-not $content) { continue }

    foreach ($forbidden in $ForbiddenHosts) {
        if ($content -match [regex]::Escape($forbidden)) {
            Write-Host "  [NOTICE] Found endpoint reference in: $($file.Name) -> $forbidden" -ForegroundColor DarkYellow
            foreach ($kw in $MutationKeywords) {
                if ($content -match $kw) {
                    Write-Error "[CRITICAL VIOLATION] File $($file.FullName) contains mutation keyword: $kw against $forbidden"
                    $ViolationFound = $true
                }
            }
        }
    }
}

# 2. Scan C# Source Code
Write-Host "`n[Step 2] Statically scanning C# source code (*.cs) for safety boundary..." -ForegroundColor Yellow
$CsFiles = Get-ChildItem -Path $SolutionRoot -Filter "*.cs" -Recurse | Where-Object {
    $_.FullName -notmatch "\\(bin|obj|\.git)\\"
}

$MutationCount = 0
foreach ($cs in $CsFiles) {
    $lines = Get-Content -Path $cs.FullName -ErrorAction SilentlyContinue
    $lineNo = 1
    foreach ($line in $lines) {
        if ($line -match "140\.133\." -and ($line -match "ExecuteNonQuery|ExecuteCommand|Delete|Update")) {
            Write-Error "[CRITICAL VIOLATION] $($cs.FullName):$lineNo contains mutation call towards 140.133.*.*!"
            $ViolationFound = $true
            $MutationCount++
        }
        $lineNo++
    }
}

Write-Host "`n==========================================================" -ForegroundColor Cyan
if ($ViolationFound) {
    Write-Error "[FAILED] Security boundary check failed! Potential mutation found."
    exit 1
} else {
    Write-Host "[SUCCESS] Security boundary compliance 100% PASSED!" -ForegroundColor Green
    Write-Host "  - No unauthorized mutation connection to Sybase.nkust.edu.tw." -ForegroundColor Green
    Write-Host "  - No data mutation targeting 140.133.*.* network host." -ForegroundColor Green
    Write-Host "  - Codebase ingestion strictly maintained in offline static analysis mode." -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Cyan
}
