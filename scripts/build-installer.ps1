# Builds the distributable Windows installer:
#   1. self-contained Release publish of the desktop app (no .NET needed on target machines)
#   2. WiX MSI from the publish output
#   3. creates a portable ZIP from the publish output
#   4. generates SHA-256 checksums for release artifacts
#   5. copies artifacts into the website's downloads folder when present
#   6. builds the marketing site and packages it as clip-yourself.zip for deployment
#      (skipped when npm isn't on PATH)
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

# Newest by write time: the one WiX just built, even if older-version MSIs linger.
$msi = Get-ChildItem "$root\installer\bin\Release\*.msi" | Sort-Object LastWriteTime -Descending | Select-Object -First 1

# Sign the MSI itself
Invoke-Sign $msi.FullName

Write-Host "==> MSI: $($msi.FullName) ($([Math]::Round($msi.Length / 1MB, 1)) MB)"

$releaseDir = Split-Path $msi.FullName -Parent
$portableZip = Join-Path $releaseDir ("{0}-portable.zip" -f $msi.BaseName)

# Drop artifacts from earlier versions so the release dir only holds the current build.
Get-ChildItem $releaseDir -File |
    Where-Object { $_.Extension -in '.msi', '.zip', '.wixpdb' -and $_.BaseName -notlike "$($msi.BaseName)*" } |
    Remove-Item -Force

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
    # Keep only the current release in the site's downloads folder so the deploy
    # bundle never ships stale installers from earlier versions.
    Get-ChildItem $downloads -File |
        Where-Object { $_.Extension -in '.msi', '.zip' } |
        Remove-Item -Force
    Copy-Item $msi.FullName $downloads -Force
    Copy-Item $portableZip $downloads -Force
    Copy-Item $checksumsPath $downloads -Force
    Write-Host "==> Copied to $downloads"

    # Build the marketing site and package it for deployment. Named clip-yourself.zip
    # so Explorer's "Extract All" drops it into a clip-yourself\ folder.
    if (Get-Command npm -ErrorAction SilentlyContinue) {
        Push-Location "$root\website"
        # npm/tsc/vite write progress to stderr; on PowerShell 7.4+ that trips
        # native-command error handling under $ErrorActionPreference='Stop'. Judge
        # success by the exit code instead.
        $PSNativeCommandUseErrorActionPreference = $false
        try {
            Write-Host "==> Building website..."
            # Run through cmd: the npm.ps1 shim mangles args when called via '& npm'.
            cmd /c "npm run build"
            if ($LASTEXITCODE -ne 0) { throw "website build failed" }

            $siteZip = "$root\clip-yourself.zip"
            if (Test-Path $siteZip) { Remove-Item $siteZip -Force }
            Compress-Archive -Path "$root\website\dist\*" -DestinationPath $siteZip -CompressionLevel Optimal
            Write-Host "==> Site package: $siteZip ($([Math]::Round((Get-Item $siteZip).Length / 1MB, 1)) MB)"
        }
        finally {
            Pop-Location
        }
    }
    else {
        Write-Host "==> npm not found on PATH; skipping site package (clip-yourself.zip)"
    }
}
