# ==============================================================================
# NKUST-OOSA Modernization & Upgrade Compatibility Verifier
# Evaluates packages against .NET 10 LTS target specifications and flags Deprecated components.
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
Write-Host "[UPGRADE] Evaluating .NET 10 LTS Modernization Compatibility ($SolutionRoot)..." -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# Known Deprecated or Risk Patterns for .NET 10
$DeprecationRules = @(
    @{
        Pattern = "wkhtmltox"
        Status = "Deprecated"
        Reason = "Unmaintained native binary with security vulnerabilities and lack of modern CSS support."
        Alternative = "PuppeteerSharp (Headless Chromium)"
    },
    @{
        Pattern = "System\.Drawing\.Common"
        Status = "Deprecated"
        Reason = "Marked as Windows-only by Microsoft since .NET 7. Throws PlatformNotSupportedException on Linux."
        Alternative = "SixLabors.ImageSharp (Pure managed cross-platform)"
    },
    @{
        Pattern = "Microsoft\.AspNet\.Mvc"
        Status = "Deprecated"
        Reason = "Legacy ASP.NET Framework package improperly referenced in Core project."
        Alternative = "Microsoft.AspNetCore.App (Built-in shared framework)"
    },
    @{
        Pattern = "Microsoft\.AspNetCore\.Authentication\.Cookies"
        Status = "Deprecated"
        Reason = "Standalone package deprecated; Cookie authentication is now native in ASP.NET Core shared framework."
        Alternative = "Native AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)"
    },
    @{
        Pattern = "Moment\.js|moment"
        Status = "Deprecated"
        Reason = "Project in maintenance mode; lacks Tree-Shaking, excessive bundle size."
        Alternative = "Day.js (Lightweight 2KB alternative)"
    }
)

# 1. Check C# Project References
Write-Host "`n[Check 1] Auditing NuGet References against .NET 10 deprecations..." -ForegroundColor Yellow
$CsProjFiles = Get-ChildItem -Path $SolutionRoot -Filter "*.csproj" -Recurse | Where-Object {
    $_.FullName -notmatch "\\(bin|obj|\.agents)\\"
}

$FoundIssues = @()

foreach ($proj in $CsProjFiles) {
    $content = Get-Content -Path $proj.FullName -Raw
    foreach ($rule in $DeprecationRules) {
        if ($content -match $rule.Pattern) {
            $issue = [PSCustomObject]@{
                Source = $proj.Name
                Component = $rule.Pattern
                Status = $rule.Status
                Alternative = $rule.Alternative
                Reason = $rule.Reason
            }
            $FoundIssues += $issue
            Write-Host "  - [FLAGGED] $($proj.Name): Component '$($rule.Pattern)' is $($rule.Status)!" -ForegroundColor Red
            Write-Host "    Reason: $($rule.Reason)" -ForegroundColor DarkGray
            Write-Host "    Recommended: $($rule.Alternative)" -ForegroundColor Green
        }
    }
}

# [新增註釋資訊]-2026-10-20-13-45-00 2. Check Client-side Assets (versions.json & libman.json)
Write-Host "`n[Check 2] Auditing Client-side dependencies (versions.json & libman.json)..." -ForegroundColor Yellow

$ClientConfigs = Get-ChildItem -Path $SolutionRoot -Include "versions.json", "libman.json" -Recurse | Where-Object {
    $_.FullName -notmatch "\\(bin|obj|\.git)\\"
}

foreach ($cfg in $ClientConfigs) {
    $clientContent = Get-Content $cfg.FullName -Raw
    foreach ($rule in $DeprecationRules) {
        if ($clientContent -match $rule.Pattern) {
            $issue = [PSCustomObject]@{
                Source = $cfg.Name
                Component = $rule.Pattern
                Status = $rule.Status
                Alternative = $rule.Alternative
                Reason = $rule.Reason
            }
            $FoundIssues += $issue
            Write-Host "  - [FLAGGED] $($cfg.Name): Component '$($rule.Pattern)' is $($rule.Status)!" -ForegroundColor Red
        }
    }
}

Write-Host "`n==========================================================" -ForegroundColor Cyan
if ($FoundIssues.Count -gt 0) {
    Write-Host "[SUMMARY] Identified $($FoundIssues.Count) components requiring modernization / migration." -ForegroundColor Yellow
} else {
    Write-Host "[SUMMARY] All audited components satisfy modern .NET 10 LTS baseline." -ForegroundColor Green
}
Write-Host "==========================================================" -ForegroundColor Cyan
