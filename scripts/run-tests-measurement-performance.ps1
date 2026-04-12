param(
    [ValidateSet("quiet", "minimal", "normal", "detailed", "diagnostic")]
    [string]$Verbosity = "minimal",

    [ValidateSet("auto", "standard", "acceptance")]
    [string]$GateProfile = "auto",

    [string]$TightenFromUtc,

    [string]$Configuration,

    [switch]$NoBuild,

    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$runner = Join-Path $scriptRoot "run-dotnet-test-serial.ps1"
$project = Join-Path $repoRoot "Acme.Product\tests\Acme.Product.Tests\Acme.Product.Tests.csproj"

function Resolve-MeasurementPerfGateProfile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RequestedProfile,

        [string]$TightenFromUtcInput
    )

    if ($RequestedProfile -in @("standard", "acceptance")) {
        return [PSCustomObject]@{ Profile = $RequestedProfile; Reason = "explicit parameter" }
    }

    $envProfile = $env:CV_MEASUREMENT_PERF_GATE_PROFILE
    if ($envProfile -in @("standard", "acceptance")) {
        return [PSCustomObject]@{ Profile = $envProfile; Reason = "CV_MEASUREMENT_PERF_GATE_PROFILE override" }
    }

    $effectiveTightenFrom = $TightenFromUtcInput
    if ([string]::IsNullOrWhiteSpace($effectiveTightenFrom)) {
        $effectiveTightenFrom = $env:CV_MEASUREMENT_PERF_TIGHTEN_FROM_UTC
    }

    if (-not [string]::IsNullOrWhiteSpace($effectiveTightenFrom)) {
        $styles = [System.Globalization.DateTimeStyles]::AssumeUniversal -bor [System.Globalization.DateTimeStyles]::AdjustToUniversal
        $parsed = [DateTimeOffset]::MinValue
        if ([DateTimeOffset]::TryParse($effectiveTightenFrom, [System.Globalization.CultureInfo]::InvariantCulture, $styles, [ref]$parsed)) {
            if ([DateTimeOffset]::UtcNow -ge $parsed) {
                return [PSCustomObject]@{ Profile = "acceptance"; Reason = "auto tightened by CV_MEASUREMENT_PERF_TIGHTEN_FROM_UTC=$effectiveTightenFrom" }
            }
        }
    }

    return [PSCustomObject]@{ Profile = "standard"; Reason = "auto default" }
}

if (-not [string]::IsNullOrWhiteSpace($TightenFromUtc)) {
    $env:CV_MEASUREMENT_PERF_TIGHTEN_FROM_UTC = $TightenFromUtc
}

$resolvedProfile = Resolve-MeasurementPerfGateProfile -RequestedProfile $GateProfile -TightenFromUtcInput $TightenFromUtc
$effectiveGateProfile = $resolvedProfile.Profile

if ([string]::IsNullOrWhiteSpace($env:CV_MEASUREMENT_PERF_BUDGET_SCALE)) {
    $env:CV_MEASUREMENT_PERF_BUDGET_SCALE = if ($effectiveGateProfile -eq "acceptance") { "1.2" } else { "1.5" }
}

if ([string]::IsNullOrWhiteSpace($env:CV_MEASUREMENT_PERF_WARMUP_ITERS)) {
    $env:CV_MEASUREMENT_PERF_WARMUP_ITERS = "5"
}

if ([string]::IsNullOrWhiteSpace($env:CV_MEASUREMENT_PERF_MEASURE_ITERS)) {
    $env:CV_MEASUREMENT_PERF_MEASURE_ITERS = "24"
}

$env:CV_MEASUREMENT_PERF_GATE_PROFILE = $effectiveGateProfile

Write-Host "[measurement-perf] GateProfile=$effectiveGateProfile (reason: $($resolvedProfile.Reason))"
Write-Host "[measurement-perf] CV_MEASUREMENT_PERF_BUDGET_SCALE=$($env:CV_MEASUREMENT_PERF_BUDGET_SCALE)"
Write-Host "[measurement-perf] CV_MEASUREMENT_PERF_WARMUP_ITERS=$($env:CV_MEASUREMENT_PERF_WARMUP_ITERS)"
Write-Host "[measurement-perf] CV_MEASUREMENT_PERF_MEASURE_ITERS=$($env:CV_MEASUREMENT_PERF_MEASURE_ITERS)"

$parameters = @{
    Project = $project
    FullyQualifiedName = @(
        "MeasurementPerformanceBudgetAcceptanceTests"
    )
    Verbosity = $Verbosity
}

if (-not [string]::IsNullOrWhiteSpace($Configuration)) {
    $parameters.Configuration = $Configuration
}

if ($NoBuild) {
    $parameters.NoBuild = $true
}

if ($NoRestore) {
    $parameters.NoRestore = $true
}

& $runner @parameters
