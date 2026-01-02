$ErrorActionPreference = 'Continue'

Write-Host "Inspecting installation folder and shortcuts for Win7 Virtual Monitor`n" -ForegroundColor Cyan

$installDir = 'C:\Program Files (x86)\Win7 Virtual Monitor'
$exePath = Join-Path $installDir 'Win7App.exe'

if (Test-Path $installDir) {
    Write-Host "Install folder: $installDir (exists)" -ForegroundColor Green
    Write-Host "Files in install folder:" -ForegroundColor Yellow
    Get-ChildItem -Path $installDir -Recurse -File | Select-Object @{n='Path';e={$_.FullName}},Length,LastWriteTime | Format-Table -AutoSize
} else {
    Write-Host "Install folder not found: $installDir" -ForegroundColor Red
}

if (Test-Path $exePath) {
    $fi = Get-Item $exePath
    Write-Host "\nExecutable:" -ForegroundColor Yellow
    Write-Host "Path: $($fi.FullName)";
    Write-Host "Size: $($fi.Length) bytes";
    Write-Host "Product: $($fi.VersionInfo.ProductName)";
    Write-Host "FileDescription: $($fi.VersionInfo.FileDescription)";
}

# Shortcut locations
$startMenuAll = Join-Path $env:ProgramData 'Microsoft\Windows\Start Menu\Programs\Win7 Virtual Monitor'
$startMenuUser = Join-Path $env:AppData 'Microsoft\Windows\Start Menu\Programs\Win7 Virtual Monitor'
$publicDesktop = Join-Path $env:Public 'Desktop'
$userDesktop = Join-Path $env:UserProfile 'Desktop'

$shortcutPaths = @()
if (Test-Path $startMenuAll) { $shortcutPaths += Get-ChildItem -Path $startMenuAll -Filter *.lnk -Recurse -ErrorAction SilentlyContinue }
if (Test-Path $startMenuUser) { $shortcutPaths += Get-ChildItem -Path $startMenuUser -Filter *.lnk -Recurse -ErrorAction SilentlyContinue }
if (Test-Path $publicDesktop) { $shortcutPaths += Get-ChildItem -Path $publicDesktop -Filter *.lnk -Recurse -ErrorAction SilentlyContinue }
if (Test-Path $userDesktop) { $shortcutPaths += Get-ChildItem -Path $userDesktop -Filter *.lnk -Recurse -ErrorAction SilentlyContinue }

if ($shortcutPaths.Count -eq 0) {
    Write-Host "No .lnk shortcuts found in Start Menu or Desktop." -ForegroundColor Yellow
} else {
    $shell = New-Object -ComObject WScript.Shell
    Write-Host "\nShortcut details:" -ForegroundColor Cyan
    foreach ($s in $shortcutPaths) {
        try {
            $lnk = $shell.CreateShortcut($s.FullName)
            [PSCustomObject]@{
                Shortcut = $s.FullName
                Target = $lnk.TargetPath
                Arguments = $lnk.Arguments
                WorkingDirectory = $lnk.WorkingDirectory
                IconLocation = $lnk.IconLocation
                Description = $lnk.Description
            } | Format-List
        } catch {
            Write-Host "Failed to read shortcut: $($s.FullName)" -ForegroundColor Red
        }
    }
}

# List icon files in install folder
Write-Host "\nIcon files in install folder:" -ForegroundColor Yellow
Get-ChildItem -Path $installDir -Include *.ico,*.png,*.jpg -Recurse -ErrorAction SilentlyContinue | Select-Object FullName, Length | Format-Table -AutoSize

Write-Host "\nDone." -ForegroundColor Green
