# ==============================================================================
# NKUST-OOSA BOM Extractor (Front-end & Back-end Software Bill of Materials)
# Extracts NuGet PackageReferences and LibMan Client-side Libraries.
# ==============================================================================

param(
    [string]$SolutionRoot = "",
    [string]$OutputMarkdownPath = ""
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
Write-Host "[BOM] Extracting Bill of Materials (BOM) from $SolutionRoot..." -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Parse Backend NuGet Packages from .csproj files
$CsProjFiles = Get-ChildItem -Path $SolutionRoot -Filter "*.csproj" -Recurse | Where-Object {
    $_.FullName -notmatch "\\(bin|obj|\.agents)\\"
}

$ServerBOM = @()
foreach ($proj in $CsProjFiles) {
    [xml]$xml = Get-Content $proj.FullName
    $projName = [System.IO.Path]::GetFileNameWithoutExtension($proj.Name)
    $pkgRefs = $xml.Project.ItemGroup.PackageReference
    if ($pkgRefs) {
        foreach ($pkg in $pkgRefs) {
            $pkgId = $pkg.Include
            if (-not $pkgId) { $pkgId = $pkg.Update }
            if (-not [string]::IsNullOrWhiteSpace($pkgId)) {
                $ver = $pkg.Version
                if (-not $ver) { $ver = $pkg.VersionOverride }
                $ServerBOM += [PSCustomObject]@{
                    Project = $projName
                    PackageId = $pkgId
                    Version = if ($ver) { $ver } else { "Transitive / Dynamic" }
                    Scope = "Server-side NuGet"
                }
            }
        }
    }
}

Write-Host "Extracted $($ServerBOM.Count) Server-side NuGet references across $($CsProjFiles.Count) projects." -ForegroundColor Green

# [新增註釋資訊]-2026-10-20-13-45-00 2. Parse Frontend dependencies (versions.json & libman.json)
$ClientBOM = @()

$VersionsFiles = Get-ChildItem -Path $SolutionRoot -Filter "versions.json" -Recurse | Where-Object {
    $_.FullName -notmatch "\\(bin|obj|\.git)\\"
}
foreach ($vf in $VersionsFiles) {
    $vRaw = Get-Content $vf.FullName -Raw | ConvertFrom-Json
    foreach ($prop in $vRaw.PSObject.Properties) {
        $ClientBOM += [PSCustomObject]@{
            PackageId = $prop.Name
            Version = $prop.Value.version
            Provider = "npm (Official Registry)"
            Destination = "wwwroot/lib/"
            Scope = "Client-side NPM/Vendor"
        }
    }
}

# [原來註釋資訊] Parse Frontend LibMan Libraries
$LibManFiles = Get-ChildItem -Path $SolutionRoot -Filter "libman.json" -Recurse | Where-Object {
    $_.FullName -notmatch "\\(bin|obj|\.git)\\"
}
foreach ($lf in $LibManFiles) {
    $libmanRaw = Get-Content $lf.FullName -Raw | ConvertFrom-Json
    $defaultProvider = $libmanRaw.defaultProvider
    foreach ($lib in $libmanRaw.libraries) {
        $libStr = $lib.library
        $provider = if ($lib.provider) { $lib.provider } else { $defaultProvider }
        
        # Parse Library name and version (e.g. jquery@3.7.1)
        if ($libStr -match "^(?<name>@?[^@]+)@(?<ver>.+)$") {
            $name = $matches['name']
            $ver = $matches['ver']
        } else {
            $name = $libStr
            $ver = "Latest / Unspecified"
        }

        $ClientBOM += [PSCustomObject]@{
            PackageId = $name
            Version = $ver
            Provider = $provider
            Destination = $lib.destination
            Scope = "Client-side LibMan"
        }
    }
}
Write-Host "Extracted $($ClientBOM.Count) Client-side library specifications." -ForegroundColor Green

# Console Summary
Write-Host "`n[Server-side NuGet Highlights]" -ForegroundColor Yellow
$ServerBOM | Select-Object -Property Project, PackageId, Version | Format-Table -AutoSize | Out-String | Write-Host

Write-Host "`n[Client-side Library Highlights]" -ForegroundColor Yellow
$ClientBOM | Select-Object -Property PackageId, Version, Provider | Format-Table -AutoSize | Out-String | Write-Host

# Export Markdown if specified
if ($OutputMarkdownPath) {
    $md = "# NKUST-OOSA Automated Software Bill of Materials (BOM)`n`n"
    $md += "Generated at: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')`n`n"
    
    $md += "## Server-side NuGet Packages`n`n"
    $md += "| Project | Package ID | Version | Scope |`n"
    $md += "| :--- | :--- | :---: | :--- |`n"
    foreach ($item in $ServerBOM) {
        $md += "| $($item.Project) | $($item.PackageId) | $($item.Version) | $($item.Scope) |`n"
    }

    $md += "`n## Client-side Libraries (LibMan)`n`n"
    $md += "| Package ID | Version | Provider | Destination |`n"
    $md += "| :--- | :---: | :---: | :--- |`n"
    foreach ($item in $ClientBOM) {
        $md += "| $($item.PackageId) | $($item.Version) | $($item.Provider) | $($item.Destination) |`n"
    }

    [System.IO.File]::WriteAllText($OutputMarkdownPath, $md, [System.Text.Encoding]::UTF8)
    Write-Host "`nBOM report exported to: $OutputMarkdownPath" -ForegroundColor Green
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "[BOM] Extraction completed successfully." -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan
