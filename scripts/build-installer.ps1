# Builds the distributable Windows installer:
#   1. self-contained Release publish of the desktop app (no .NET needed on target machines)
#   2. WiX MSI from the publish output
#   3. copies the MSI into the website's downloads folder when present
param(
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

Write-Host "==> Publishing desktop app ($Runtime, self-contained)..."
dotnet publish "$root\src\ClipYourself.Desktop" -c Release -r $Runtime --self-contained true
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

Write-Host "==> Building MSI..."
dotnet build "$root\installer\ClipYourself.Installer.wixproj" -c Release
if ($LASTEXITCODE -ne 0) { throw "MSI build failed" }

$msi = Get-ChildItem "$root\installer\bin\Release\*.msi" | Select-Object -First 1
Write-Host "==> MSI: $($msi.FullName) ($([Math]::Round($msi.Length / 1MB, 1)) MB)"

$downloads = "$root\website\public\downloads"
if (Test-Path $downloads) {
    Copy-Item $msi.FullName $downloads -Force
    Write-Host "==> Copied to $downloads"
}
