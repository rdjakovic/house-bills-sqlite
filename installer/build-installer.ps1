<#
.SYNOPSIS
    Builds the HouseBills installer (artifacts\installer\HouseBills-Setup-<version>.exe).

.DESCRIPTION
    1. Publishes the WPF app self-contained for win-x64 (target PCs need no .NET install; SQLite's native library
       is part of the publish output).
    2. Compiles installer\HouseBills.iss with Inno Setup 6 (winget install JRSoftware.InnoSetup).

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1 -Version 1.0.0
#>
param(
    [string]$Version = '1.0.0'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$artifacts = Join-Path $root 'artifacts'
$publishDir = Join-Path $artifacts 'publish'

function Find-InnoSetupCompiler {
    $candidates = @(
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    $found = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    if (-not $found) {
        $command = Get-Command ISCC.exe -ErrorAction SilentlyContinue
        if ($command) { $found = $command.Source }
    }
    if (-not $found) {
        throw 'Inno Setup 6 was not found. Install it with: winget install JRSoftware.InnoSetup'
    }
    return $found
}

Write-Host "Publishing HouseBills $Version (self-contained win-x64)..."
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish (Join-Path $root 'src\HouseBills.Wpf') -c Release -r win-x64 --self-contained -p:Version=$Version -o $publishDir
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed with exit code $LASTEXITCODE." }

Write-Host 'Compiling installer...'
$iscc = Find-InnoSetupCompiler
& $iscc "/DAppVersion=$Version" "/DPublishDir=$publishDir" "/O$(Join-Path $artifacts 'installer')" (Join-Path $PSScriptRoot 'HouseBills.iss')
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed with exit code $LASTEXITCODE." }

Write-Host "Installer: $(Join-Path $artifacts "installer\HouseBills-Setup-$Version.exe")"
