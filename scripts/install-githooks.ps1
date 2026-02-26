param(
    [switch]$VerifyOnly
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$hookPath = Join-Path $repoRoot ".githooks"
$preCommitHook = Join-Path $hookPath "pre-commit"

if (-not (Test-Path -Path $preCommitHook -PathType Leaf)) {
    throw "Hook file not found: $preCommitHook"
}

if ($VerifyOnly) {
    Write-Host "[hooks] Found hook file: $preCommitHook"
    $configuredHookPath = git config --get core.hooksPath
    if ([string]::IsNullOrWhiteSpace($configuredHookPath)) {
        Write-Host "[hooks] core.hooksPath is not configured."
    }
    else {
        Write-Host "[hooks] core.hooksPath=$configuredHookPath"
    }
    return
}

Push-Location $repoRoot
try {
    git config core.hooksPath .githooks
}
finally {
    Pop-Location
}

Write-Host "[hooks] Installed. Git now uses '.githooks' as hooks path."
Write-Host "[hooks] To verify: ./scripts/install-githooks.ps1 -VerifyOnly"
