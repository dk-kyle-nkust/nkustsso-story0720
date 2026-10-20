# ==============================================================================
# NKUST-OOSA Mermaid Dependency Topology Generator
# Parses solution ProjectReferences and outputs valid Mermaid graph syntax.
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
Write-Host "[MERMAID] Generating Solution Dependency Graph from $SolutionRoot..." -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

$CsProjFiles = Get-ChildItem -Path $SolutionRoot -Filter "*.csproj" -Recurse | Where-Object {
    $_.FullName -notmatch "\\(bin|obj|\.agents)\\"
}

$Edges = @()
$ProjectNodes = @{}

foreach ($proj in $CsProjFiles) {
    [xml]$xml = Get-Content $proj.FullName
    $sourceProj = [System.IO.Path]::GetFileNameWithoutExtension($proj.Name)
    $ProjectNodes[$sourceProj] = $true

    $projRefs = $xml.Project.ItemGroup.ProjectReference
    if ($projRefs) {
        foreach ($ref in $projRefs) {
            $targetPath = $ref.Include
            if (-not [string]::IsNullOrWhiteSpace($targetPath)) {
                $targetProj = [System.IO.Path]::GetFileNameWithoutExtension($targetPath)
                if (-not [string]::IsNullOrWhiteSpace($targetProj)) {
                    $Edges += [PSCustomObject]@{
                        From = $sourceProj
                        To = $targetProj
                    }
                }
            }
        }
    }
}

# Construct Mermaid graph
$sb = [System.Text.StringBuilder]::new()
[void]$sb.AppendLine('```mermaid')
[void]$sb.AppendLine('graph TD')
[void]$sb.AppendLine('    classDef host fill:#fff3e0,stroke:#e65100,stroke-width:2px,color:#bf360c;')
[void]$sb.AppendLine('    classDef lib fill:#e8f5e9,stroke:#2e7d32,stroke-width:2px,color:#1b5e20;')
[void]$sb.AppendLine('')

foreach ($node in $ProjectNodes.Keys) {
    $cls = if ($node -match 'Web') { 'host' } else { 'lib' }
    $line = '    ' + $node + '["' + $node + '"]:::' + $cls
    [void]$sb.AppendLine($line)
}

[void]$sb.AppendLine('')
foreach ($edge in $Edges) {
    $line = '    ' + $edge.From + ' -->|references| ' + $edge.To
    [void]$sb.AppendLine($line)
}

[void]$sb.AppendLine('```')
$mermaidCode = $sb.ToString()

Write-Host "`nGenerated Mermaid Code:" -ForegroundColor Yellow
Write-Host $mermaidCode -ForegroundColor White

if ($OutputMarkdownPath) {
    [System.IO.File]::WriteAllText($OutputMarkdownPath, $mermaidCode, [System.Text.Encoding]::UTF8)
    Write-Host "Exported to: $OutputMarkdownPath" -ForegroundColor Green
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "[MERMAID] Dependency graph generation completed." -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan
