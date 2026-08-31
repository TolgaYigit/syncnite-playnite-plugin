# Builds the plugin and packages it as a .pext using Playnite's own Toolbox.exe (ships
# alongside any Playnite install - Playnite.DesktopApp.exe's folder).
param(
    [string]$Configuration = "Release",
    [string]$ToolboxPath
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not $ToolboxPath) {
    $candidates = @(
        "$env:ProgramFiles\Playnite\Toolbox.exe",
        "${env:ProgramFiles(x86)}\Playnite\Toolbox.exe",
        "$env:LOCALAPPDATA\Playnite\Toolbox.exe"
    )
    $ToolboxPath = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if (-not $ToolboxPath -or -not (Test-Path $ToolboxPath)) {
    throw "Couldn't find Playnite's Toolbox.exe. Pass -ToolboxPath explicitly - it ships next to Playnite.DesktopApp.exe in any Playnite install (including portable copies)."
}

dotnet build "$root\PlayniteCloudSync.csproj" -c $Configuration
if ($LASTEXITCODE -ne 0) { throw "Build failed." }

$dist = Join-Path $root "dist"
New-Item -ItemType Directory -Force -Path $dist | Out-Null

& $ToolboxPath pack "$root\bin\$Configuration" $dist
if ($LASTEXITCODE -ne 0) { throw "Packing failed." }

Write-Host "Packaged to $dist"
