<#
.SYNOPSIS
    Removes the Plant Draw add-in from Revit.

.DESCRIPTION
    Deletes the manifest from every Revit add-ins folder it may have been written to
    (2024 - 2027) and removes the installed binaries from %LOCALAPPDATA%\PlantDraw.

.EXAMPLE
    .\Uninstall-PlantDraw.ps1
#>
[CmdletBinding()]
param(
    [switch]$KeepFiles
)

$ErrorActionPreference = 'Stop'

$versions = @('2024', '2025', '2026', '2027')

foreach ($version in $versions) {
    $manifest = Join-Path $env:APPDATA "Autodesk\Revit\Addins\$version\PlantDraw.addin"
    if (Test-Path -LiteralPath $manifest) {
        Remove-Item -LiteralPath $manifest -Force
        Write-Host "Removed $manifest" -ForegroundColor Cyan
    }
}

if (-not $KeepFiles) {
    $installRoot = Join-Path $env:LOCALAPPDATA 'PlantDraw'

    if (Test-Path -LiteralPath $installRoot) {
        try {
            Remove-Item -LiteralPath $installRoot -Recurse -Force
            Write-Host "Removed $installRoot" -ForegroundColor Cyan
        }
        catch {
            Write-Warning "Could not delete $installRoot (close Revit first): $($_.Exception.Message)"
        }
    }
}

Write-Host ''
Write-Host 'Plant Draw uninstalled. Restart Revit.' -ForegroundColor Green
