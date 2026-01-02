# setup_tools.ps1
# Detect and prepare packaging tools: Chocolatey, WiX Toolset, Inno Setup

param(
    [switch]$AutoInstall
)

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$toolsDir = Join-Path $scriptDir "tools"
if (-not (Test-Path $toolsDir)) { New-Item -ItemType Directory -Path $toolsDir | Out-Null }

function Test-Tool {
    param($Paths)
    foreach ($p in $Paths) { if (Test-Path $p) { return $p } }
    return $null
}

# Typical paths
$isccPaths = @("C:\Program Files (x86)\Inno Setup 6\ISCC.exe", "C:\Program Files\Inno Setup 6\ISCC.exe", "C:\Program Files (x86)\Inno Setup 5\ISCC.exe")
$wixPaths = @("C:\Program Files (x86)\WiX Toolset v3.11\bin\candle.exe", "C:\Program Files\WiX Toolset v3.11\bin\candle.exe")

$choco = Get-Command choco -ErrorAction SilentlyContinue
$iscc = Test-Tool $isccPaths
$wix = Test-Tool $wixPaths

Write-Host "Tool check:" -ForegroundColor Cyan
Write-Host "  Chocolatey: " -NoNewline; if ($choco) { Write-Host "Found ($($choco.Source))" -ForegroundColor Green } else { Write-Host "Not found" -ForegroundColor Yellow }
Write-Host "  Inno Setup: " -NoNewline; if ($iscc) { Write-Host "Found ($iscc)" -ForegroundColor Green } else { Write-Host "Not found" -ForegroundColor Yellow }
Write-Host "  WiX Toolset: " -NoNewline; if ($wix) { Write-Host "Found ($wix)" -ForegroundColor Green } else { Write-Host "Not found" -ForegroundColor Yellow }

if ($iscc -and $wix) {
    Write-Host "All required packaging tools are present." -ForegroundColor Green
    return 0
}

# If AutoInstall and choco exists, try installing
if ($AutoInstall -and $choco) {
    Write-Host "Attempting installation via Chocolatey..." -ForegroundColor Cyan
    if (-not $wix) {
        Write-Host "Installing WiX Toolset..." -ForegroundColor Yellow
        choco install wix -y
    }
    if (-not $iscc) {
        Write-Host "Installing InnoSetup..." -ForegroundColor Yellow
        choco install innosetup -y
    }

    # Re-evaluate
    $iscc = Test-Tool $isccPaths
    $wix = Test-Tool $wixPaths
    if ($iscc -and $wix) {
        Write-Host "Installation successful." -ForegroundColor Green
        return 0
    } else {
        Write-Host "Some tools still missing after choco install." -ForegroundColor Red
    }
}

# If not auto or choco not available, provide manual instructions and download hints
Write-Host "\nNext steps to prepare tools:" -ForegroundColor Cyan
if (-not $choco) {
    Write-Host "- (Optional) Install Chocolatey to enable automatic installs:" -ForegroundColor Gray
    Write-Host "  Run (as Admin):" -ForegroundColor Gray
    Write-Host "    Set-ExecutionPolicy Bypass -Scope Process -Force; [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor 3072; iex ((New-Object System.Net.WebClient).DownloadString('https://chocolatey.org/install.ps1'))" -ForegroundColor White
}

if (-not $wix) {
    Write-Host "- Install WiX Toolset v3.11:" -ForegroundColor Gray
    Write-Host "  Official releases: https://wixtoolset.org/releases/" -ForegroundColor White
    Write-Host "  If you have Chocolatey: 'choco install wix -y'" -ForegroundColor Gray
}

if (-not $iscc) {
    Write-Host "- Install Inno Setup:" -ForegroundColor Gray
    Write-Host "  Official: https://jrsoftware.org/isinfo.php" -ForegroundColor White
    Write-Host "  If you have Chocolatey: 'choco install innosetup -y'" -ForegroundColor Gray
}

Write-Host "\nAfter installing, re-run this script or run the build script." -ForegroundColor Cyan
return 1
