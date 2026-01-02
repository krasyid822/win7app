$ErrorActionPreference = 'Continue'
try {
    Write-Host "Querying uninstall registry for 'Win7 Virtual Monitor'..."
    $match = 'Win7 Virtual Monitor'
    $keys = @()
    $keys += Get-ChildItem 'HKLM:\Software\Microsoft\Windows\CurrentVersion\Uninstall' -ErrorAction SilentlyContinue
    $keys += Get-ChildItem 'HKLM:\Software\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall' -ErrorAction SilentlyContinue

    $found = @()
    foreach ($k in $keys) {
        $p = Get-ItemProperty -Path $k.PSPath -ErrorAction SilentlyContinue
        if ($p -and $p.DisplayName -and $p.DisplayName -like "*$match*") {
            $found += [PSCustomObject]@{
                Key = $k.PSPath
                DisplayName = $p.DisplayName
                InstallLocation = $p.InstallLocation
                UninstallString = $p.UninstallString
                Publisher = $p.Publisher
            }
        }
    }

    if ($found.Count -eq 0) {
        Write-Host 'No uninstall registry entry found.'
    } else {
        $found | Format-List
    }

    Write-Host "`nIf InstallLocation missing, searching common folders for Win7App.exe (this may take a moment)..."
    $paths = @('C:\Program Files','C:\Program Files (x86)','C:\ProgramData','C:\Users')
    $results = @()
    foreach ($p in $paths) {
        if (Test-Path $p) {
            Write-Host "Searching $p..."
            try {
                $r = Get-ChildItem -Path $p -Filter 'Win7App.exe' -Recurse -ErrorAction SilentlyContinue -Force
                if ($r) { $results += $r }
            } catch {
                # ignore access errors
            }
        }
    }

    if ($results.Count -gt 0) {
        $results | Select-Object FullName | Format-List
    } else {
        Write-Host 'No Win7App.exe found in common folders.'
    }
} catch {
    Write-Host 'Error during locate:' $_.Exception.Message
    exit 1
}
