<#
.SYNOPSIS
    Builds, tests, publishes Nuvio Player, and generates the Inno Setup installer.
.DESCRIPTION
    Executes automated tests, runs release compilation and publish, verifies the native
    LibVLC bundle, and compiles the setup executable if Inno Setup is installed.
#>

param(
    [switch]$SkipTests,
    [switch]$SelfContained = $false
)

$ErrorActionPreference = "Stop"

$RootDir = Resolve-Path (Join-Path $PSScriptRoot "..")
$PublishDir = Join-Path $RootDir "publish\win-x64"
$ProjectFile = Join-Path $RootDir "src\NuvioPlayer\NuvioPlayer.csproj"
$SolutionFile = Join-Path $RootDir "NuvioPlayer.slnx"
$InstallerScript = Join-Path $RootDir "installer\NuvioPlayer.iss"

Write-Host "============================================================" -ForegroundColor Cyan
Write-Host "  Nuvio Player - Build, Test and Packaging Pipeline" -ForegroundColor Cyan
Write-Host "============================================================" -ForegroundColor Cyan

# 1. Run Tests
if (-not $SkipTests) {
    Write-Host "`n[1/4] Running automated tests in Release mode..." -ForegroundColor Yellow
    dotnet test $SolutionFile -c Release --verbosity normal
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Tests failed! Aborting release build."
    }
    Write-Host "All automated tests passed successfully." -ForegroundColor Green
} else {
    Write-Host "`n[1/4] Skipping tests (-SkipTests specified)." -ForegroundColor DarkGray
}

# 2. Clean & Publish
Write-Host "`n[2/4] Publishing Nuvio Player (win-x64, Release)..." -ForegroundColor Yellow
if (Test-Path $PublishDir) {
    Remove-Item -Recurse -Force $PublishDir
}

if ($SelfContained) {
    dotnet publish $ProjectFile -c Release -r win-x64 -o $PublishDir --self-contained true
} else {
    dotnet publish $ProjectFile -c Release -r win-x64 -o $PublishDir --no-self-contained
}

if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed!"
}

# Remove unused 32-bit LibVLC folder to reduce installer size and compilation time
$unusedX86 = Join-Path $PublishDir "libvlc\win-x86"
if (Test-Path $unusedX86) {
    Remove-Item -Recurse -Force $unusedX86
}

# 3. Verify Native Dependencies
Write-Host "`n[3/4] Verifying published distribution..." -ForegroundColor Yellow
$exePath = Join-Path $PublishDir "NuvioPlayer.exe"
$libVlcDll = Join-Path $PublishDir "libvlc\win-x64\libvlc.dll"
$pluginsDir = Join-Path $PublishDir "libvlc\win-x64\plugins"

if (-not (Test-Path $exePath)) {
    Write-Error "Missing executable: $exePath"
}
if (-not (Test-Path $libVlcDll)) {
    Write-Error "Missing LibVLC core library: $libVlcDll"
}
if (-not (Test-Path $pluginsDir)) {
    Write-Error "Missing LibVLC plugins directory: $pluginsDir"
}

Write-Host "Executable verified: $exePath" -ForegroundColor Green
Write-Host "Bundled LibVLC verified: $libVlcDll" -ForegroundColor Green
Write-Host "LibVLC Plugins verified: $pluginsDir" -ForegroundColor Green

# 4. Inno Setup Compilation
Write-Host "`n[4/4] Building Installer..." -ForegroundColor Yellow

$innoCandidates = @(
    "iscc.exe",
    "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
)

$isccPath = $null
foreach ($candidate in $innoCandidates) {
    $found = Get-Command $candidate -ErrorAction SilentlyContinue
    if ($found) {
        $isccPath = $found.Source
        break
    }
    if (Test-Path $candidate) {
        $isccPath = $candidate
        break
    }
}

if ($isccPath) {
    Write-Host "Found Inno Setup compiler: $isccPath" -ForegroundColor Gray
    & $isccPath $InstallerScript
    if ($LASTEXITCODE -eq 0) {
        $outputSetup = Join-Path $RootDir "installer\output\NuvioPlayer-0.1.0-Setup.exe"
        Write-Host "`nSUCCESS: Installer created at $outputSetup" -ForegroundColor Green
    } else {
        Write-Warning "Inno Setup compilation exited with code $LASTEXITCODE."
    }
} else {
    Write-Host "Inno Setup compiler (ISCC.exe) not found on PATH or standard directories." -ForegroundColor Yellow
    Write-Host "The application is fully published and ready at: $PublishDir" -ForegroundColor Yellow
    Write-Host "To compile the installer, install Inno Setup 6 and run: iscc.exe $InstallerScript" -ForegroundColor Gray
}

Write-Host "`nRelease packaging complete!" -ForegroundColor Cyan
