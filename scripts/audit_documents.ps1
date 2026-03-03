param(
    [string]$RootPath = (Get-Location).Path,
    [string]$AuditDate = (Get-Date -Format 'yyyy-MM-dd')
)

$ErrorActionPreference = 'Stop'

$excludeSegments = @(
    'node_modules',
    'bin',
    'obj',
    '.git',
    '.vs',
    'build_check',
    'nupkg',
    '文档归纳'
)

$docExt = @('.md', '.txt')
$archiveRoot = Join-Path $RootPath '文档归纳'

if (Test-Path -LiteralPath $archiveRoot) {
    Remove-Item -LiteralPath $archiveRoot -Recurse -Force
}

function Get-DocumentFiles {
    param(
        [string]$BasePath,
        [string[]]$Extensions,
        [string[]]$ExcludedSegments
    )

    Get-ChildItem -Path $BasePath -Recurse -File | Where-Object {
        $extOk = $Extensions -contains $_.Extension.ToLowerInvariant()
        if (-not $extOk) {
            return $false
        }

        $rel = $_.FullName.Substring($BasePath.Length + 1)
        $segments = $rel -split '[\\/]'
        foreach ($seg in $segments) {
            if ($ExcludedSegments -contains $seg) {
                return $false
            }
        }
        return $true
    } | ForEach-Object {
        $_.FullName.Substring($BasePath.Length + 1).Replace('/', '\')
    } | Sort-Object -Unique
}

function Get-AuditStatus {
    param(
        [int]$Checked,
        [int]$Unchecked,
        [int]$TodoHits
    )

    $status = '无核查项'
    $reason = '未检测到可核查任务项'

    if ($Checked + $Unchecked -gt 0) {
        if ($Unchecked -eq 0 -and $Checked -gt 0) {
            $status = '已完成'
            $reason = '任务清单均已勾选'
        } elseif ($Checked -gt 0 -and $Unchecked -gt 0) {
            $status = '进行中'
            $reason = '任务清单存在部分未勾选项'
        } elseif ($Checked -eq 0 -and $Unchecked -gt 0) {
            $status = '未完成'
            $reason = '任务清单尚未开始勾选'
        }
    } elseif ($TodoHits -gt 0) {
        $status = '未完成'
        $reason = '检测到待办关键词（TODO/待办/未完成/TBD/FIXME/WIP）'
    }

    return @{
        Status = $status
        Reason = $reason
    }
}

function Insert-AuditBlock {
    param(
        [string]$Text,
        [string]$Extension,
        [string]$Date,
        [string]$Status,
        [int]$Checked,
        [int]$Unchecked,
        [int]$TodoHits,
        [string]$Reason
    )

    $clean = [regex]::Replace(
        $Text,
        '(?s)<!-- DOC_AUDIT_STATUS_START -->.*?<!-- DOC_AUDIT_STATUS_END -->\r?\n*',
        ''
    )

    $nl = if ($clean -match "`r`n") { "`r`n" } else { "`n" }
    $total = $Checked + $Unchecked

    $blockLines = @(
        '<!-- DOC_AUDIT_STATUS_START -->',
        '## 文档审计状态（自动更新）',
        "- 审计日期：$Date",
        "- 完成状态：$Status",
        "- 任务统计：总计 $total，已完成 $Checked，未完成 $Unchecked，待办关键词命中 $TodoHits",
        "- 判定依据：$Reason",
        '<!-- DOC_AUDIT_STATUS_END -->',
        ''
    )
    $block = [string]::Join($nl, $blockLines)

    if ($Extension -eq 'md') {
        if ($clean -match '(?m)^#\s+.+$') {
            $updated = [regex]::Replace(
                $clean,
                '(?m)^#\s+.+$',
                { param($m) $m.Value + $nl + $nl + $block.TrimEnd("`r", "`n") },
                1
            )
            return $updated.TrimEnd("`r", "`n") + $nl
        }

        return $block + $clean.TrimStart("`r", "`n")
    }

    return $block + $clean.TrimStart("`r", "`n")
}

$docFiles = Get-DocumentFiles -BasePath $RootPath -Extensions $docExt -ExcludedSegments $excludeSegments
$results = New-Object System.Collections.Generic.List[object]

foreach ($rel in $docFiles) {
    $abs = Join-Path $RootPath $rel
    $text = Get-Content -Raw -LiteralPath $abs -Encoding UTF8
    if ($null -eq $text) {
        $text = ''
    }

    $clean = [regex]::Replace(
        $text,
        '(?s)<!-- DOC_AUDIT_STATUS_START -->.*?<!-- DOC_AUDIT_STATUS_END -->\r?\n*',
        ''
    )

    $checked = ([regex]::Matches($clean, '(?m)^\s*[-*]\s*\[[xX]\]')).Count
    $unchecked = ([regex]::Matches($clean, '(?m)^\s*[-*]\s*\[\s\]')).Count
    $todoHits = ([regex]::Matches($clean, '(?im)\bTODO\b|待办|未完成|TBD|FIXME|WIP')).Count

    $audit = Get-AuditStatus -Checked $checked -Unchecked $unchecked -TodoHits $todoHits
    $status = $audit.Status
    $reason = $audit.Reason
    $ext = [System.IO.Path]::GetExtension($rel).ToLowerInvariant().TrimStart('.')
    $actionable = ($checked + $unchecked -gt 0) -or ($todoHits -gt 0)

    if ($actionable) {
        $updated = Insert-AuditBlock `
            -Text $text `
            -Extension $ext `
            -Date $AuditDate `
            -Status $status `
            -Checked $checked `
            -Unchecked $unchecked `
            -TodoHits $todoHits `
            -Reason $reason
        [System.IO.File]::WriteAllText($abs, $updated, [System.Text.UTF8Encoding]::new($false))
    }

    $results.Add([pscustomobject]@{
        Path = $rel
        Ext = $ext
        Status = $status
        Checked = $checked
        Unchecked = $unchecked
        TodoHits = $todoHits
        Actionable = $actionable
    }) | Out-Null
}

New-Item -ItemType Directory -Path $archiveRoot -Force | Out-Null
$statusOrder = @('已完成', '进行中', '未完成', '无核查项')
$typeMap = @{
    md = 'Markdown'
    txt = 'Text'
}

foreach ($status in $statusOrder) {
    $groups = $results | Where-Object { $_.Status -eq $status } | Group-Object Ext | Sort-Object Name
    foreach ($group in $groups) {
        $ext = $group.Name
        $typeName = if ($typeMap.ContainsKey($ext)) { $typeMap[$ext] } else { $ext.ToUpperInvariant() }
        $dir = Join-Path (Join-Path $archiveRoot $status) $typeName
        New-Item -ItemType Directory -Path $dir -Force | Out-Null

        $lines = New-Object System.Collections.Generic.List[string]
        $lines.Add("# 文件清单 - $status / $typeName") | Out-Null
        $lines.Add('') | Out-Null
        $lines.Add("- 生成日期：$AuditDate") | Out-Null
        $lines.Add("- 文件数量：$($group.Count)") | Out-Null
        $lines.Add('') | Out-Null
        $lines.Add('| 文件路径 | 已完成 | 未完成 | 待办命中 |') | Out-Null
        $lines.Add('|---|---:|---:|---:|') | Out-Null

        foreach ($item in ($group.Group | Sort-Object Path)) {
            $p = $item.Path.Replace('|', '\|')
            $lines.Add("| $p | $($item.Checked) | $($item.Unchecked) | $($item.TodoHits) |") | Out-Null
        }

        [System.IO.File]::WriteAllLines(
            (Join-Path $dir '文件清单.md'),
            $lines,
            [System.Text.UTF8Encoding]::new($false)
        )
    }
}

$summaryLines = New-Object System.Collections.Generic.List[string]
$summaryLines.Add('# 文档审计总览') | Out-Null
$summaryLines.Add('') | Out-Null
$summaryLines.Add("- 审计日期：$AuditDate") | Out-Null
$summaryLines.Add('- 审计范围：`*.md`、`*.txt`（已排除 `node_modules/bin/obj/.git/.vs/build_check/nupkg/文档归纳`）') | Out-Null
$summaryLines.Add("- 文档总数：$($results.Count)") | Out-Null
$summaryLines.Add("- 已回写审计状态块：$((($results | Where-Object { $_.Actionable }).Count))") | Out-Null
$summaryLines.Add('') | Out-Null
$summaryLines.Add('## 状态统计') | Out-Null
$summaryLines.Add('') | Out-Null
$summaryLines.Add('| 状态 | 数量 |') | Out-Null
$summaryLines.Add('|---|---:|') | Out-Null
foreach ($status in $statusOrder) {
    $summaryLines.Add("| $status | $((($results | Where-Object { $_.Status -eq $status }).Count)) |") | Out-Null
}
$summaryLines.Add('') | Out-Null
$summaryLines.Add('## 类型统计') | Out-Null
$summaryLines.Add('') | Out-Null
$summaryLines.Add('| 类型 | 数量 |') | Out-Null
$summaryLines.Add('|---|---:|') | Out-Null
foreach ($group in ($results | Group-Object Ext | Sort-Object Name)) {
    $typeName = if ($typeMap.ContainsKey($group.Name)) { $typeMap[$group.Name] } else { $group.Name.ToUpperInvariant() }
    $summaryLines.Add("| $typeName | $($group.Count) |") | Out-Null
}
$summaryLines.Add('') | Out-Null
$summaryLines.Add('## 归纳目录') | Out-Null
$summaryLines.Add('') | Out-Null
$summaryLines.Add('- `文档归纳/已完成/<类型>/文件清单.md`') | Out-Null
$summaryLines.Add('- `文档归纳/进行中/<类型>/文件清单.md`') | Out-Null
$summaryLines.Add('- `文档归纳/未完成/<类型>/文件清单.md`') | Out-Null
$summaryLines.Add('- `文档归纳/无核查项/<类型>/文件清单.md`') | Out-Null

[System.IO.File]::WriteAllLines(
    (Join-Path $archiveRoot '审计总览.md'),
    $summaryLines,
    [System.Text.UTF8Encoding]::new($false)
)

$results | Sort-Object Path | ConvertTo-Json -Depth 4 | Out-File -FilePath (Join-Path $archiveRoot 'audit-results.json') -Encoding utf8

$output = @(
    'AUDIT_DONE',
    "TOTAL=$($results.Count)",
    "ACTIONABLE=$((($results | Where-Object { $_.Actionable }).Count))",
    "COMPLETED=$((($results | Where-Object { $_.Status -eq '已完成' }).Count))",
    "IN_PROGRESS=$((($results | Where-Object { $_.Status -eq '进行中' }).Count))",
    "PENDING=$((($results | Where-Object { $_.Status -eq '未完成' }).Count))",
    "NO_ACTION=$((($results | Where-Object { $_.Status -eq '无核查项' }).Count))"
)

$output -join "`n"
