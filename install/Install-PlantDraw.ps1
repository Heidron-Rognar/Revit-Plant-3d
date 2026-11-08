<#
.SYNOPSIS
    Builds and installs the Plant Draw add-in for Autodesk Revit 2025 / 2026.

.DESCRIPTION
    Per-user install. Nothing is written outside %LOCALAPPDATA% and
    %APPDATA%\Autodesk\Revit\Addins, so no administrator rights are needed.

    The DLL is installed to: %LOCALAPPDATA%\PlantDraw\bin\PlantDraw.dll
    The manifest is installed to: %APPDATA%\Autodesk\Revit\Addins\<version>\PlantDraw.addin

.EXAMPLE
    .\Install-PlantDraw.ps1

.EXAMPLE
    .\Install-PlantDraw.ps1 -RevitVersion 2025 -Configuration Debug

.EXAMPLE
    .\Install-PlantDraw.ps1 -RevitApiDir "D:\Apps\Autodesk\Revit 2026\"
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$RevitVersion = '2026',

    [string]$RevitApiDir = '',

    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'

function Write-Step {
    param([string]$Message)
    Write-Host "==> $Message" -ForegroundColor Cyan
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot 'src\PlantDraw\PlantDraw.csproj'
$manifestTemplate = Join-Path $PSScriptRoot 'PlantDraw.addin'

if (-not (Test-Path -LiteralPath $projectPath)) {
    throw "Cannot find the project at $projectPath. Run this script from the repository's install folder."
}

# -- 1. build -----------------------------------------------------------------
if (-not $SkipBuild) {
    Write-Step "Building Plant Draw ($Configuration)"

    $dotnet = Get-Command dotnet -ErrorAction SilentlyContinue
    if (-not $dotnet) {
        throw 'The .NET 8 SDK was not found. Install it from https://dotnet.microsoft.com/download/dotnet/8.0 and run this script again.'
    }

    $buildArgs = @(
        'build', $projectPath,
        '-c', $Configuration,
        '-p:Platform=x64',
        "-p:RevitVersion=$RevitVersion",
        '--nologo'
    )

    if ($RevitApiDir) {
        $buildArgs += "-p:RevitApiDir=$RevitApiDir"
    }

    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) {
        throw "The build failed (exit code $LASTEXITCODE). If RevitAPI.dll was not found, pass -RevitApiDir with your Revit program folder."
    }
}

# -- 2. locate the built assembly ---------------------------------------------
$candidates = @(
    (Join-Path $repoRoot "src\PlantDraw\bin\$Configuration\PlantDraw.dll"),
    (Join-Path $repoRoot "src\PlantDraw\bin\$Configuration\net8.0-windows\PlantDraw.dll"),
    (Join-Path $repoRoot "src\PlantDraw\bin\x64\$Configuration\net8.0-windows\PlantDraw.dll")
)

$dllPath = $null
foreach ($candidate in $candidates) {
    if (Test-Path -LiteralPath $candidate) {
        $dllPath = $candidate
        break
    }
}

if (-not $dllPath) {
    throw "PlantDraw.dll was not found after the build. Checked: $($candidates -join '; ')"
}

# -- 3. copy the payload ------------------------------------------------------
$installRoot = Join-Path $env:LOCALAPPDATA 'PlantDraw'
$binDir = Join-Path $installRoot 'bin'

Write-Step "Installing to $binDir"
New-Item -ItemType Directory -Force -Path $binDir | Out-Null

$targetDll = Join-Path $binDir 'PlantDraw.dll'

# Revit locks loaded assemblies; if it is running, tell the user instead of failing obscurely.
try {
    Copy-Item -LiteralPath $dllPath -Destination $targetDll -Force
}
catch {
    throw "Could not overwrite $targetDll. Close Revit and run the installer again. ($($_.Exception.Message))"
}

$pdbSource = [System.IO.Path]::ChangeExtension($dllPath, '.pdb')
if (Test-Path -LiteralPath $pdbSource) {
    Copy-Item -LiteralPath $pdbSource -Destination (Join-Path $binDir 'PlantDraw.pdb') -Force
}

# -- 4. write the manifest ----------------------------------------------------
$addInDir = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$RevitVersion"
New-Item -ItemType Directory -Force -Path $addInDir | Out-Null

$addInPath = Join-Path $addInDir 'PlantDraw.addin'
$assemblyXml = [System.Security.SecurityElement]::Escape($targetDll)
$manifest = Get-Content -LiteralPath $manifestTemplate -Raw
$manifest = $manifest.Replace('__ASSEMBLY__', $assemblyXml)

Set-Content -LiteralPath $addInPath -Value $manifest -Encoding UTF8

Write-Step "Manifest written to $addInPath"
Write-Host ''
Write-Host 'Plant Draw installed.' -ForegroundColor Green
Write-Host 'Start Revit, open a project, then use the "Plant Draw" ribbon tab -> "3D Direction Pad".'
Write-Host ''
Write-Host "Uninstall with: .\Uninstall-PlantDraw.ps1 -RevitVersion $RevitVersion"
