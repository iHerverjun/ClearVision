param(
    [ValidateSet("quiet", "minimal", "normal", "detailed", "diagnostic")]
    [string]$Verbosity = "minimal",

    [ValidateSet("regression", "integration", "all")]
    [string]$Gate = "all",

    [string]$Configuration,

    [switch]$NoBuild,

    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$runner = Join-Path $scriptRoot "run-dotnet-test-serial.ps1"
$project = Join-Path $repoRoot "Acme.Product\tests\Acme.Product.Tests\Acme.Product.Tests.csproj"
$resultsDirectory = Join-Path $repoRoot "test_results"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$logFileName = "calibration-$Gate-$timestamp.trx"
$repoDotnetHome = Join-Path $repoRoot ".dotnet-home"
$repoNuGetPackages = Join-Path $repoRoot ".dotnet\.nuget\packages"

if ([string]::IsNullOrWhiteSpace($env:DOTNET_CLI_HOME) -and (Test-Path $repoDotnetHome)) {
    $env:DOTNET_CLI_HOME = $repoDotnetHome
}

if ([string]::IsNullOrWhiteSpace($env:NUGET_PACKAGES) -and (Test-Path $repoNuGetPackages)) {
    $env:NUGET_PACKAGES = $repoNuGetPackages
}

if ([string]::IsNullOrWhiteSpace($env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE)) {
    $env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
}

if ([string]::IsNullOrWhiteSpace($env:DOTNET_NOLOGO)) {
    $env:DOTNET_NOLOGO = "1"
}

$regressionTestClasses = @(
    "CalibrationLoaderOperatorTests",
    "CameraCalibrationOperatorTests",
    "CoordinateTransformOperatorTests",
    "FisheyeCalibrationOperatorTests",
    "FisheyeUndistortOperatorTests",
    "HandEyeCalibrationOperatorTests",
    "NPointCalibrationOperatorTests",
    "PixelToWorldTransformOperatorTests",
    "StereoCalibrationOperatorTests",
    "TranslationRotationCalibrationOperatorTests",
    "UndistortOperatorTests"
)

$integrationTestClasses = @(
    "Integration.CalibrationV2IntegrationTests",
    "Integration.LegacyCalibrationContractAuditTests"
)

$selectedTestClasses = switch ($Gate) {
    "regression" { $regressionTestClasses }
    "integration" { $integrationTestClasses }
    "all" { ($regressionTestClasses + $integrationTestClasses) | Select-Object -Unique }
    default { throw "Unsupported gate '$Gate'." }
}

Write-Host "[calibration-regression] Gate=$Gate"
Write-Host "[calibration-regression] Selected test classes: $($selectedTestClasses -join ', ')"

$parameters = @{
    Project = $project
    FullyQualifiedName = $selectedTestClasses
    Verbosity = $Verbosity
    ResultsDirectory = $resultsDirectory
    LogFileName = $logFileName
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
