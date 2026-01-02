$ErrorActionPreference = 'Continue'
try {
    $msi = Resolve-Path -Path (Join-Path $PSScriptRoot '..\output\Win7VirtualMonitor.msi') -ErrorAction Stop
    Write-Host "MSI path: $msi"

    Write-Host "Starting installer (elevation prompt may appear)..."
    Start-Process -FilePath 'msiexec.exe' -ArgumentList "/i `"$($msi.Path)`" /passive" -Verb RunAs -Wait
    Write-Host "msiexec exit code: $LASTEXITCODE"

    Write-Host "\nChecking install locations..."
    $paths = @(
        "$env:ProgramFiles\Win7 Virtual Monitor",
        "$env:ProgramFiles(x86)\Win7 Virtual Monitor"
    )
    foreach ($p in $paths) {
        Write-Host "$p :" (Test-Path $p)
    }

    $startMenu = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\Win7 Virtual Monitor'
    Write-Host "Start Menu folder exists:" (Test-Path $startMenu)

    Write-Host "\nChecking Uninstall registry entries (HKLM)..."
    $keys = @()
    $keys += Get-ChildItem 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall' -ErrorAction SilentlyContinue
    $keys += Get-ChildItem 'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall' -ErrorAction SilentlyContinue

    $found = @()
    foreach ($k in $keys) {
        $p = Get-ItemProperty -Path $k.PSPath -ErrorAction SilentlyContinue
        if ($p -and $p.DisplayName -and $p.DisplayName -like '*Win7*') {
            $found += [PSCustomObject]@{ DisplayName = $p.DisplayName; UninstallString = $p.UninstallString }
        }
    }
    if ($found.Count -gt 0) { $found | Format-List } else { Write-Host 'No uninstall entry found.' }
} catch {
    Write-Host 'Error during test install:' $_.Exception.Message
    exit 1
}
