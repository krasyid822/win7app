# install_wix_v6.ps1
param(
    [string]$Url
)

if ([string]::IsNullOrWhiteSpace($Url)) {
    Write-Host "No URL provided. Use -Url '<msi-url>' to download specific MSI." -ForegroundColor Red
    exit 2
}

$out = Join-Path $env:TEMP (Split-Path $Url -Leaf)
Write-Host "Downloading: $Url -> $out"
try {
    Invoke-WebRequest -UseBasicParsing -Uri $Url -OutFile $out -ErrorAction Stop
} catch {
    Write-Host "Download failed: $_" -ForegroundColor Red
    exit 3
}

Write-Host "Running installer (UAC will prompt)..."
Start-Process -FilePath $out -Verb RunAs -Wait
Write-Host "Installer finished"
