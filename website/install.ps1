# DropMorph one-line installer for Windows 10 and 11. In PowerShell:
#
#   irm https://dropmorph.vercel.app/install.ps1 | iex
#
# Finds the latest release on GitHub, downloads DropMorph-<version>-win-Setup.exe, checks its SHA-256 against the
# release's checksum file and starts Setup (which asks for administrator permission once).
# Set $env:DROPMORPH_VERSION = '1.12.0' first to install a specific version.

& {
    $ErrorActionPreference = 'Stop'
    $ProgressPreference = 'SilentlyContinue'   # Invoke-WebRequest is many times faster without its progress bar.
    [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
    $repo = 'https://github.com/Cod4Nitish/DropMorph'

    if (-not [Environment]::Is64BitOperatingSystem) { throw 'DropMorph needs 64-bit Windows 10 (1809 or newer) or Windows 11.' }

    $version = $env:DROPMORPH_VERSION
    if ($version) {
        $version = $version.TrimStart('v')
    }
    else {
        # github.com/<repo>/releases/latest redirects to .../releases/tag/v<version> (no API rate limit).
        $request = [Net.WebRequest]::Create("$repo/releases/latest")
        $request.Method = 'HEAD'
        $response = $request.GetResponse()
        try { $version = ($response.ResponseUri.AbsoluteUri -split '/v')[-1] } finally { $response.Close() }
        if ($version -notmatch '^\d+\.\d+\.\d+') { throw "Could not find the latest DropMorph version." }
    }

    $setup = "DropMorph-$version-win-Setup.exe"
    $base = "$repo/releases/download/v$version"
    $folder = Join-Path ([IO.Path]::GetTempPath()) "DropMorph-$version"
    New-Item -ItemType Directory -Force -Path $folder | Out-Null
    $target = Join-Path $folder $setup

    Write-Host ''
    Write-Host 'DropMorph - free file converter' -ForegroundColor Cyan
    Write-Host 'Developed by Nitish Singh  (https://github.com/cod4nitish)' -ForegroundColor Yellow
    Write-Host ''
    Write-Host "Downloading DropMorph $version for Windows..."
    Invoke-WebRequest -UseBasicParsing -Uri "$base/$setup" -OutFile $target
    $sums = (Invoke-WebRequest -UseBasicParsing -Uri "$base/DropMorph-$version-SHA256SUMS.txt").Content
    if ($sums -is [byte[]]) { $sums = [Text.Encoding]::UTF8.GetString($sums) }
    $line = $sums -split "`r?`n" | Where-Object { $_ -match "\s\*?$([regex]::Escape($setup))$" } | Select-Object -First 1
    $expected = if ($line) { ($line -split '\s+')[0] } else { '' }
    $actual = (Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash
    if (-not $expected -or $expected -ne $actual) {
        Remove-Item -LiteralPath $target -Force
        throw 'Checksum mismatch: the download is incomplete or was changed. Nothing was installed.'
    }
    Write-Host 'Checksum OK. Starting Setup (click Yes when Windows asks for permission)...'
    # Downloaded by PowerShell, so the file has no "downloaded from the internet" mark and SmartScreen doesn't block it.
    Start-Process -FilePath $target -Wait
    Write-Host 'Done. Hold Ctrl, drag a file and drop it on a format in the donut.'
    Write-Host 'Thanks for installing DropMorph, developed by Nitish Singh. Like it? Star it: https://github.com/Cod4Nitish/DropMorph' -ForegroundColor Yellow
}
