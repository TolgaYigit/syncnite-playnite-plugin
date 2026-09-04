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

# Toolbox names the .pext after extension.yaml's Id (a GUID) - rename to something a human
# would actually want to see in a Releases list or on the Playnite Addon Database.
$versionLine = Get-Content "$root\extension.yaml" | Where-Object { $_ -match "^Version:\s*(.+)$" }
$version = ($versionLine -replace "^Version:\s*", "").Trim() -replace "\.", "_"
$packed = Get-ChildItem $dist -Filter "*.pext" | Sort-Object LastWriteTime -Descending | Select-Object -First 1
$renamed = Join-Path $dist "Syncnite_$version.pext"
Move-Item $packed.FullName $renamed -Force

Write-Host "Packaged to $renamed"
