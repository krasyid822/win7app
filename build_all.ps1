# Win7 Virtual Monitor - Build All Script
# Builds: Release EXE, MSI Installer, and Portable ZIP
# Usage: .\build_all.ps1

param(
    [switch]$SkipBuild,      # Skip EXE build (use existing)
    [switch]$MSIOnly,        # Only build MSI
    [switch]$PortableOnly,   # Only build Portable ZIP
    [switch]$Clean           # Clean output folder first
)

$ErrorActionPreference = 'Stop'

# Paths
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$projectDir = $scriptDir
$win7AppDir = Join-Path $projectDir 'Win7App'
$installerDir = Join-Path $projectDir 'installer'
$outputDir = Join-Path $projectDir 'output'
$releaseDir = Join-Path $win7AppDir 'bin\Release'
$csproj = Join-Path $win7AppDir 'Win7App.csproj'
$wxsFile = Join-Path $installerDir 'Win7App.wxs'

# Output files
$exePath = Join-Path $releaseDir 'Win7App.exe'
$msiPath = Join-Path $outputDir 'Win7VirtualMonitor.msi'
$zipPath = Join-Path $outputDir 'Win7VirtualMonitor_Portable.zip'

# Colors
function Write-Title($msg) { Write-Host "`n=== $msg ===" -ForegroundColor Cyan }
function Write-Success($msg) { Write-Host "[OK] $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "[!] $msg" -ForegroundColor Yellow }
function Write-Err($msg) { Write-Host "[X] $msg" -ForegroundColor Red }

Write-Host ""
Write-Host "============================================" -ForegroundColor Magenta
Write-Host " Win7 Virtual Monitor - Build All" -ForegroundColor Magenta
Write-Host "============================================" -ForegroundColor Magenta
Write-Host ""

# Create output directory
if ($Clean -and (Test-Path $outputDir)) {
    Write-Title "Cleaning output folder"
    Remove-Item -Path $outputDir -Recurse -Force
    Write-Success "Output folder cleaned"
}

if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir | Out-Null
    Write-Success "Created output folder: $outputDir"
}

# Step 1: Build EXE
if (-not $SkipBuild) {
    Write-Title "Step 1: Building Release EXE"
    
    # Ensure nuget.exe is available and restore packages (for classic packages.config projects)
    $nugetExe = Join-Path $scriptDir 'nuget.exe'
    if (-not (Test-Path $nugetExe)) {
        Write-Host "Downloading nuget.exe..."
        $nugetUrl = 'https://dist.nuget.org/win-x86-commandline/latest/nuget.exe'
        try {
            Invoke-WebRequest -Uri $nugetUrl -OutFile $nugetExe -UseBasicParsing -ErrorAction Stop
            Write-Success "Downloaded nuget.exe"
        } catch {
            Write-Warn "Could not download nuget.exe: $($_.Exception.Message)"
        }
    }

    if (Test-Path $nugetExe) {
        Write-Host "Installing NuGet packages from packages.config..."
        $pkgConfig = Join-Path $win7AppDir 'packages.config'
        $packagesOut = Join-Path $win7AppDir 'packages'
        if (Test-Path $pkgConfig) {
            & $nugetExe install $pkgConfig -OutputDirectory $packagesOut
            if ($LASTEXITCODE -ne 0) {
                Write-Warn "NuGet install failed with exit code $LASTEXITCODE"
            } else {
                Write-Success "NuGet packages installed to: $packagesOut"
            }
        } else {
            Write-Warn "No packages.config found at: $pkgConfig"
        }
    } else {
        Write-Warn "nuget.exe not available; continuing without package install"
    }

    # Find MSBuild
    $msbuildPaths = @(
        "${env:ProgramFiles}\Microsoft Visual Studio\2022\*\MSBuild\Current\Bin\MSBuild.exe",
        "${env:ProgramFiles(x86)}\Microsoft Visual Studio\2019\*\MSBuild\Current\Bin\MSBuild.exe",
        "C:\Windows\Microsoft.NET\Framework\v4.0.30319\MSBuild.exe"
    )
    
    $msbuild = $null
    foreach ($pattern in $msbuildPaths) {
        $found = Get-ChildItem -Path $pattern -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($found) { $msbuild = $found.FullName; break }
    }
    
    if (-not $msbuild) {
        Write-Err "MSBuild not found!"
        exit 1
    }
    
    Write-Host "Using MSBuild: $msbuild"
    
    & $msbuild $csproj /p:Configuration=Release /t:Rebuild /v:minimal
    if ($LASTEXITCODE -ne 0) {
        Write-Err "MSBuild failed with exit code $LASTEXITCODE"
        exit 1
    }
    
    if (Test-Path $exePath) {
        $exeInfo = Get-Item $exePath
        Write-Success "Built: $exePath ($($exeInfo.Length) bytes)"
    } else {
        Write-Err "EXE not found after build!"
        exit 1
    }
} else {
    Write-Title "Step 1: Skipping EXE build (using existing)"
    if (-not (Test-Path $exePath)) {
        Write-Err "EXE not found: $exePath"
        exit 1
    }
    Write-Success "Using existing: $exePath"
}

# Step 2: Build MSI
if (-not $PortableOnly) {
    Write-Title "Step 2: Building MSI Installer"
    
    # Check WiX
    $wix = Get-Command wix -ErrorAction SilentlyContinue
    if (-not $wix) {
        Write-Warn "WiX CLI not found in PATH. Skipping MSI build."
        Write-Warn "Install WiX Toolset v6: https://github.com/wixtoolset/wix/releases"
    } else {
        Write-Host "Using WiX: $($wix.Source)"
        
        Push-Location $installerDir
        try {
            wix build Win7App.wxs -out $msiPath
            if ($LASTEXITCODE -eq 0 -and (Test-Path $msiPath)) {
                $msiInfo = Get-Item $msiPath
                Write-Success "Built: $msiPath ($($msiInfo.Length) bytes)"
                
                # Remove .wixpdb if exists
                $wixpdb = $msiPath -replace '\.msi$', '.wixpdb'
                if (Test-Path $wixpdb) { Remove-Item $wixpdb -Force }
            } else {
                Write-Err "WiX build failed"
            }
        } finally {
            Pop-Location
        }
    }
}

# Step 3: Build Portable ZIP
if (-not $MSIOnly) {
    Write-Title "Step 3: Building Portable ZIP"
    
    # Create temp folder
    $tempDir = Join-Path $env:TEMP "Win7VirtualMonitor_Portable_$(Get-Date -Format 'yyyyMMddHHmmss')"
    New-Item -ItemType Directory -Path $tempDir | Out-Null
    
    # Copy all Release files (exe, dlls, certs)
    Copy-Item -Path (Join-Path $releaseDir "*") -Destination $tempDir -Recurse -Force
    
    # Copy icon if exists
    $icoPath = Join-Path $win7AppDir 'app.ico'
    if (Test-Path $icoPath) {
        Copy-Item $icoPath $tempDir
    }
    
    # Create README
    $readme = @"
Win7 Virtual Monitor - Portable Edition
========================================

Stream your Windows display to Android devices over WiFi.

Usage:
1. Run Win7App.exe as Administrator
2. Configure HTTP port (default: 8080) and optional HTTPS
3. Set a password for security
4. Click "Start Server"
5. On Android, open Chrome and navigate to:
   http://<your-pc-ip>:8080
6. Enter the password and enjoy!

Features:
- Real-time screen streaming (MJPEG)
- Touch input support
- Audio streaming (optional)
- PWA support (Add to Home Screen)
- Fullscreen mode

Requirements:
- Windows 7 or later
- .NET Framework 4.6.1
- Administrator privileges (for input injection)

Note: Make sure Windows Firewall allows the app on ports 8080/8081.
"@
    $readme | Out-File -FilePath (Join-Path $tempDir 'README.txt') -Encoding UTF8
    
    # Remove old ZIP if exists
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }
    
    # Create ZIP
    Compress-Archive -Path "$tempDir\*" -DestinationPath $zipPath -CompressionLevel Optimal
    
    # Cleanup temp
    Remove-Item $tempDir -Recurse -Force
    
    if (Test-Path $zipPath) {
        $zipInfo = Get-Item $zipPath
        Write-Success "Built: $zipPath ($($zipInfo.Length) bytes)"
    } else {
        Write-Err "Failed to create ZIP"
    }
}

# Summary
Write-Title "Build Summary"
Write-Host ""

$outputs = @()
if (Test-Path $exePath) {
    $outputs += [PSCustomObject]@{ Type = 'EXE'; Path = $exePath; Size = (Get-Item $exePath).Length }
}
if (Test-Path $msiPath) {
    $outputs += [PSCustomObject]@{ Type = 'MSI'; Path = $msiPath; Size = (Get-Item $msiPath).Length }
}
if (Test-Path $zipPath) {
    $outputs += [PSCustomObject]@{ Type = 'Portable ZIP'; Path = $zipPath; Size = (Get-Item $zipPath).Length }
}

$outputs | Format-Table -Property Type, @{n='Size (bytes)';e={$_.Size}}, Path -AutoSize

Write-Host ""
Write-Success "Build completed!"
Write-Host ""
Write-Host "Output folder: $outputDir" -ForegroundColor Gray
