param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$OutputRoot = "",
    [switch]$SkipPublish
)

$ErrorActionPreference = "Stop"

function Resolve-RepoRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ScriptRoot
    )

    return (Resolve-Path (Join-Path $ScriptRoot "..")).Path
}

function Resolve-FirstExistingPath {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Candidates
    )

    foreach ($candidate in $Candidates) {
        if ([string]::IsNullOrWhiteSpace($candidate)) {
            continue
        }

        if (Test-Path $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    return $null
}

function Ensure-Directory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

function Get-FlowParameter {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Operator,
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ($null -eq $Operator.parameters) {
        return $null
    }

    return $Operator.parameters | Where-Object { $_.name -eq $Name } | Select-Object -First 1
}

function Get-OrAddUniqueRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [hashtable]$Map,
        [Parameter(Mandatory = $true)]
        [string]$SourcePath,
        [Parameter(Mandatory = $true)]
        [string]$RelativeDirectory
    )

    $normalizedSource = [System.IO.Path]::GetFullPath($SourcePath)
    if ($Map.ContainsKey($normalizedSource)) {
        return $Map[$normalizedSource]
    }

    $fileName = [System.IO.Path]::GetFileName($normalizedSource)
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($normalizedSource)
    $extension = [System.IO.Path]::GetExtension($normalizedSource)
    $candidateName = $fileName
    $index = 1

    while ($Map.Values -contains (Join-Path $RelativeDirectory $candidateName)) {
        $candidateName = "{0}-{1}{2}" -f $baseName, $index, $extension
        $index++
    }

    $relativePath = (Join-Path $RelativeDirectory $candidateName).Replace("/", "\")
    $Map[$normalizedSource] = $relativePath
    return $relativePath
}

function Copy-FileWithParent {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SourceFile,
        [Parameter(Mandatory = $true)]
        [string]$DestinationFile
    )

    $parent = [System.IO.Path]::GetDirectoryName($DestinationFile)
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        Ensure-Directory $parent
    }

    Copy-Item -LiteralPath $SourceFile -Destination $DestinationFile -Force
}

function Update-AppSettingsForPortableDb {
    param(
        [Parameter(Mandatory = $true)]
        [string]$AppSettingsPath
    )

    if (-not (Test-Path $AppSettingsPath)) {
        return
    }

    $appSettings = Get-Content -LiteralPath $AppSettingsPath -Raw | ConvertFrom-Json
    if ($null -eq $appSettings.Database) {
        $appSettings | Add-Member -MemberType NoteProperty -Name Database -Value ([pscustomobject]@{})
    }

    $appSettings.Database.Path = "vision.db"
    $appSettings | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $AppSettingsPath -Encoding UTF8
}

function Update-ConfigForPortableStorage {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ConfigPath
    )

    if (-not (Test-Path $ConfigPath)) {
        return
    }

    $config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
    if ($null -eq $config.storage) {
        $config | Add-Member -MemberType NoteProperty -Name storage -Value ([pscustomobject]@{})
    }

    $config.storage.imageSavePath = "VisionData\Images"
    $config | ConvertTo-Json -Depth 32 | Set-Content -LiteralPath $ConfigPath -Encoding UTF8
}

function Patch-FlowFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FlowRoot,
        [Parameter(Mandatory = $true)]
        [hashtable]$ModelMap,
        [Parameter(Mandatory = $true)]
        [hashtable]$SampleMap,
        [Parameter(Mandatory = $true)]
        [string]$DefaultLabelsRelativePath
    )

    $modelSources = @{}
    $sampleSources = @{}

    if (-not (Test-Path $FlowRoot)) {
        return [pscustomobject]@{
            ModelSources = @()
            SampleSources = @()
        }
    }

    $flowFiles = Get-ChildItem -LiteralPath $FlowRoot -Filter *.json -File
    foreach ($flowFile in $flowFiles) {
        $flow = Get-Content -LiteralPath $flowFile.FullName -Raw | ConvertFrom-Json
        $changed = $false

        foreach ($operator in $flow.operators) {
            if ($operator.type -eq "DeepLearning") {
                $modelParam = Get-FlowParameter -Operator $operator -Name "ModelPath"
                if ($null -ne $modelParam -and -not [string]::IsNullOrWhiteSpace([string]$modelParam.value)) {
                    $modelSource = [System.IO.Path]::GetFullPath([string]$modelParam.value)
                    $modelSources[$modelSource] = $true

                    if (Test-Path $modelSource) {
                        $modelParam.value = Get-OrAddUniqueRelativePath -Map $ModelMap -SourcePath $modelSource -RelativeDirectory "portable-assets\models"
                        $changed = $true
                    }
                }

                $labelsParam = Get-FlowParameter -Operator $operator -Name "LabelsPath"
                if ($null -ne $labelsParam) {
                    $labelsParam.value = $DefaultLabelsRelativePath
                    $changed = $true
                }
            }

            if ($operator.type -eq "ImageAcquisition") {
                $fileParam = Get-FlowParameter -Operator $operator -Name "FilePath"
                if ($null -ne $fileParam -and -not [string]::IsNullOrWhiteSpace([string]$fileParam.value)) {
                    $sampleSource = [System.IO.Path]::GetFullPath([string]$fileParam.value)
                    $sampleSources[$sampleSource] = $true

                    if (Test-Path $sampleSource) {
                        $fileParam.value = Get-OrAddUniqueRelativePath -Map $SampleMap -SourcePath $sampleSource -RelativeDirectory "portable-assets\samples"
                        $changed = $true
                    }
                }
            }
        }

        if ($changed) {
            $flow | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $flowFile.FullName -Encoding UTF8
        }
    }

    return [pscustomobject]@{
        ModelSources = @($modelSources.Keys)
        SampleSources = @($sampleSources.Keys)
    }
}

function Write-LaunchScript {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TargetPath
    )

    $content = @'
@echo off
setlocal
pushd "%~dp0"

if not exist "%ProgramFiles(x86)%\Microsoft\EdgeWebView\Application" (
  echo [WARN] 未检测到 WebView2 Runtime。
  echo [WARN] 如启动后白屏或初始化失败，请先运行 "Prereqs\Install Prereqs.bat"。
  echo.
)

set "PATH=%~dp0HikvisionRuntime;%~dp0HikvisionRuntime\ThirdParty;%PATH%"
start "" "%~dp0Acme.Product.Desktop.exe"
'@

    Set-Content -LiteralPath $TargetPath -Value $content -Encoding ASCII
}

function Write-PrereqInstallerScript {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TargetPath
    )

    $content = @'
@echo off
setlocal
pushd "%~dp0"

if exist "VC_redist.x64.exe" (
  echo Installing VC++ runtime...
  start /wait "" "%~dp0VC_redist.x64.exe" /install /quiet /norestart
)

if exist "MicrosoftEdgeWebView2RuntimeInstallerX64.exe" (
  echo Installing WebView2 runtime...
  start /wait "" "%~dp0MicrosoftEdgeWebView2RuntimeInstallerX64.exe" /silent /install
)

if exist "windowsdesktop-runtime-8.0.22-win-x64.exe" (
  echo Installing .NET Desktop Runtime...
  start /wait "" "%~dp0windowsdesktop-runtime-8.0.22-win-x64.exe" /install /quiet /norestart
)

echo.
echo 现场依赖安装完成，请重新运行 "Launch ClearVision.bat"。
pause
'@

    Set-Content -LiteralPath $TargetPath -Value $content -Encoding ASCII
}

function Write-Readme {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TargetPath,
        [Parameter(Mandatory = $true)]
        [string]$PackageName,
        [Parameter(Mandatory = $true)]
        [string[]]$PackagedProjects
    )

    $projectLines = @()
    if ($PackagedProjects.Count -gt 0) {
        $projectLines = $PackagedProjects | Sort-Object -Unique | ForEach-Object { "- $_" }
    }
    else {
        $projectLines = @("- Project names could not be resolved from vision.db. Please verify vision.db and App_Data\\ProjectFlows manually.")
    }

    $lines = @(
        "ClearVision Portable Package",
        "============================",
        "",
        "Package: $PackageName",
        "",
        "Included content",
        "----------------"
    )

    $lines += $projectLines
    $lines += @(
        "- Active database: vision.db",
        "- Project flow files: App_Data\\ProjectFlows",
        "- Wire-sequence scenario package: scenario-package-wire-sequence",
        "- Portable models and samples: portable-assets",
        "- Huaray camera dependencies: MVSDK / GenICam DLLs in the package root",
        "- Hikvision camera runtime: HikvisionRuntime",
        "- Offline installers: Prereqs",
        "",
        "Recommended startup",
        "-------------------",
        "1. Launch via Launch ClearVision.bat.",
        "2. If WebView2 is missing or the app starts with a blank window, run Prereqs\\Install Prereqs.bat first.",
        "3. The packaged project still includes a sample image path for smoke testing. Switch to the on-site camera binding after deployment.",
        "",
        "Portable adjustments already applied",
        "----------------------------------",
        "- appsettings.json now points Database:Path to the packaged vision.db.",
        "- config.json now saves images to VisionData\\Images inside the package.",
        "- The current flow now uses packaged relative paths for the model and sample image.",
        "- scenario-package-wire-sequence\\models now contains the ONNX model and labels.txt.",
        "",
        "Notes",
        "-----",
        "- Use Launch ClearVision.bat if Hikvision cameras are needed, because it injects HikvisionRuntime into PATH.",
        "- The app is self-contained, but WebView2 / VC++ / .NET desktop installers are still included as offline fallbacks."
    )

    Set-Content -LiteralPath $TargetPath -Value ($lines -join [Environment]::NewLine) -Encoding UTF8
}

$repoRoot = Resolve-RepoRoot -ScriptRoot $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($OutputRoot)) {
    $OutputRoot = Join-Path $repoRoot "publish"
}

$desktopProject = Join-Path $repoRoot "Acme.Product\src\Acme.Product.Desktop\Acme.Product.Desktop.csproj"
$runtimeStateDir = Resolve-FirstExistingPath @(
    (Join-Path $repoRoot "Acme.Product\src\Acme.Product.Desktop\bin\Debug\net8.0-windows\win-x64"),
    (Join-Path $repoRoot ".tmp\single-desktop-out")
)
$localDbSource = Resolve-FirstExistingPath @(
    (Join-Path $env:LOCALAPPDATA "ClearVision\vision.db"),
    (Join-Path $repoRoot "vision.db"),
    (Join-Path $repoRoot "Acme.Product\src\Acme.Product.Desktop\vision.db")
)
$scenarioPackageSource = $null
$scenarioPackageDirectory = Get-ChildItem -Path $repoRoot -Directory -ErrorAction SilentlyContinue |
    ForEach-Object {
        Get-ChildItem -Path $_.FullName -Directory -Filter "scenario-package-wire-sequence" -ErrorAction SilentlyContinue
    } |
    Select-Object -First 1
if ($scenarioPackageDirectory) {
    $scenarioPackageSource = $scenarioPackageDirectory.FullName
}

$scenarioLabelsSource = if ($scenarioPackageSource) {
    Resolve-FirstExistingPath @(
        (Join-Path $scenarioPackageSource "labels\labels.txt")
    )
}
else {
    $null
}

$hikvisionRuntimeSource = "C:\Program Files (x86)\Common Files\MVS\Runtime\Win64_x64"

$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$packageName = "ClearVision-Portable-$timestamp"
$stagingDir = Join-Path $OutputRoot $packageName
$zipPath = Join-Path $OutputRoot "$packageName.zip"

Ensure-Directory $OutputRoot

if (Test-Path $stagingDir) {
    Remove-Item -LiteralPath $stagingDir -Recurse -Force
}

if (Test-Path $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

if (-not $SkipPublish) {
    Write-Host "Publishing self-contained desktop build..."
    & dotnet publish $desktopProject `
        -c $Configuration `
        -r $RuntimeIdentifier `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:PublishReadyToRun=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:EnableCompressionInSingleFile=true `
        -p:PublishTrimmed=false `
        -o $stagingDir

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed with exit code $LASTEXITCODE."
    }
}
else {
    Ensure-Directory $stagingDir
}

if (-not (Test-Path $stagingDir)) {
    throw "Publish output directory was not created: $stagingDir"
}

if ($null -eq $localDbSource) {
    throw "Could not find the active vision.db to package."
}

Write-Host "Copying runtime data..."
Copy-FileWithParent -SourceFile $localDbSource -DestinationFile (Join-Path $stagingDir "vision.db")

if ($null -ne $runtimeStateDir) {
    $runtimeConfig = Join-Path $runtimeStateDir "config.json"
    if (Test-Path $runtimeConfig) {
        Copy-FileWithParent -SourceFile $runtimeConfig -DestinationFile (Join-Path $stagingDir "config.json")
    }

    $runtimeAppData = Join-Path $runtimeStateDir "App_Data"
    if (Test-Path $runtimeAppData) {
        Copy-Item -LiteralPath $runtimeAppData -Destination (Join-Path $stagingDir "App_Data") -Recurse -Force
    }

    $runtimeInference = Join-Path $runtimeStateDir "inference"
    if (Test-Path $runtimeInference) {
        Copy-Item -LiteralPath $runtimeInference -Destination (Join-Path $stagingDir "inference") -Recurse -Force
    }
}

if (Test-Path $scenarioPackageSource) {
    Write-Host "Copying wire-sequence scenario package..."
    Copy-Item -LiteralPath $scenarioPackageSource -Destination (Join-Path $stagingDir "scenario-package-wire-sequence") -Recurse -Force
}

Update-AppSettingsForPortableDb -AppSettingsPath (Join-Path $stagingDir "appsettings.json")
Update-ConfigForPortableStorage -ConfigPath (Join-Path $stagingDir "config.json")

$portableAssetsRoot = Join-Path $stagingDir "portable-assets"
Ensure-Directory (Join-Path $portableAssetsRoot "models")
Ensure-Directory (Join-Path $portableAssetsRoot "samples")
Ensure-Directory (Join-Path $stagingDir "VisionData\Images")

$modelMap = @{}
$sampleMap = @{}
$defaultLabelsRelativePath = "portable-assets\models\labels.txt"
$patchResult = Patch-FlowFiles `
    -FlowRoot (Join-Path $stagingDir "App_Data\ProjectFlows") `
    -ModelMap $modelMap `
    -SampleMap $sampleMap `
    -DefaultLabelsRelativePath $defaultLabelsRelativePath

Write-Host "Copying external models and samples..."
foreach ($modelSource in $modelMap.Keys) {
    if (-not (Test-Path $modelSource)) {
        throw "Model referenced by the current flow does not exist: $modelSource"
    }

    $relativeModelPath = $modelMap[$modelSource]
    $targetModelPath = Join-Path $stagingDir $relativeModelPath
    Copy-FileWithParent -SourceFile $modelSource -DestinationFile $targetModelPath
}

$portableLabelsPath = Join-Path $portableAssetsRoot "models\labels.txt"
$firstModelSource = $modelMap.Keys | Select-Object -First 1
$siblingLabels = $null
if ($firstModelSource) {
    $candidateSiblingLabels = Join-Path (Split-Path $firstModelSource -Parent) "labels.txt"
    if (Test-Path $candidateSiblingLabels) {
        $siblingLabels = $candidateSiblingLabels
    }
}

if ($siblingLabels) {
    Copy-FileWithParent -SourceFile $siblingLabels -DestinationFile $portableLabelsPath
}
elseif ($scenarioLabelsSource) {
    Copy-FileWithParent -SourceFile $scenarioLabelsSource -DestinationFile $portableLabelsPath
}

foreach ($sampleSource in $sampleMap.Keys) {
    if (-not (Test-Path $sampleSource)) {
        Write-Warning "Sample image referenced by the current flow is missing: $sampleSource"
        continue
    }

    $relativeSamplePath = $sampleMap[$sampleSource]
    $targetSamplePath = Join-Path $stagingDir $relativeSamplePath
    Copy-FileWithParent -SourceFile $sampleSource -DestinationFile $targetSamplePath
}

if ($firstModelSource -and (Test-Path (Join-Path $stagingDir "scenario-package-wire-sequence\models"))) {
    $scenarioModelPath = Join-Path $stagingDir "scenario-package-wire-sequence\models\wire-seq-yolo-v1.2.onnx"
    Copy-FileWithParent -SourceFile $firstModelSource -DestinationFile $scenarioModelPath

    if (Test-Path $portableLabelsPath) {
        Copy-FileWithParent -SourceFile $portableLabelsPath -DestinationFile (Join-Path $stagingDir "scenario-package-wire-sequence\models\labels.txt")
    }
}

if (Test-Path $hikvisionRuntimeSource) {
    Write-Host "Copying Hikvision runtime..."
    $hikvisionRuntimeTarget = Join-Path $stagingDir "HikvisionRuntime"
    Ensure-Directory $hikvisionRuntimeTarget
    Copy-Item -Path (Join-Path $hikvisionRuntimeSource "*") -Destination $hikvisionRuntimeTarget -Recurse -Force
}

$prereqDir = Join-Path $stagingDir "Prereqs"
Ensure-Directory $prereqDir

Write-Host "Copying offline installers..."
foreach ($installerName in @(
    "MicrosoftEdgeWebView2RuntimeInstallerX64.exe",
    "VC_redist.x64.exe",
    "windowsdesktop-runtime-8.0.22-win-x64.exe"
)) {
    $installerSearchResult = Get-ChildItem -Path (Join-Path $env:USERPROFILE "Desktop") -Filter $installerName -Recurse -File -ErrorAction SilentlyContinue | Select-Object -First 1
    $installerSource = if ($installerSearchResult) { $installerSearchResult.FullName } else { $null }
    if (Test-Path $installerSource) {
        Copy-FileWithParent -SourceFile $installerSource -DestinationFile (Join-Path $prereqDir $installerName)
    }
}

Write-LaunchScript -TargetPath (Join-Path $stagingDir "Launch ClearVision.bat")
Write-PrereqInstallerScript -TargetPath (Join-Path $prereqDir "Install Prereqs.bat")

$packagedProjects = @()
try {
    $projectNames = & sqlite3 (Join-Path $stagingDir "vision.db") "SELECT Name FROM Projects ORDER BY Name;"
    if ($LASTEXITCODE -eq 0 -and $projectNames) {
        $packagedProjects = @($projectNames | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
    }
}
catch {
    Write-Warning "Failed to query project names from the packaged vision.db."
}

Write-Readme `
    -TargetPath (Join-Path $stagingDir "README-site-deploy.txt") `
    -PackageName $packageName `
    -PackagedProjects $packagedProjects

Write-Host "Creating ZIP archive..."
Push-Location $OutputRoot
try {
    tar -a -c -f $zipPath $packageName
    if ($LASTEXITCODE -ne 0) {
        throw "ZIP packaging failed with exit code $LASTEXITCODE."
    }
}
finally {
    Pop-Location
}

Write-Host ""
Write-Host "Portable package created:"
Write-Host "  Folder: $stagingDir"
Write-Host "  Zip:    $zipPath"
