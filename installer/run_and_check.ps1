$ErrorActionPreference = 'Continue'
$exe = 'C:\Program Files (x86)\Win7 Virtual Monitor\Win7App.exe'
if (-not (Test-Path $exe)) { Write-Host "Executable not found: $exe"; exit 2 }
$p = Start-Process -FilePath $exe -WindowStyle Minimized -PassThru
Write-Host "Started PID: $($p.Id)"
Start-Sleep -Seconds 3
Write-Host '--- netstat ---'
netstat -ano | Select-String ':8080|:8081' | ForEach-Object { $_.Line }
Write-Host '--- http check ---'
try {
    $r = Invoke-WebRequest -Uri 'http://127.0.0.1:8080/' -UseBasicParsing -TimeoutSec 5
    Write-Host 'HTTP Status:' $r.StatusCode
} catch {
    Write-Host 'HTTP request failed:' $_.Exception.Message
}
Write-Host '--- process info ---'
Get-Process -Id $p.Id -ErrorAction SilentlyContinue | Select-Object Id, ProcessName, StartTime | Format-List
