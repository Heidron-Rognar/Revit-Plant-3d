<#
.SYNOPSIS
    Builds the Plant Draw solution.

.EXAMPLE
    .\build.ps1

.EXAMPLE
    .\build.ps1 -Configuration Debug -RevitVersion 2025
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$RevitVersion = '2026',

    [string]$RevitApiDir = ''
)

$ErrorActionPreference = 'Stop'

$repoRoot = $PSScriptRoot
$solution = Join-Path $repoRoot 'PlantDraw.sln'

if (-not (Test-Path -LiteralPath $solution)) {
    throw "Cannot find $solution"
}

$buildArgs = @(
    'build', $solution,
    '-c', $Configuration,
    '-p:Platform=x64',
    "-p:RevitVersion=$RevitVersion",
    '--nologo'
)

if ($RevitApiDir) {
    $buildArgs += "-p:RevitApiDir=$RevitApiDir"
}

Write-Host "==> dotnet $($buildArgs -join ' ')" -ForegroundColor Cyan
& dotnet @buildArgs

if ($LASTEXITCODE -ne 0) {
    throw "Build failed with exit code $LASTEXITCODE."
}

Write-Host ''
Write-Host "Build succeeded: src\PlantDraw\bin\$Configuration\PlantDraw.dll" -ForegroundColor Green
Write-Host 'Install it with: .\install\Install-PlantDraw.ps1 -SkipBuild'
