$ErrorActionPreference = "Stop"
$Version = "3.5.0"

Write-Host "Building FlipKit Hub for Inno Setup..." -ForegroundColor Cyan

$hubDir = ".\releases\temp\FlipKit-Hub-Windows-x64-v$Version"
$serversDir = "$hubDir\servers"

# Clean and create directories
if (Test-Path $hubDir) { Remove-Item $hubDir -Recurse -Force }
New-Item -ItemType Directory -Path $hubDir -Force | Out-Null
New-Item -ItemType Directory -Path $serversDir -Force | Out-Null

# Build Desktop
Write-Host "Building Desktop..." -ForegroundColor Yellow
dotnet publish FlipKit.Desktop -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $hubDir
if ($LASTEXITCODE -ne 0) { throw "Desktop build failed" }

# Build Web
Write-Host "Building Web server..." -ForegroundColor Yellow
dotnet publish FlipKit.Web -c Release -r win-x64 --self-contained true -o $serversDir
if ($LASTEXITCODE -ne 0) { throw "Web build failed" }

# Build API (to separate temp then merge)
Write-Host "Building API server..." -ForegroundColor Yellow
$apiTemp = ".\releases\temp\api-temp-$Version"
dotnet publish FlipKit.Api -c Release -r win-x64 --self-contained true -o $apiTemp
if ($LASTEXITCODE -ne 0) { throw "API build failed" }

# Merge API files into servers (skip duplicates)
Get-ChildItem $apiTemp | ForEach-Object {
    $dest = Join-Path $serversDir $_.Name
    if (-not (Test-Path $dest)) {
        Move-Item $_.FullName $serversDir -Force
    }
}
Remove-Item $apiTemp -Recurse -Force

# Create README
"FlipKit Hub v$Version" | Out-File "$hubDir\README.txt" -Encoding UTF8

Write-Host ""
Write-Host "Hub build complete at: $hubDir" -ForegroundColor Green
Write-Host ""

# Run Inno Setup
Write-Host "Running Inno Setup..." -ForegroundColor Yellow
$isccPaths = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe"
)
$iscc = $null
foreach ($path in $isccPaths) {
    if (Test-Path $path) {
        $iscc = $path
        break
    }
}
if (-not $iscc) {
    Write-Host "Inno Setup not found. Checking PATH..." -ForegroundColor Yellow
    $iscc = "iscc"
}

& $iscc "/DVersion=$Version" ".\Installer\Windows\FlipKit.iss"

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "SUCCESS! Installer created:" -ForegroundColor Green
    Get-ChildItem ".\releases\FlipKit-Setup-*.exe" | ForEach-Object {
        Write-Host "  $($_.Name) ($([math]::Round($_.Length / 1MB, 2)) MB)" -ForegroundColor Cyan
    }
} else {
    Write-Host "Inno Setup failed with exit code $LASTEXITCODE" -ForegroundColor Red
}
