# Win7 Virtual Monitor - Build Installer Script
# PowerShell version

param(
    [switch]$AutoInstall
)

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host " Win7 Virtual Monitor - Build Installer" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = Split-Path -Parent $scriptDir
$outputDir = Join-Path $projectDir "output"
$releaseDir = Join-Path $projectDir "Win7App\bin\Release"

# Create output directory
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
}

# Check if Release build exists
$exePath = Join-Path $releaseDir "Win7App.exe"
if (-not (Test-Path $exePath)) {
    Write-Host "ERROR: Win7App.exe not found in Release folder!" -ForegroundColor Red
    Write-Host "Please build the project first (Release configuration)" -ForegroundColor Yellow
    exit 1
}

# Detect Inno Setup and WiX; if missing, run setup_tools to guide/install
$isccPaths = @(
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 5\ISCC.exe"
)

$iscc = $null
foreach ($path in $isccPaths) {
    if (Test-Path $path) {
        $iscc = $path
        break
    }
}

$wixPath = "C:\Program Files (x86)\WiX Toolset v3.11\bin"

if (-not $iscc -or -not (Test-Path "$wixPath\candle.exe")) {
    Write-Host "Packaging tools not fully present. Running setup_tools.ps1 to prepare tools..." -ForegroundColor Yellow
    $setupScript = Join-Path $scriptDir "setup_tools.ps1"
    if (Test-Path $setupScript) {
        if ($AutoInstall) { & $setupScript -AutoInstall } else { & $setupScript }
        Start-Sleep -Seconds 2
        # Re-detect after attempt
        foreach ($path in $isccPaths) { if (Test-Path $path) { $iscc = $path; break } }
    } else {
        Write-Host "setup_tools.ps1 not found in installer folder." -ForegroundColor Red
    }
}

if ($iscc) {
    Write-Host "Found Inno Setup at: $iscc" -ForegroundColor Green
    Write-Host "Building EXE installer..." -ForegroundColor Yellow
    
    Push-Location $scriptDir
    & $iscc "Win7App.iss"
    $result = $LASTEXITCODE
    Pop-Location
    
    if ($result -eq 0) {
        Write-Host ""
        Write-Host "SUCCESS!" -ForegroundColor Green
        Write-Host "Installer created: $outputDir\Win7VirtualMonitor_Setup_v1.0.0.exe" -ForegroundColor Cyan
    } else {
        Write-Host "ERROR: Inno Setup compilation failed." -ForegroundColor Red
    }
    exit $result
}

# Try WiX
if (Test-Path "$wixPath\candle.exe") {
    Write-Host "Found WiX Toolset" -ForegroundColor Green
    Write-Host "Building MSI installer..." -ForegroundColor Yellow
    
    Push-Location $scriptDir
    & "$wixPath\candle.exe" "Win7App.wxs" -o "Win7App.wixobj"
    if ($LASTEXITCODE -eq 0) {
        & "$wixPath\light.exe" "Win7App.wixobj" -o "$outputDir\Win7VirtualMonitor.msi"
        if ($LASTEXITCODE -eq 0) {
            Write-Host ""
            Write-Host "SUCCESS!" -ForegroundColor Green
            Write-Host "Installer created: $outputDir\Win7VirtualMonitor.msi" -ForegroundColor Cyan
            Remove-Item "Win7App.wixobj" -ErrorAction SilentlyContinue
        }
    }
    Pop-Location
    exit $LASTEXITCODE
}

# No tool found - create ZIP as fallback
Write-Host "No installer tool found. Creating portable ZIP..." -ForegroundColor Yellow

$zipPath = Join-Path $outputDir "Win7VirtualMonitor_Portable.zip"

# Remove old zip if exists
if (Test-Path $zipPath) {
    Remove-Item $zipPath
}

# Create temp folder
$tempDir = Join-Path $env:TEMP "Win7VirtualMonitor"
if (Test-Path $tempDir) {
    Remove-Item $tempDir -Recurse -Force
}
New-Item -ItemType Directory -Path $tempDir | Out-Null

# Copy all Release files (exe, dlls, certs)
Copy-Item -Path (Join-Path $releaseDir "*") -Destination $tempDir -Recurse -Force

# Create ZIP
Compress-Archive -Path "$tempDir\*" -DestinationPath $zipPath -CompressionLevel Optimal

# Cleanup
Remove-Item $tempDir -Recurse -Force

Write-Host ""
Write-Host "SUCCESS!" -ForegroundColor Green
Write-Host "Portable ZIP created: $zipPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "For a proper installer, please install:" -ForegroundColor Yellow
Write-Host "  Inno Setup: https://jrsoftware.org/isdl.php" -ForegroundColor Gray
Write-Host "  WiX Toolset: https://wixtoolset.org/releases/" -ForegroundColor Gray
