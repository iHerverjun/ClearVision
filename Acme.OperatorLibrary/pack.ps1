param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$RunSmokeTest
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $scriptRoot "Acme.OperatorLibrary.csproj"
$nupkgPath = Join-Path $scriptRoot "nupkg"
$smokeTestPath = Join-Path $scriptRoot "tests/Acme.OperatorLibrary.SmokeTests/Acme.OperatorLibrary.SmokeTests.csproj"
$nugetConfigPath = Join-Path $scriptRoot "nuget.config"

Write-Host "[pack] Project: $projectPath"
Write-Host "[pack] Output : $nupkgPath"

dotnet pack $projectPath -c $Configuration -o $nupkgPath
if ($LASTEXITCODE -ne 0) {
    throw "[pack] dotnet pack failed with exit code $LASTEXITCODE"
}

if ($RunSmokeTest) {
    Write-Host "[pack] Running smoke tests with local package source..."
    dotnet restore $smokeTestPath --configfile $nugetConfigPath
    if ($LASTEXITCODE -ne 0) {
        throw "[pack] dotnet restore (smoke test) failed with exit code $LASTEXITCODE"
    }

    dotnet test $smokeTestPath -c $Configuration --no-restore
    if ($LASTEXITCODE -ne 0) {
        throw "[pack] dotnet test (smoke test) failed with exit code $LASTEXITCODE"
    }
}

Write-Host "[pack] Done."
