<#
    fetch-engine.ps1
    Downloads the official 7-Zip release and extracts the full 7z.dll engine
    into vendor/engine-x64/. Run from anywhere.
#>
param(
    [string]$Version = "26.02"
)

$ErrorActionPreference = "Stop"
$tag  = $Version
$flat = $Version -replace '\.', ''          # 26.02 -> 2602
$root = Split-Path -Parent $PSScriptRoot    # repo root
$vendor = Join-Path $root "vendor"
$engine = Join-Path $vendor "engine-x64"
New-Item -ItemType Directory -Force -Path $vendor, $engine | Out-Null

$exe = Join-Path $vendor "7z$flat-x64.exe"
$url = "https://github.com/ip7z/7zip/releases/download/$tag/7z$flat-x64.exe"

Write-Host "Downloading $url"
Invoke-WebRequest $url -OutFile $exe

# Use an available 7z to unpack the installer (it is a 7z SFX archive).
$sevenzip = (Get-Command 7z -ErrorAction SilentlyContinue).Source
if (-not $sevenzip) { throw "Need a '7z' on PATH to extract the installer." }

& $sevenzip e $exe "-o$engine" "7z.dll" "7z.exe" "License.txt" -y | Out-Null
Write-Host "Engine extracted to $engine"
Get-ChildItem $engine | Select-Object Name, Length
