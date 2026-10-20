# ==============================================================================
# NKUST-OOSA Project Structure Analyzer
# Scans solution, projects, namespaces and directory topology.
# ==============================================================================

param(
    [string]$SolutionPath = "",
    [string]$OutputPath = ""
)

# [新增註釋資訊]-2026-10-20-13-45-00 自動探測方案檔路徑
if ([string]::IsNullOrWhiteSpace($SolutionPath) -or (-not (Test-Path $SolutionPath))) {
    $candidates = @("NkustSSO.slnx", "NKUST.sln", "../../../NkustSSO.slnx", "../../../NKUST.sln")
    foreach ($cand in $candidates) {
        if (Test-Path $cand) {
            $SolutionPath = $cand
            break
        }
    }
    if (-not $SolutionPath -or (-not (Test-Path $SolutionPath))) {
        $found = Get-ChildItem -Path . -Filter "*.slnx" -Recurse | Select-Object -First 1
        if (-not $found) { $found = Get-ChildItem -Path . -Filter "*.sln" -Recurse | Select-Object -First 1 }
        if ($found) { $SolutionPath = $found.FullName }
    }
}

$RootDir = Split-Path -Parent $SolutionPath
if ([string]::IsNullOrWhiteSpace($RootDir)) { $RootDir = "." }
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "[ANALYSIS] Scanning Solution Structure ($SolutionPath)..." -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

if (-not (Test-Path $SolutionPath)) {
    Write-Error "Solution file not found at: $SolutionPath"
    exit 1
}

# [新增註釋資訊]-2026-10-20-13-45-00 自動判定方案格式 (.slnx / .sln) 並提取專案
$Projects = New-Object System.Collections.ArrayList

if ($SolutionPath -match '\.slnx$' -or ([System.IO.Path]::GetExtension($SolutionPath) -eq '.slnx')) {
    [xml]$slnxXml = Get-Content -Path $SolutionPath
    $projNodes = $slnxXml.SelectNodes("//Project[@Path]")
    foreach ($node in $projNodes) {
        $projRelPath = $node.Path
        if ($projRelPath -match '\.csproj$') {
            $projFullPath = [System.IO.Path]::GetFullPath((Join-Path $RootDir $projRelPath))
            $projName = [System.IO.Path]::GetFileNameWithoutExtension($projRelPath)
            [void]$Projects.Add([PSCustomObject]@{
                Name = $projName
                RelativePath = $projRelPath
                FullPath = $projFullPath
                Directory = [System.IO.Path]::GetDirectoryName($projFullPath)
            })
        }
    }
} else {
    # [原來註釋資訊] 既有 .sln 純文字行匹配解析邏輯
    $SlnLines = Get-Content -Path $SolutionPath
    foreach ($line in $SlnLines) {
        if ($line -match 'Project\("\{[A-F0-9\-]+\}"\)\s*=\s*"([^"]+)",\s*"([^"]+)"') {
            $projName = $matches[1]
            $projRelPath = $matches[2]
            if ($projRelPath -match '\.csproj$') {
                $projFullPath = Join-Path $RootDir $projRelPath
                [void]$Projects.Add([PSCustomObject]@{
                    Name = $projName
                    RelativePath = $projRelPath
                    FullPath = $projFullPath
                    Directory = Split-Path -Parent $projFullPath
                })
            }
        }
    }
}

Write-Host "Found $($Projects.Count) C# Projects in Solution:" -ForegroundColor Green

$SummaryReport = New-Object System.Collections.ArrayList
foreach ($proj in $Projects) {
    if (-not (Test-Path $proj.FullPath)) {
        Write-Warning "Project file missing: $($proj.FullPath)"
        continue
    }

    # Count C# files excluding bin/obj
    $csFiles = Get-ChildItem -Path $proj.Directory -Filter "*.cs" -Recurse | Where-Object {
        $_.FullName -notmatch "\\(bin|obj)\\"
    }

    $totalLines = 0
    foreach ($cs in $csFiles) {
        $lines = (Get-Content $cs.FullName -ErrorAction SilentlyContinue).Count
        $totalLines += $lines
    }

    # [新增註釋資訊]-2026-10-20-13-45-00 Extract Target Framework including Directory.Build.props
    [xml]$xml = Get-Content $proj.FullPath
    $targetFw = $xml.Project.PropertyGroup.TargetFramework
    if (-not $targetFw) {
        $targetFw = $xml.Project.PropertyGroup.TargetFrameworks
    }
    if (-not $targetFw) {
        $propsPath = Join-Path $RootDir "Directory.Build.props"
        if (Test-Path $propsPath) {
            [xml]$propsXml = Get-Content $propsPath
            $targetFw = $propsXml.Project.PropertyGroup.TargetFramework
        }
    }
    if (-not $targetFw) { $targetFw = "net10.0" }

    $info = [PSCustomObject]@{
        ProjectName = $proj.Name
        TargetFramework = ($targetFw -join "; ")
        FileCount = $csFiles.Count
        TotalLines = $totalLines
        Path = $proj.RelativePath
    }

    Write-Host "  - $($proj.Name): $($info.TargetFramework) | $($info.FileCount) cs files | $($info.TotalLines) lines" -ForegroundColor White
    [void]$SummaryReport.Add($info)
}

Write-Host "`n[Directory Topology Summary]" -ForegroundColor Yellow
$Directories = Get-ChildItem -Path $RootDir -Directory | Where-Object {
    $_.Name -notmatch "^\." -and $_.Name -notmatch "^(bin|obj)$"
}

foreach ($d in $Directories) {
    $subCount = (Get-ChildItem -Path $d.FullName -Directory).Count
    $fileCount = (Get-ChildItem -Path $d.FullName -File).Count
    Write-Host "  / $($d.Name) ($subCount subdirectories, $fileCount files)" -ForegroundColor Gray
}

# If OutputPath specified, export markdown table
if ($OutputPath) {
    $md = "# NKUST-OOSA Solution Structure Report`n`n"
    $md += "| Project Name | Target Framework | C# File Count | Total Code Lines | Path |`n"
    $md += "| :--- | :--- | :---: | :---: | :--- |`n"
    foreach ($item in $SummaryReport) {
        $md += "| $($item.ProjectName) | $($item.TargetFramework) | $($item.FileCount) | $($item.TotalLines) | $($item.Path) |`n"
    }
    [System.IO.File]::WriteAllText($OutputPath, $md, [System.Text.Encoding]::UTF8)
    Write-Host "`nReport saved to: $OutputPath" -ForegroundColor Green
}

Write-Host "`n==========================================================" -ForegroundColor Cyan
Write-Host "[ANALYSIS] Project structure scanning completed." -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan
