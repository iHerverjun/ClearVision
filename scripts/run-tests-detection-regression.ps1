param(
    [ValidateSet("quiet", "minimal", "normal", "detailed", "diagnostic")]
    [string]$Verbosity = "minimal",

    [ValidateSet("regression", "detection-accuracy", "detection-stability", "all")]
    [string]$Gate = "regression",

    [string]$Configuration,

    [switch]$NoBuild,

    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Split-Path -Parent $scriptRoot
$runner = Join-Path $scriptRoot "run-dotnet-test-serial.ps1"
$project = Join-Path $repoRoot "Acme.Product\tests\Acme.Product.Tests\Acme.Product.Tests.csproj"

$regressionTestClasses = @(
    "AngleMeasurementOperatorTests",
    "CaliperToolOperatorTests",
    "CircleMeasurementOperatorTests",
    "ContourMeasurementOperatorTests",
    "GapMeasurementOperatorTests",
    "GeoMeasurementOperatorTests",
    "GeometricToleranceOperatorTests",
    "HistogramAnalysisOperatorTests",
    "LineLineDistanceOperatorTests",
    "LineMeasurementOperatorTests",
    "MeasureDistanceOperatorTests",
    "PixelStatisticsOperatorTests",
    "PointLineDistanceOperatorTests",
    "SharpnessEvaluationOperatorTests",
    "WidthMeasurementOperatorTests",
    "OperatorContractReconciliationTests"
)

$accuracyTestClasses = @(
    "DetectionSequenceJudgeOperatorTests",
    "EdgePairDefectOperatorTests",
    "SurfaceDefectDetectionOperatorTests",
    "DeepLearningOperatorTests",
    "ColorDetectionOperatorTests",
    "BlobDetectionOperatorTests",
    "AnomalyDetectionOperatorTests",
    "MatchingIndustrialAcceptanceTests",
    "WireSequenceScenarioPackageTests"
)

$stabilityTestClasses = @(
    "Integration.MatchingRegressionStabilityTests",
    "Integration.PerformanceAcceptanceTests",
    "OperatorContractReconciliationTests"
)

$selectedTestClasses = switch ($Gate) {
    "regression" { $regressionTestClasses }
    "detection-accuracy" { $accuracyTestClasses }
    "detection-stability" { $stabilityTestClasses }
    "all" { ($regressionTestClasses + $accuracyTestClasses + $stabilityTestClasses) | Select-Object -Unique }
    default { throw "Unsupported gate '$Gate'." }
}

Write-Host "[detection-regression] Gate=$Gate"
Write-Host "[detection-regression] Selected test classes: $($selectedTestClasses -join ', ')"

$parameters = @{
    Project = $project
    FullyQualifiedName = $selectedTestClasses
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
