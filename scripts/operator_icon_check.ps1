param()

$ErrorActionPreference = "Stop"

function Get-RepoRoot {
    return Resolve-Path (Join-Path $PSScriptRoot "..")
}

function Get-BlockMap {
    param(
        [string]$Content,
        [string]$Marker
    )

    $pattern = [regex]::Escape("const $Marker = [") + "(?<body>[\s\S]*?)\r?\n\];"
    $match = [regex]::Match($Content, $pattern)
    if (-not $match.Success) {
        throw "Block not found: $Marker"
    }

    $map = @{}
    foreach ($line in ($match.Groups["body"].Value -split "\r?\n")) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or -not $trimmed.Contains("|")) {
            continue
        }

        $parts = $trimmed.Split("|", 2, [System.StringSplitOptions]::None)
        $key = $parts[0].Trim()
        $value = $parts[1].Trim()
        if ($key -and $value) {
            $map[$key] = $value
        }
    }

    return $map
}

function Get-OperatorTypeNames {
    param([string]$Content)

    $match = [regex]::Match($Content, "public enum OperatorType\s*\{(?<body>[\s\S]*?)\r?\n\}")
    if (-not $match.Success) {
        throw "OperatorType enum block not found."
    }

    return [regex]::Matches($match.Groups["body"].Value, "^\s*([A-Za-z][A-Za-z0-9_]*)\s*=", "Multiline") |
        ForEach-Object { $_.Groups[1].Value } |
        Sort-Object -Unique
}

function Get-OperatorCatalog {
    param([string]$RepoRoot)

    foreach ($file in Get-ChildItem -Path $RepoRoot -Recurse -Filter *.json -File) {
        try {
            $raw = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
            $json = $raw | ConvertFrom-Json
        }
        catch {
            continue
        }

        if ($null -ne $json.totalCount -and $json.operators -is [System.Collections.IEnumerable]) {
            $first = $json.operators | Select-Object -First 1
            if ($null -ne $first -and $null -ne $first.id -and $null -ne $first.category) {
                return $json
            }
        }
    }

    throw "Operator catalog JSON not found."
}

$repoRoot = Get-RepoRoot
$visualPath = Join-Path $repoRoot "Acme.Product\src\Acme.Product.Desktop\wwwroot\src\shared\operatorVisuals.js"
$enumPath = Join-Path $repoRoot "Acme.Product\src\Acme.Product.Core\Enums\OperatorEnums.cs"
$appPath = Join-Path $repoRoot "Acme.Product\src\Acme.Product.Desktop\wwwroot\src\app.js"
$flowEditorPath = Join-Path $repoRoot "Acme.Product\src\Acme.Product.Desktop\wwwroot\src\features\flow-editor\flowEditorInteraction.js"
$visualContent = Get-Content -Path $visualPath -Raw -Encoding UTF8
$enumContent = Get-Content -Path $enumPath -Raw -Encoding UTF8
$catalog = Get-OperatorCatalog -RepoRoot $repoRoot

$aliasMap = Get-BlockMap -Content $visualContent -Marker "OPERATOR_ICON_ALIAS_BLOCKS"
$iconMap = Get-BlockMap -Content $visualContent -Marker "OPERATOR_ICON_BLOCKS"
$categoryMap = Get-BlockMap -Content $visualContent -Marker "CATEGORY_ICON_BLOCKS"
$enumTypes = Get-OperatorTypeNames -Content $enumContent

$runtimeMissing = @()
foreach ($type in $enumTypes) {
    if ($iconMap.ContainsKey($type)) {
        continue
    }

    if ($aliasMap.ContainsKey($type) -and $iconMap.ContainsKey($aliasMap[$type])) {
        continue
    }

    $runtimeMissing += $type
}

$canonicalTypes = $enumTypes | Where-Object { -not $aliasMap.ContainsKey($_) }
$canonicalMissingDirect = $canonicalTypes | Where-Object { -not $iconMap.ContainsKey($_) }

$catalogCategories = $catalog.operators | ForEach-Object { $_.category } | Sort-Object -Unique
$categoryMissing = $catalogCategories | Where-Object { -not $categoryMap.ContainsKey($_) }

$legacyOperatorConfigs = @()
if (Select-String -Path $appPath -SimpleMatch "const operatorConfigs = {" -Quiet) {
    $legacyOperatorConfigs += $appPath
}
if (Select-String -Path $flowEditorPath -SimpleMatch "const operatorConfigs = {" -Quiet) {
    $legacyOperatorConfigs += $flowEditorPath
}

Write-Host "Runtime operator count: $($enumTypes.Count)" -ForegroundColor Cyan
Write-Host "Canonical operator count: $($canonicalTypes.Count)" -ForegroundColor Cyan
Write-Host "Alias count in runtime enum: $($enumTypes.Count - $canonicalTypes.Count)" -ForegroundColor Cyan
Write-Host "Direct icon count: $($iconMap.Count)" -ForegroundColor Cyan
Write-Host "Category icon count: $($categoryMap.Count)" -ForegroundColor Cyan

if ($runtimeMissing.Count -gt 0) {
    Write-Error ("Missing runtime icon coverage: " + ($runtimeMissing -join ", "))
}

if ($canonicalMissingDirect.Count -gt 0) {
    Write-Error ("Missing direct canonical icons: " + ($canonicalMissingDirect -join ", "))
}

if ($categoryMissing.Count -gt 0) {
    Write-Error ("Missing category fallbacks: " + ($categoryMissing -join ", "))
}

if ($legacyOperatorConfigs.Count -gt 0) {
    Write-Error ("Legacy inline operatorConfigs remain in: " + ($legacyOperatorConfigs -join ", "))
}

Write-Host "Operator icon coverage check passed." -ForegroundColor Green
