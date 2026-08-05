# Builds the distributable Windows installer:
#   1. self-contained Release publish of the desktop app (no .NET needed on target machines)
#   2. WiX MSI from the publish output
#   3. creates a portable ZIP from the publish output
#   4. generates SHA-256 checksums for release artifacts
#   5. copies artifacts into the website's downloads folder when present
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

$publishDir = "$root\src\ClipYourself.Desktop\bin\Release\net10.0-windows\$Runtime\publish"

# Sign the app exe before it's packed into the MSI
Invoke-Sign "$publishDir\ClipYourself.Desktop.exe"

Write-Host "==> Building MSI..."
dotnet build "$root\installer\ClipYourself.Installer.wixproj" -c Release
if ($LASTEXITCODE -ne 0) { throw "MSI build failed" }

$msi = Get-ChildItem "$root\installer\bin\Release\*.msi" | Select-Object -First 1

# Sign the MSI itself
Invoke-Sign $msi.FullName

Write-Host "==> MSI: $($msi.FullName) ($([Math]::Round($msi.Length / 1MB, 1)) MB)"

$releaseDir = Split-Path $msi.FullName -Parent
$portableZip = Join-Path $releaseDir ("{0}-portable.zip" -f $msi.BaseName)

if (Test-Path $portableZip) {
    Remove-Item $portableZip -Force
}

Write-Host "==> Creating portable ZIP..."
Compress-Archive -Path "$publishDir\*" -DestinationPath $portableZip -CompressionLevel Optimal

$checksumsPath = Join-Path $releaseDir "checksums.txt"
$msiHash = Get-FileHash $msi.FullName -Algorithm SHA256
$zipHash = Get-FileHash $portableZip -Algorithm SHA256

@(
    "# SHA256 checksums for Clip Yourself release artifacts"
    "{0} *{1}" -f $msiHash.Hash.ToLowerInvariant(), (Split-Path $msi.FullName -Leaf)
    "{0} *{1}" -f $zipHash.Hash.ToLowerInvariant(), (Split-Path $portableZip -Leaf)
) | Set-Content -Path $checksumsPath -Encoding UTF8

Write-Host "==> Portable ZIP: $portableZip ($([Math]::Round((Get-Item $portableZip).Length / 1MB, 1)) MB)"
Write-Host "==> Checksums: $checksumsPath"

$downloads = "$root\website\public\downloads"
if (Test-Path $downloads) {
    Copy-Item $msi.FullName $downloads -Force
    Copy-Item $portableZip $downloads -Force
    Copy-Item $checksumsPath $downloads -Force
    Write-Host "==> Copied to $downloads"
}
