param(
    [string]$Configuration = "Debug"
)

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$repoRoot = Resolve-Path (Join-Path $scriptDir "..")
$testProject = Join-Path $repoRoot "Acme.Product\tests\Acme.Product.Tests\Acme.Product.Tests.csproj"

if (-not (Test-Path $testProject)) {
    throw "Test project not found: $testProject"
}

$filter = @(
    "FullyQualifiedName~OperatorContractReconciliationTests",
    "FullyQualifiedName~Sprint3_TypeConvertTests",
    "FullyQualifiedName~TriggerModuleOperatorTests",
    "FullyQualifiedName~CircleMeasurementOperatorTests",
    "FullyQualifiedName~ResultOutputOperatorTests",
    "FullyQualifiedName~ShapeMatchingOperatorTests",
    "FullyQualifiedName~WidthMeasurementOperatorTests"
) -join "|"

Write-Host "Running operator contract regression suite..." -ForegroundColor Cyan
dotnet test $testProject -c $Configuration --filter $filter
