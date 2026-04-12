param(
    [ValidateSet("quiet", "minimal", "normal", "detailed", "diagnostic")]
    [string]$Verbosity = "minimal",

    [string]$Configuration,

    [switch]$NoBuild,

    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$runner = Join-Path $scriptRoot "run-dotnet-test-serial.ps1"
$project = Join-Path $repoRoot "Acme.Product\tests\Acme.Product.Tests\Acme.Product.Tests.csproj"

$testClasses = @(
    "LineMeasurementOperatorTests",
    "LineLineDistanceOperatorTests",
    "GeoMeasurementOperatorTests",
    "MeasureDistanceOperatorTests",
    "PointLineDistanceOperatorTests",
    "WidthMeasurementOperatorTests",
    "ColorMeasurementOperatorTests",
    "HistogramAnalysisOperatorTests",
    "GeometricFittingOperatorTests",
    "GeometricToleranceOperatorTests",
    "Week11_TextureColorFlowIntegrationTests"
)

Write-Host "[measurement-accuracy] Selected test classes: $($testClasses -join ', ')"

$parameters = @{
    Project = $project
    FullyQualifiedName = $testClasses
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
