$ErrorActionPreference = "Stop"

$repo = "VO-Bogey/pawdesk-desktop-pet"
$installDir = Join-Path $env:LOCALAPPDATA "PawDeskTest"
$exePath = Join-Path $installDir "PawDesk.App.exe"
$apiUrl = "https://api.github.com/repos/$repo/releases/latest"

Write-Host "Checking latest PawDesk release..."
$release = Invoke-RestMethod -Uri $apiUrl -Headers @{
    "User-Agent" = "PawDesk-Test-Installer"
}

$asset = $release.assets | Where-Object { $_.name -eq "PawDesk.App.exe" } | Select-Object -First 1
if (-not $asset) {
    throw "PawDesk.App.exe was not found in the latest release."
}

New-Item -ItemType Directory -Force -Path $installDir | Out-Null

$running = Get-Process -Name "PawDesk.App" -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "Stopping running PawDesk process..."
    $running | Stop-Process -Force
    Start-Sleep -Seconds 1
}

Write-Host "Downloading $($release.tag_name) to $exePath ..."
Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $exePath

Unblock-File -Path $exePath -ErrorAction SilentlyContinue

Write-Host "Starting PawDesk..."
Start-Process -FilePath $exePath -WorkingDirectory $installDir
Write-Host "Done."
