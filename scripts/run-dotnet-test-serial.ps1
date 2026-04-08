param(
    [Parameter(Mandatory = $true)]
    [string]$Project,

    [string[]]$FullyQualifiedName,

    [string]$Filter,

    [ValidateSet("quiet", "minimal", "normal", "detailed", "diagnostic")]
    [string]$Verbosity = "minimal",

    [string]$Configuration,

    [switch]$NoBuild,

    [switch]$NoRestore,

    [int]$LockWaitSeconds = 30
)

$ErrorActionPreference = "Stop"

function Quote-Argument {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    if ($Value -match '[\s"`|]') {
        return '"' + ($Value -replace '"', '\"') + '"'
    }

    return $Value
}

if ($FullyQualifiedName.Count -gt 0 -and -not [string]::IsNullOrWhiteSpace($Filter)) {
    throw "Specify either -FullyQualifiedName or -Filter, not both."
}

$currentProcess = Get-CimInstance Win32_Process -Filter "ProcessId = $PID"
if ($currentProcess.CommandLine -like '*-File*run-dotnet-test-serial.ps1*') {
    throw "Invoke this script from the current PowerShell shell with: & './scripts/run-dotnet-test-serial.ps1' ... . Do not wrap it with 'powershell.exe -File', because Codex can hang on leaked child processes in that mode."
}

$normalizedFullyQualifiedName = @()
foreach ($value in $FullyQualifiedName) {
    if ([string]::IsNullOrWhiteSpace($value)) {
        continue
    }

    foreach ($part in ($value -split ',')) {
        $trimmedPart = $part.Trim()
        if (-not [string]::IsNullOrWhiteSpace($trimmedPart)) {
            $normalizedFullyQualifiedName += $trimmedPart
        }
    }
}

$resolvedProject = Resolve-Path -LiteralPath $Project
$projectPath = $resolvedProject.Path
$projectKey = $projectPath.ToLowerInvariant()
$sha256 = [System.Security.Cryptography.SHA256]::Create()
$hashBytes = $sha256.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($projectKey))
$hashText = [System.BitConverter]::ToString($hashBytes).Replace("-", "")
$mutexName = "Global\ClearVision.DotNetTest." + $hashText.Substring(0, 24)
$mutex = [System.Threading.Mutex]::new($false, $mutexName)
$lockAcquired = $false
$exitCode = 0

try {
    Write-Host "[dotnet-test] Waiting for project lock: $projectPath"

    try {
        $lockAcquired = $mutex.WaitOne([TimeSpan]::FromSeconds([Math]::Max($LockWaitSeconds, 0)))
    }
    catch [System.Threading.AbandonedMutexException] {
        $lockAcquired = $true
    }

    if (-not $lockAcquired) {
        throw "Timed out after $LockWaitSeconds seconds waiting to run dotnet test for $projectPath. Another run for the same project is still active."
    }

    $effectiveFilter = $Filter
    if ($normalizedFullyQualifiedName.Count -gt 0) {
        $filterParts = $normalizedFullyQualifiedName |
            ForEach-Object { "FullyQualifiedName~$_" }

        $effectiveFilter = $filterParts -join "|"
    }

    $arguments = @(
        "test"
        $projectPath
        "--nologo"
        "--verbosity"
        $Verbosity
    )

    if ($NoBuild) {
        $arguments += "--no-build"
    }

    if ($NoRestore) {
        $arguments += "--no-restore"
    }

    if (-not [string]::IsNullOrWhiteSpace($Configuration)) {
        $arguments += @("--configuration", $Configuration)
    }

    if (-not [string]::IsNullOrWhiteSpace($effectiveFilter)) {
        $arguments += @("--filter", $effectiveFilter)
    }

    $preview = "dotnet " + (($arguments | ForEach-Object { Quote-Argument $_ }) -join " ")
    Write-Host "[dotnet-test] Acquired project lock."

    if ($normalizedFullyQualifiedName.Count -gt 0) {
        Write-Host "[dotnet-test] Combined $($normalizedFullyQualifiedName.Count) FullyQualifiedName filters into one invocation."
    }

    Write-Host "[dotnet-test] $preview"

    & dotnet @arguments
    $exitCode = $LASTEXITCODE
}
finally {
    $sha256.Dispose()

    if ($lockAcquired) {
        [void]$mutex.ReleaseMutex()
    }

    $mutex.Dispose()
}

exit $exitCode
