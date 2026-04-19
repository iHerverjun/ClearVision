param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$RunSmokeTest,
    [string]$PackageVersion = "",
    [string]$SourceRevisionId = "",
    [string]$RepositoryBranch = "",
    [string]$RepositoryCommit = ""
)

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectPath = Join-Path $scriptRoot "Acme.OperatorLibrary.csproj"
$nupkgPath = Join-Path $scriptRoot "nupkg"
$smokeTestPath = Join-Path $scriptRoot "tests/Acme.OperatorLibrary.SmokeTests/Acme.OperatorLibrary.SmokeTests.csproj"
$nugetConfigPath = Join-Path $scriptRoot "nuget.config"

if ([string]::IsNullOrWhiteSpace($PackageVersion)) {
    $PackageVersion = $env:ACME_OPERATORLIB_PACKAGE_VERSION
}

if ([string]::IsNullOrWhiteSpace($SourceRevisionId)) {
    $SourceRevisionId = $env:SOURCE_REVISION_ID
}

if ([string]::IsNullOrWhiteSpace($RepositoryBranch)) {
    $RepositoryBranch = $env:BUILD_SOURCEBRANCHNAME
}

if ([string]::IsNullOrWhiteSpace($RepositoryBranch)) {
    $RepositoryBranch = $env:GITHUB_REF_NAME
}

if ([string]::IsNullOrWhiteSpace($RepositoryCommit)) {
    $RepositoryCommit = $env:BUILD_SOURCEVERSION
}

if ([string]::IsNullOrWhiteSpace($RepositoryCommit)) {
    $RepositoryCommit = $env:GITHUB_SHA
}

if ([string]::IsNullOrWhiteSpace($SourceRevisionId)) {
    $SourceRevisionId = $RepositoryCommit
}

$packProperties = @()
if (-not [string]::IsNullOrWhiteSpace($PackageVersion)) {
    $packProperties += "-p:PackageVersion=$PackageVersion"
}
if (-not [string]::IsNullOrWhiteSpace($SourceRevisionId)) {
    $packProperties += "-p:SourceRevisionId=$SourceRevisionId"
}
if (-not [string]::IsNullOrWhiteSpace($RepositoryBranch)) {
    $packProperties += "-p:RepositoryBranch=$RepositoryBranch"
}
if (-not [string]::IsNullOrWhiteSpace($RepositoryCommit)) {
    $packProperties += "-p:RepositoryCommit=$RepositoryCommit"
}

Write-Host "[pack] Project: $projectPath"
Write-Host "[pack] Output : $nupkgPath"
if (-not [string]::IsNullOrWhiteSpace($PackageVersion)) {
    Write-Host "[pack] PackageVersion     : $PackageVersion"
}
if (-not [string]::IsNullOrWhiteSpace($SourceRevisionId)) {
    Write-Host "[pack] SourceRevisionId   : $SourceRevisionId"
}
if (-not [string]::IsNullOrWhiteSpace($RepositoryBranch)) {
    Write-Host "[pack] RepositoryBranch   : $RepositoryBranch"
}
if (-not [string]::IsNullOrWhiteSpace($RepositoryCommit)) {
    Write-Host "[pack] RepositoryCommit   : $RepositoryCommit"
}

dotnet pack $projectPath -c $Configuration -o $nupkgPath @packProperties
if ($LASTEXITCODE -ne 0) {
    throw "[pack] dotnet pack failed with exit code $LASTEXITCODE"
}

$resolvedPackageVersion = $PackageVersion
if ([string]::IsNullOrWhiteSpace($resolvedPackageVersion)) {
    $resolvedPackageVersion = (dotnet msbuild $projectPath -nologo -getProperty:PackageVersion).Trim()
}

$expectedPackageName = "Acme.OperatorLibrary.$resolvedPackageVersion.nupkg"
$expectedPackagePath = Join-Path $nupkgPath $expectedPackageName
if (-not (Test-Path -LiteralPath $expectedPackagePath)) {
    throw "[pack] expected package was not produced: $expectedPackagePath"
}

if ($RunSmokeTest) {
    Write-Host "[pack] Running package acceptance tests with local package source..."

    dotnet restore $smokeTestPath `
        --configfile $nugetConfigPath `
        --source $nupkgPath `
        --source "https://api.nuget.org/v3/index.json" `
        -p:AcmeOperatorLibraryPackageVersion=$resolvedPackageVersion
    if ($LASTEXITCODE -ne 0) {
        throw "[pack] dotnet restore (smoke test) failed with exit code $LASTEXITCODE"
    }

    dotnet test $smokeTestPath -c $Configuration --no-restore -p:AcmeOperatorLibraryPackageVersion=$resolvedPackageVersion
    if ($LASTEXITCODE -ne 0) {
        throw "[pack] dotnet test (smoke test) failed with exit code $LASTEXITCODE"
    }
}

Write-Host "[pack] Done. PackageVersion=$resolvedPackageVersion"
