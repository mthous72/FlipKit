# FlipKit Hub Release Build Script
# Builds unified Hub packages for Windows and Linux
# Version: 3.1.0

param(
    [string]$Version = "3.1.0"
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "FlipKit Hub v$Version Release Build" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Building unified packages for Windows and Linux" -ForegroundColor Yellow
Write-Host "(macOS excluded due to code signing requirements)" -ForegroundColor DarkGray
Write-Host ""

# Clean old releases
Write-Host "Cleaning old release folder..." -ForegroundColor Yellow
if (Test-Path ".\releases") {
    Remove-Item ".\releases" -Recurse -Force
}
New-Item -ItemType Directory -Path ".\releases" | Out-Null
New-Item -ItemType Directory -Path ".\releases\temp" | Out-Null

# Hub targets
$hubTargets = @(
    @{ Runtime = "win-x64"; Name = "Windows-x64"; Ext = "zip" },
    @{ Runtime = "linux-x64"; Name = "Linux-x64"; Ext = "zip" }
)

foreach ($target in $hubTargets) {
    Write-Host ""
    Write-Host "Building FlipKit Hub for $($target.Name)..." -ForegroundColor Green
    Write-Host "============================================" -ForegroundColor Green

    $hubDir = ".\releases\temp\FlipKit-Hub-$($target.Name)-v$Version"
    $serversDir = "$hubDir\servers"

    # Create folder structure
    New-Item -ItemType Directory -Path $hubDir -Force | Out-Null
    New-Item -ItemType Directory -Path $serversDir -Force | Out-Null

    # Build Desktop App
    Write-Host "  [1/3] Building Desktop app..." -ForegroundColor Yellow

    dotnet publish FlipKit.Desktop `
        -c Release `
        -r $target.Runtime `
        --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o "$hubDir"

    if ($LASTEXITCODE -ne 0) {
        throw "Desktop build failed for $($target.Name)"
    }

    Write-Host "    Desktop app built successfully" -ForegroundColor Green

    # Build Web Server
    Write-Host "  [2/3] Building Web server..." -ForegroundColor Yellow

    $webTempDir = ".\releases\temp\web-$($target.Runtime)"
    dotnet publish FlipKit.Web `
        -c Release `
        -r $target.Runtime `
        --self-contained true `
        -o "$webTempDir"

    if ($LASTEXITCODE -ne 0) {
        throw "Web build failed for $($target.Name)"
    }

    # Move Web server to servers folder
    Move-Item "$webTempDir\*" "$serversDir\" -Force
    Remove-Item $webTempDir -Recurse -Force

    Write-Host "    Web server built successfully" -ForegroundColor Green

    # Build API Server
    Write-Host "  [3/3] Building API server..." -ForegroundColor Yellow

    $apiTempDir = ".\releases\temp\api-$($target.Runtime)"
    dotnet publish FlipKit.Api `
        -c Release `
        -r $target.Runtime `
        --self-contained true `
        -o "$apiTempDir"

    if ($LASTEXITCODE -ne 0) {
        throw "API build failed for $($target.Name)"
    }

    # Move API server to servers folder
    Move-Item "$apiTempDir\*" "$serversDir\" -Force
    Remove-Item $apiTempDir -Recurse -Force

    Write-Host "    API server built successfully" -ForegroundColor Green

    # Copy Documentation
    Write-Host "  [4/4] Copying documentation..." -ForegroundColor Yellow

    $docsDir = "$hubDir\Docs"
    New-Item -ItemType Directory -Path $docsDir -Force | Out-Null

    Copy-Item ".\Docs\USER-GUIDE.md" "$docsDir\" -ErrorAction SilentlyContinue
    Copy-Item ".\Docs\WEB-USER-GUIDE.md" "$docsDir\" -ErrorAction SilentlyContinue
    Copy-Item ".\Docs\DEPLOYMENT-GUIDE.md" "$docsDir\" -ErrorAction SilentlyContinue
    Copy-Item ".\Docs\HUB-ARCHITECTURE.md" "$docsDir\" -ErrorAction SilentlyContinue
    Copy-Item ".\README.md" "$docsDir\" -ErrorAction SilentlyContinue
    Copy-Item ".\LICENSE" "$hubDir\" -ErrorAction SilentlyContinue

    # Create README.txt
    $exeName = if ($target.Runtime -like "win-*") { "FlipKit.Desktop.exe" } else { "FlipKit.Desktop" }

    $readmeContent = @"
FlipKit Hub v$Version

A unified package containing FlipKit Desktop, Web, and API servers.

QUICK START:
1. Launch $exeName
2. Servers auto-start automatically
3. Access Web UI from your phone at http://YOUR-IP:5000

DOCUMENTATION:
See the Docs folder for complete guides.

GETTING HELP:
https://github.com/mthous72/FlipKit/issues
"@

    $readmeContent | Out-File -FilePath "$hubDir\README.txt" -Encoding UTF8

    Write-Host "    Documentation added" -ForegroundColor Green

    # Create Archive
    Write-Host "  Creating archive..." -ForegroundColor Yellow

    $archiveName = "FlipKit-Hub-$($target.Name)-v$Version.$($target.Ext)"

    # Create archive (using zip for both platforms)
    Compress-Archive `
        -Path "$hubDir\*" `
        -DestinationPath ".\releases\$archiveName" `
        -Force

    Write-Host ""
    Write-Host "  Created: $archiveName" -ForegroundColor Green

    # Calculate size
    $size = (Get-Item ".\releases\$archiveName").Length / 1MB
    Write-Host "  Size: $([math]::Round($size, 2)) MB" -ForegroundColor Cyan

    # Clean up temp folder
    Remove-Item $hubDir -Recurse -Force
}

# Build complete
Write-Host ""
Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Build Complete!" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

Get-ChildItem ".\releases" -File | Format-Table Name, Length

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Test packages"
Write-Host "  2. Create GitHub release"
Write-Host "  3. Upload packages"
Write-Host ""
