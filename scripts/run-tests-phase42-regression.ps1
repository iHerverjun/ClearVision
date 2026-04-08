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

$parameters = @{
    Project = $project
    FullyQualifiedName = @(
        "Phase42RegionProcessingOperatorTests",
        "Phase42MeasurementAndSignalOperatorTests",
        "LocalDeformableMatchingPhase42Tests",
        "PixelToWorldTransformOperatorTests",
        "PlanarMatchingOperatorTests",
        "ImageOutputLifecycleGuardTests",
        "OperatorMetadataMigrationTests",
        "OperatorContractReconciliationTests"
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
