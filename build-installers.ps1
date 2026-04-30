<#
.SYNOPSIS
    Builds FlipKit installers for Windows, Mac, and Linux.

.DESCRIPTION
    This script publishes self-contained builds of FlipKit and creates
    platform-specific installers:
    - Windows: Inno Setup .exe installer
    - Mac: DMG with .app bundle
    - Linux: .deb, .rpm, and .tar.gz packages

.PARAMETER Version
    The version number for the release (e.g., "3.3.0")

.PARAMETER Platform
    Which platform to build for: Windows, Mac, Linux, or All

.EXAMPLE
    .\build-installers.ps1 -Version 3.3.0
    Builds all platform installers for version 3.3.0

.EXAMPLE
    .\build-installers.ps1 -Version 3.3.0 -Platform Windows
    Builds only the Windows installer
#>

param(
    [Parameter(Mandatory = $true)]
    [string]$Version,

    [ValidateSet("Windows", "Mac", "Linux", "All")]
    [string]$Platform = "All"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = $PSScriptRoot
$PublishDir = Join-Path $ProjectRoot "publish"
$InstallerDir = Join-Path $ProjectRoot "installers"

Write-Host "FlipKit Installer Build Script v$Version" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Create output directories
New-Item -ItemType Directory -Force -Path $PublishDir | Out-Null
New-Item -ItemType Directory -Force -Path $InstallerDir | Out-Null

# Build Windows
if ($Platform -eq "Windows" -or $Platform -eq "All") {
    Write-Host "Building Windows (x64)..." -ForegroundColor Yellow

    $winPublishDir = Join-Path $PublishDir "win-x64"

    dotnet publish "$ProjectRoot\FlipKit.Desktop" `
        -c Release `
        -r win-x64 `
        --self-contained `
        -p:PublishSingleFile=false `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $winPublishDir

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Windows build failed"
        exit 1
    }

    Write-Host "Windows build completed: $winPublishDir" -ForegroundColor Green

    # Check for Inno Setup
    $iscc = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
    if (-not (Test-Path $iscc)) {
        $iscc = "iscc"  # Try PATH
    }

    if (Get-Command $iscc -ErrorAction SilentlyContinue) {
        Write-Host "Creating Windows installer with Inno Setup..." -ForegroundColor Yellow

        $issFile = Join-Path $ProjectRoot "Installer\Windows\FlipKit.iss"
        & $iscc "/DVersion=$Version" $issFile

        if ($LASTEXITCODE -eq 0) {
            Write-Host "Windows installer created successfully!" -ForegroundColor Green
        } else {
            Write-Warning "Inno Setup compilation failed. ZIP will be created instead."
        }
    } else {
        Write-Warning "Inno Setup not found. Creating ZIP instead..."
    }

    # Always create a ZIP as backup
    $zipFile = Join-Path $InstallerDir "FlipKit-Windows-x64-v$Version.zip"
    if (Test-Path $zipFile) { Remove-Item $zipFile }
    Compress-Archive -Path "$winPublishDir\*" -DestinationPath $zipFile
    Write-Host "Created: $zipFile" -ForegroundColor Green

    Write-Host ""
}

# Build Mac (x64 and ARM64)
if ($Platform -eq "Mac" -or $Platform -eq "All") {
    Write-Host "Building macOS (x64)..." -ForegroundColor Yellow

    $macX64Dir = Join-Path $PublishDir "osx-x64"

    dotnet publish "$ProjectRoot\FlipKit.Desktop" `
        -c Release `
        -r osx-x64 `
        --self-contained `
        -o $macX64Dir

    if ($LASTEXITCODE -ne 0) {
        Write-Error "macOS x64 build failed"
        exit 1
    }

    Write-Host "macOS x64 build completed: $macX64Dir" -ForegroundColor Green

    Write-Host "Building macOS (ARM64)..." -ForegroundColor Yellow

    $macArm64Dir = Join-Path $PublishDir "osx-arm64"

    dotnet publish "$ProjectRoot\FlipKit.Desktop" `
        -c Release `
        -r osx-arm64 `
        --self-contained `
        -o $macArm64Dir

    if ($LASTEXITCODE -ne 0) {
        Write-Error "macOS ARM64 build failed"
        exit 1
    }

    Write-Host "macOS ARM64 build completed: $macArm64Dir" -ForegroundColor Green

    # Create ZIP packages (DMG requires running on Mac)
    $zipFileX64 = Join-Path $InstallerDir "FlipKit-macOS-x64-v$Version.zip"
    $zipFileArm = Join-Path $InstallerDir "FlipKit-macOS-arm64-v$Version.zip"

    if (Test-Path $zipFileX64) { Remove-Item $zipFileX64 }
    if (Test-Path $zipFileArm) { Remove-Item $zipFileArm }

    Compress-Archive -Path "$macX64Dir\*" -DestinationPath $zipFileX64
    Compress-Archive -Path "$macArm64Dir\*" -DestinationPath $zipFileArm

    Write-Host "Created: $zipFileX64" -ForegroundColor Green
    Write-Host "Created: $zipFileArm" -ForegroundColor Green

    Write-Host ""
    Write-Host "Note: To create DMG files, run ./Installer/Mac/create-dmg.sh on macOS" -ForegroundColor Cyan
    Write-Host ""
}

# Build Linux
if ($Platform -eq "Linux" -or $Platform -eq "All") {
    Write-Host "Building Linux (x64)..." -ForegroundColor Yellow

    $linuxDir = Join-Path $PublishDir "linux-x64"

    dotnet publish "$ProjectRoot\FlipKit.Desktop" `
        -c Release `
        -r linux-x64 `
        --self-contained `
        -o $linuxDir

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Linux build failed"
        exit 1
    }

    Write-Host "Linux build completed: $linuxDir" -ForegroundColor Green

    # Create ZIP package (tar.gz and .deb/.rpm require running on Linux)
    $zipFile = Join-Path $InstallerDir "FlipKit-Linux-x64-v$Version.zip"
    if (Test-Path $zipFile) { Remove-Item $zipFile }
    Compress-Archive -Path "$linuxDir\*" -DestinationPath $zipFile

    Write-Host "Created: $zipFile" -ForegroundColor Green

    Write-Host ""
    Write-Host "Note: To create .deb/.rpm/.tar.gz, run ./Installer/Linux/build-packages.sh on Linux" -ForegroundColor Cyan
    Write-Host ""
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Build Complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Output directory: $InstallerDir" -ForegroundColor White
Write-Host ""
Get-ChildItem $InstallerDir | ForEach-Object {
    $size = [math]::Round($_.Length / 1MB, 2)
    Write-Host "  $($_.Name) ($size MB)" -ForegroundColor Gray
}
