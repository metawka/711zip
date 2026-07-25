<#
    build-dist.ps1
    Produces a compact self-contained release of 711zip and compiles the
    Windows installer with Inno Setup.

    Steps:
      1. Clean publish, then `dotnet publish` (self-contained, x64, Release).
      2. Strip unused Windows App SDK AI/ML runtime (onnxruntime, DirectML).
      3. Compile installer/711zip.iss with ISCC into dist/.
#>
param(
    [string]$Version = "0.1.0"
)

$ErrorActionPreference = "Stop"
$root    = Split-Path -Parent $PSScriptRoot
$proj    = Join-Path $root "src\App\711zip.csproj"
$tfmDir  = "net10.0-windows10.0.19041.0"
$pubDir  = Join-Path $root "src\App\bin\x64\Release\$tfmDir\win-x64\publish"

Write-Host "==> Publishing (this pulls the self-contained runtime)..."
if (Test-Path $pubDir) { Remove-Item -Recurse -Force $pubDir }
& dotnet publish $proj -c Release -r win-x64 -p:DebugType=none -p:DebugSymbols=false | Out-Null

Write-Host "==> Stripping unused AI/ML runtime..."
foreach ($f in "onnxruntime.dll", "DirectML.dll") {
    $p = Join-Path $pubDir $f
    if (Test-Path $p) { Remove-Item $p -Force; Write-Host "    removed $f" }
}

$size = [math]::Round((Get-ChildItem $pubDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB, 1)
Write-Host "==> Payload size: $size MB"

# Locate ISCC.exe (Inno Setup 6).
$iscc = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
) | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $iscc) { throw "ISCC.exe (Inno Setup 6) not found." }

Write-Host "==> Compiling installer..."
$iss = Join-Path $root "installer\711zip.iss"
New-Item -ItemType Directory -Force -Path (Join-Path $root "dist") | Out-Null
& $iscc "/DAppVersion=$Version" "/DPublishDir=$pubDir" $iss

$setup = Join-Path $root "dist\711zip-$Version-setup.exe"
if (Test-Path $setup) {
    $mb = [math]::Round((Get-Item $setup).Length / 1MB, 1)
    Write-Host "==> Installer ready: $setup ($mb MB)"
} else {
    throw "Installer was not produced."
}
