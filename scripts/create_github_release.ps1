param(
    [string]$TagName = "v0.1.0",
    [string]$ReleaseName = "Nuvio Player v0.1.0",
    [string]$InstallerPath = "installer\output\NuvioPlayer-0.1.0-Setup.exe"
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Net.Http

$RootDir = Resolve-Path (Join-Path $PSScriptRoot "..")
$FullInstallerPath = Join-Path $RootDir $InstallerPath

if (-not (Test-Path $FullInstallerPath)) {
    Write-Error "Installer file not found at: $FullInstallerPath"
}

$installerItem = Get-Item $FullInstallerPath
$fileSizeMB = [math]::Round($installerItem.Length / 1MB, 2)
Write-Host "Found installer: $($installerItem.Name) ($fileSizeMB MB)" -ForegroundColor Cyan

# Fetch credentials from git credential helper
$credOutput = @"
protocol=https
host=github.com
"@ | git credential fill

$token = $null
foreach ($line in ($credOutput -split "`r?`n")) {
    if ($line -match "^password=(.+)$") {
        $token = $matches[1]
        break
    }
}

if (-not $token) {
    Write-Error "Could not retrieve GitHub token from git credential manager."
}

$repoOwner = "RisinuSenarath"
$repoName = "nuvio-player"

$headers = @{
    "Authorization" = "token $token"
    "User-Agent" = "Nuvio-Release-Publisher"
    "Accept" = "application/vnd.github.v3+json"
}

# Release body markdown
$releaseBody = @"
# Nuvio Player v0.1.0

A modern, high-performance desktop video player for Windows built with WPF, .NET 8, and LibVLC (Hardware-accelerated Direct3D11).

### Key Features
- **Fluid & Responsive UI:** Modern dark glassmorphism design system with rounded corners, frameless window resizing, and custom controls.
- **Hardware-Accelerated Playback:** Zero-copy Direct3D11 rendering powered by LibVLC, supporting 4K HDR, AV1, HEVC, H.264, VP9, and all major video containers (.mp4, .mkv, .avi, .mov, .webm, .ts, etc.).
- **Audio Boost:** High-fidelity audio playback with volume boosting up to 150%.
- **Keyboard Shortcuts:**
  - `Space` / `MediaPlayPause`: Play / Pause
  - `Enter` / `F`: Toggle Fullscreen / Windowed mode
  - `Left` / `Right`: Seek backward / forward 5s (with instant scrubber line sync)
  - `Up` / `Down`: Volume up / down
  - `M`: Mute / Unmute
  - `Escape`: Exit fullscreen
- **Seamless Shell Integration:** Windows file association support, custom app branding icon, and single-instance command-line playback.

### Installation
Download and run **`NuvioPlayer-0.1.0-Setup.exe`** below to install Nuvio Player on Windows 10 or 11 (x64).
"@

# 1. Create or Get Release
Write-Host "Checking for existing release for tag $TagName..." -ForegroundColor Yellow
$release = $null
try {
    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repoOwner/$repoName/releases/tags/$TagName" -Headers $headers -Method Get
    Write-Host "Found existing release (ID: $($release.id))." -ForegroundColor Gray
} catch {
    Write-Host "Release does not exist yet. Creating new release..." -ForegroundColor Yellow
    $createBody = @{
        tag_name = $TagName
        target_commitish = "main"
        name = $ReleaseName
        body = $releaseBody
        draft = $false
        prerelease = $false
    } | ConvertTo-Json

    $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repoOwner/$repoName/releases" -Headers $headers -Method Post -Body $createBody
    Write-Host "Created release '$ReleaseName' (ID: $($release.id))." -ForegroundColor Green
}

# 2. Check if asset already exists on release
$assetName = $installerItem.Name
if ($release.assets) {
    foreach ($existingAsset in $release.assets) {
        if ($existingAsset.name -eq $assetName) {
            Write-Host "Asset '$assetName' already exists on release. Deleting existing asset (ID: $($existingAsset.id))..." -ForegroundColor Yellow
            $deleteUrl = "https://api.github.com/repos/$repoOwner/$repoName/releases/assets/$($existingAsset.id)"
            Invoke-RestMethod -Uri $deleteUrl -Headers $headers -Method Delete
            Write-Host "Deleted existing asset." -ForegroundColor Gray
            break
        }
    }
}

# 3. Upload Asset
Write-Host "Uploading $assetName ($fileSizeMB MB) to GitHub Release..." -ForegroundColor Yellow

$uploadUri = "https://uploads.github.com/repos/$repoOwner/$repoName/releases/$($release.id)/assets?name=$assetName"

$httpClient = [System.Net.Http.HttpClient]::new()
$httpClient.Timeout = [TimeSpan]::FromMinutes(10)
$httpClient.DefaultRequestHeaders.Add("Authorization", "token $token")
$httpClient.DefaultRequestHeaders.Add("User-Agent", "Nuvio-Release-Publisher")
$httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json")

$fileStream = [System.IO.File]::OpenRead($FullInstallerPath)
$streamContent = [System.Net.Http.StreamContent]::new($fileStream)
$streamContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse("application/octet-stream")

try {
    $response = $httpClient.PostAsync($uploadUri, $streamContent).GetAwaiter().GetResult()
    if ($response.IsSuccessStatusCode) {
        $responseJson = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult() | ConvertFrom-Json
        Write-Host "`nSUCCESS: Asset uploaded successfully!" -ForegroundColor Green
        Write-Host "Download URL: $($responseJson.browser_download_url)" -ForegroundColor Cyan
        Write-Host "Release URL: $($release.html_url)" -ForegroundColor Cyan
    } else {
        $errText = $response.Content.ReadAsStringAsync().GetAwaiter().GetResult()
        Write-Error "Upload failed: $($response.StatusCode) - $errText"
    }
} finally {
    $fileStream.Dispose()
    $httpClient.Dispose()
}
