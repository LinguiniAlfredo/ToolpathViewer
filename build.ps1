<#
.SYNOPSIS
    Build and packaging script for ToolpathViewer.

.DESCRIPTION
    Compiles, tests, and publishes ToolpathViewer into a self-contained,
    single-file standalone executable for Windows.

.PARAMETER Target
    The build target to execute: 'Publish' (default), 'Build', 'Test', 'Clean'.

.PARAMETER Configuration
    Build configuration: 'Release' (default) or 'Debug'.

.PARAMETER Runtime
    Target runtime identifier (RID). Default is 'win-x64'.

.PARAMETER OutputDir
    Destination folder for published artifacts. Default is './artifacts'.

.PARAMETER NoSelfContained
    Do not bundle the .NET runtime with the output executable.

.PARAMETER NoSingleFile
    Do not bundle into a single executable file.

.PARAMETER NoReadyToRun
    Disable Ahead-Of-Time ReadyToRun (R2R) compilation.

.PARAMETER NoCompression
    Disable internal single-file compression.

.PARAMETER SkipTests
    Skip running unit tests before publishing.

.PARAMETER Verbosity
    MSBuild verbosity level: 'quiet', 'minimal' (default), 'normal', 'detailed', 'diagnostic'.

.EXAMPLE
    .\build.ps1
    Publishes a Release win-x64 standalone single-file executable to ./artifacts

.EXAMPLE
    .\build.ps1 -Target Test
    Runs all unit tests.

.EXAMPLE
    .\build.ps1 -Target Publish -OutputDir "C:\Tools\ToolpathViewer" -SkipTests
    Publishes directly to a custom destination without running tests.
#>

[CmdletBinding()]
param(
    [ValidateSet('Publish', 'Build', 'Test', 'Clean')]
    [string]$Target = 'Publish',

    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$Runtime = 'win-x64',

    [string]$OutputDir = '',

    [switch]$NoSelfContained,
    [switch]$NoSingleFile,
    [switch]$NoReadyToRun,
    [switch]$NoCompression,
    [switch]$SkipTests,

    [ValidateSet('quiet', 'minimal', 'normal', 'detailed', 'diagnostic')]
    [string]$Verbosity = 'minimal'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Determine root paths robustly across PowerShell editions
$ScriptRoot = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }
if (-not $ScriptRoot) {
    $ScriptRoot = Get-Location
}

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = Join-Path $ScriptRoot "artifacts"
}
elseif (-not [System.IO.Path]::IsPathRooted($OutputDir)) {
    $OutputDir = [System.IO.Path]::GetFullPath((Join-Path (Get-Location) $OutputDir))
}

$Stopwatch = [System.Diagnostics.Stopwatch]::StartNew()

function Write-Banner([string]$title, [string]$color = "Cyan") {
    Write-Host ""
    Write-Host ("=" * 60) -ForegroundColor $color
    Write-Host "  $title" -ForegroundColor $color
    Write-Host ("=" * 60) -ForegroundColor $color
    Write-Host ""
}

function Write-Success([string]$message) {
    Write-Host "[SUCCESS] $message" -ForegroundColor Green
}

function Write-Info([string]$message) {
    Write-Host "[INFO]    $message" -ForegroundColor Cyan
}

function Write-Warn([string]$message) {
    Write-Host "[WARNING] $message" -ForegroundColor Yellow
}

function Write-Err([string]$message) {
    Write-Host "[ERROR]   $message" -ForegroundColor Red
}

function Test-DotnetSdk {
    try {
        $dotnetVersion = (& dotnet --version).Trim()
        Write-Info "Using .NET SDK version: $dotnetVersion"
    }
    catch {
        Write-Err ".NET SDK not found. Please install .NET 10 SDK: https://dotnet.microsoft.com/download"
        exit 1
    }
}

function Invoke-Clean {
    Write-Banner "CLEANING OUTPUT DIRECTORIES"
    
    $cleanPaths = @(
        $OutputDir,
        (Join-Path $ScriptRoot "src/ToolpathViewer.App/bin"),
        (Join-Path $ScriptRoot "src/ToolpathViewer.App/obj"),
        (Join-Path $ScriptRoot "src/ToolpathViewer.Core/bin"),
        (Join-Path $ScriptRoot "src/ToolpathViewer.Core/obj"),
        (Join-Path $ScriptRoot "src/ToolpathViewer.Rendering/bin"),
        (Join-Path $ScriptRoot "src/ToolpathViewer.Rendering/obj"),
        (Join-Path $ScriptRoot "tests/ToolpathViewer.Tests/bin"),
        (Join-Path $ScriptRoot "tests/ToolpathViewer.Tests/obj")
    )

    foreach ($path in $cleanPaths) {
        if (Test-Path $path) {
            Write-Info "Removing: $path"
            Remove-Item -Path $path -Recurse -Force -ErrorAction SilentlyContinue
        }
    }

    Write-Success "Clean completed."
}

function Invoke-Build {
    Write-Banner "BUILDING SOLUTION ($Configuration)"
    $solutionPath = Join-Path $ScriptRoot "ToolpathViewer.slnx"
    
    & dotnet build $solutionPath `
        --configuration $Configuration `
        --verbosity $Verbosity

    if ($LASTEXITCODE -ne 0) {
        Write-Err "Build failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }

    Write-Success "Solution build succeeded."
}

function Invoke-Test {
    Write-Banner "RUNNING UNIT TESTS ($Configuration)"
    $testProject = Join-Path $ScriptRoot "tests/ToolpathViewer.Tests/ToolpathViewer.Tests.csproj"

    & dotnet test $testProject `
        --configuration $Configuration `
        --no-build `
        --verbosity $Verbosity `
        --logger "console;verbosity=normal"

    if ($LASTEXITCODE -ne 0) {
        Write-Err "Unit tests failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }

    Write-Success "All tests passed."
}

function Invoke-Publish {
    Write-Banner "PUBLISHING STANDALONE EXECUTABLE"

    $appProject = Join-Path $ScriptRoot "src/ToolpathViewer.App/ToolpathViewer.App.csproj"
    $selfContained = -not $NoSelfContained
    $singleFile = -not $NoSingleFile
    $readyToRun = -not $NoReadyToRun
    $compression = -not $NoCompression

    Write-Info "Target Project   : $appProject"
    Write-Info "Configuration    : $Configuration"
    Write-Info "Runtime (RID)    : $Runtime"
    Write-Info "Output Directory : $OutputDir"
    Write-Info "Self-Contained   : $selfContained"
    Write-Info "Single-File      : $singleFile"
    Write-Info "ReadyToRun (R2R) : $readyToRun"
    Write-Info "Compression      : $compression"
    Write-Host ""

    # Ensure clean output destination
    if (Test-Path $OutputDir) {
        Remove-Item -Path $OutputDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

    $publishArgs = @(
        "publish",
        $appProject,
        "-c", $Configuration,
        "-r", $Runtime,
        "--self-contained", $selfContained.ToString().ToLower(),
        "-p:PublishSingleFile=$($singleFile.ToString().ToLower())",
        "-p:PublishReadyToRun=$($readyToRun.ToString().ToLower())",
        "-p:EnableCompressionInSingleFile=$($compression.ToString().ToLower())",
        "-p:IncludeNativeLibrariesForSelfExtract=true",
        "-p:DebugType=embedded",
        "-o", $OutputDir,
        "-v", $Verbosity
    )

    & dotnet @publishArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Err "Publish failed with exit code $LASTEXITCODE"
        exit $LASTEXITCODE
    }

    # Inspect generated artifact
    $exePath = Join-Path $OutputDir "ToolpathViewer.App.exe"
    if (Test-Path $exePath) {
        $exeFile = Get-Item $exePath
        $sizeMB = [Math]::Round($exeFile.Length / 1MB, 2)
        Write-Banner "STANDALONE EXECUTABLE CREATED" "Green"
        Write-Success "Binary   : $exePath"
        Write-Success "Size     : $sizeMB MB"
    } else {
        Write-Warn "Publish finished, but ToolpathViewer.App.exe was not found directly in $OutputDir"
    }
}

# --- Execution Entry Point ---
try {
    Test-DotnetSdk

    switch ($Target) {
        'Clean' {
            Invoke-Clean
        }
        'Build' {
            Invoke-Build
        }
        'Test' {
            Invoke-Build
            Invoke-Test
        }
        'Publish' {
            if (-not $SkipTests) {
                Invoke-Build
                Invoke-Test
            }
            Invoke-Publish
        }
    }

    $Stopwatch.Stop()
    $elapsed = [Math]::Round($Stopwatch.Elapsed.TotalSeconds, 1)
    Write-Host ""
    Write-Success "Completed target '$Target' in ${elapsed}s."
    exit 0
}
catch {
    $Stopwatch.Stop()
    Write-Err "An unexpected error occurred: $_"
    exit 1
}
