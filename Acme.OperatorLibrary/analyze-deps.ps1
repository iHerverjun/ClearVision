param(
    [string]$OutputDir = "analysis"
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$operatorsRoot = Join-Path $repoRoot "Acme.Product/src/Acme.Product.Infrastructure/Operators"
$outputRoot = Join-Path $scriptRoot $OutputDir

if (-not (Test-Path -Path $operatorsRoot -PathType Container)) {
    throw "Operators path not found: $operatorsRoot"
}

New-Item -Path $outputRoot -ItemType Directory -Force | Out-Null

$operatorFiles = Get-ChildItem -Path $operatorsRoot -Filter *.cs -Recurse -File

if ($operatorFiles.Count -eq 0) {
    throw "No operator source files found under: $operatorsRoot"
}

$targets = @(
    @{ Name = "OperatorBase"; Pattern = "\bOperatorBase\b"; Category = "Infrastructure"; Recommendation = "Keep a lightweight base class in package layer." },
    @{ Name = "ImageWrapper"; Pattern = "\bImageWrapper\b"; Category = "Infrastructure"; Recommendation = "Keep image abstraction stable; isolate memory strategy by profile." },
    @{ Name = "OperatorExecutionOutput"; Pattern = "\bOperatorExecutionOutput\b"; Category = "Core"; Recommendation = "Expose host-agnostic execution result model for package consumers." },
    @{ Name = "OperatorMetadata"; Pattern = "\bOperatorMetadata\b"; Category = "Core"; Recommendation = "Expose metadata DTO/contract and adapter." },
    @{ Name = "PortDefinition"; Pattern = "\bPortDefinition\b"; Category = "Core"; Recommendation = "Expose port DTO/contract and adapter." },
    @{ Name = "Operator"; Pattern = "\bOperator\s+@?operator\b|\bOperator\s+\w+\s*,"; Category = "Core"; Recommendation = "Wrap runtime operator entity behind request contract if host isolation is required." }
)

$dependencyRows = foreach ($target in $targets) {
    $matches = Select-String -Path $operatorFiles.FullName -Pattern $target.Pattern -AllMatches
    $fileList = @(
        $matches |
        Select-Object -ExpandProperty Path -Unique |
        ForEach-Object {
            $full = [System.IO.Path]::GetFullPath($_)
            if ($full.StartsWith($repoRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                $relative = $full.Substring($repoRoot.Length).TrimStart('\', '/')
            }
            else {
                $relative = $full
            }
            $relative.Replace("\", "/")
        }
    )

    $matchCount = 0
    foreach ($m in $matches) {
        $matchCount += $m.Matches.Count
    }

    [PSCustomObject]@{
        Name = $target.Name
        Category = $target.Category
        MatchCount = $matchCount
        FileCount = $fileList.Count
        Files = $fileList
        Recommendation = $target.Recommendation
    }
}

$usingMatches = Select-String `
    -Path $operatorFiles.FullName `
    -Pattern "^\s*using\s+(Acme\.Product\.(Core|Infrastructure)\.[A-Za-z0-9_.]+)\s*;" `
    -AllMatches

$namespaceCounter = @{}
foreach ($entry in $usingMatches) {
    foreach ($match in $entry.Matches) {
        $ns = $match.Groups[1].Value
        if (-not $namespaceCounter.ContainsKey($ns)) {
            $namespaceCounter[$ns] = 0
        }
        $namespaceCounter[$ns] += 1
    }
}

$namespaceRows = @(
    $namespaceCounter.GetEnumerator() |
    ForEach-Object {
        [PSCustomObject]@{
            Namespace = $_.Key
            RefCount = $_.Value
        }
    } |
    Sort-Object `
        @{ Expression = "RefCount"; Descending = $true }, `
        @{ Expression = "Namespace"; Descending = $false }
)

$report = [PSCustomObject]@{
    GeneratedAt = (Get-Date).ToString("o")
    OperatorFileCount = $operatorFiles.Count
    DependencyRows = $dependencyRows
    NamespaceRows = $namespaceRows
}

$jsonPath = Join-Path $outputRoot "dependency-report.json"
$mdPath = Join-Path $outputRoot "dependency-report.md"

$report | ConvertTo-Json -Depth 8 | Set-Content -Path $jsonPath -Encoding utf8

$md = New-Object System.Text.StringBuilder
[void]$md.AppendLine("# Operator Dependency Report")
[void]$md.AppendLine()
[void]$md.AppendLine(('> GeneratedAt: `{0}`' -f $report.GeneratedAt))
[void]$md.AppendLine(('> OperatorFiles: **{0}**' -f $report.OperatorFileCount))
[void]$md.AppendLine()
[void]$md.AppendLine("## Key Host Dependency Types")
[void]$md.AppendLine("| Type | Category | MatchCount | FileCount | Recommendation |")
[void]$md.AppendLine("|------|----------|-----------:|----------:|---------------|")
foreach ($row in $dependencyRows) {
    $safe = $row.Recommendation.Replace("|", "\|")
    [void]$md.AppendLine(('| `{0}` | {1} | {2} | {3} | {4} |' -f $row.Name, $row.Category, $row.MatchCount, $row.FileCount, $safe))
}

[void]$md.AppendLine()
[void]$md.AppendLine("## Namespace Usage (Top 20)")
[void]$md.AppendLine("| Namespace | RefCount |")
[void]$md.AppendLine("|-----------|---------:|")
foreach ($row in ($namespaceRows | Select-Object -First 20)) {
    [void]$md.AppendLine(('| `{0}` | {1} |' -f $row.Namespace, $row.RefCount))
}

[void]$md.AppendLine()
[void]$md.AppendLine("## Notes")
[void]$md.AppendLine('- This report is generated from `Acme.Product.Infrastructure/Operators/*.cs`.')
[void]$md.AppendLine("- MatchCount is text-pattern based and intended for migration prioritization, not semantic compilation truth.")
[void]$md.AppendLine('- For Phase 3.3, use this report together with abstraction adapters under `Acme.OperatorLibrary/src`.')

$md.ToString() | Set-Content -Path $mdPath -Encoding utf8

Write-Host "[deps] Operator files : $($report.OperatorFileCount)"
Write-Host "[deps] JSON report    : $jsonPath"
Write-Host "[deps] Markdown report: $mdPath"
