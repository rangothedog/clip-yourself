# Builds the distributable Windows installer:
#   1. self-contained Release publish of the desktop app (no .NET needed on target machines)
#   2. WiX MSI from the publish output
#   3. copies the MSI into the website's downloads folder when present
#
# Code signing (removes the "Unknown publisher" UAC warning) is optional: pass the
# thumbprint of a code-signing certificate for "Rango Studio LLC" installed in the
# CurrentUser\My (or LocalMachine\My) store. Without it the build still succeeds,
# just unsigned.
param(
    [string]$Runtime = "win-x64",
    [string]$CertThumbprint,
    [string]$TimestampUrl = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

function Invoke-Sign([string]$file) {
    if (-not $CertThumbprint) { return }
    $signtool = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $signtool) { throw "signtool.exe not found (install the Windows SDK)" }
    Write-Host "==> Signing $file"
    & $signtool.FullName sign /sha1 $CertThumbprint /fd SHA256 /tr $TimestampUrl /td SHA256 $file
    if ($LASTEXITCODE -ne 0) { throw "signing failed for $file" }
}

Write-Host "==> Publishing desktop app ($Runtime, self-contained)..."
dotnet publish "$root\src\ClipYourself.Desktop" -c Release -r $Runtime --self-contained true
if ($LASTEXITCODE -ne 0) { throw "publish failed" }

# Sign the app exe before it's packed into the MSI
Invoke-Sign "$root\src\ClipYourself.Desktop\bin\Release\net10.0-windows\$Runtime\publish\ClipYourself.Desktop.exe"

Write-Host "==> Building MSI..."
dotnet build "$root\installer\ClipYourself.Installer.wixproj" -c Release
if ($LASTEXITCODE -ne 0) { throw "MSI build failed" }

$msi = Get-ChildItem "$root\installer\bin\Release\*.msi" | Select-Object -First 1

# Sign the MSI itself
Invoke-Sign $msi.FullName

Write-Host "==> MSI: $($msi.FullName) ($([Math]::Round($msi.Length / 1MB, 1)) MB)"

$downloads = "$root\website\public\downloads"
if (Test-Path $downloads) {
    Copy-Item $msi.FullName $downloads -Force
    Write-Host "==> Copied to $downloads"
}
