$exe = Get-Item 'Win7App\bin\Release\Win7App.exe'
Write-Host 'EXE:' $exe.FullName
Write-Host 'Size:' $exe.Length 'bytes'
$vi = $exe.VersionInfo
Write-Host 'FileDescription:' $vi.FileDescription
Write-Host 'ProductName:' $vi.ProductName
Write-Host '--- Checking embedded icon ---'
Add-Type -AssemblyName System.Drawing
try {
    $icon = [System.Drawing.Icon]::ExtractAssociatedIcon($exe.FullName)
    Write-Host 'Icon extracted:' $icon.Width 'x' $icon.Height
    $icon.Dispose()
} catch {
    Write-Host 'No icon embedded or error:' $_.Exception.Message
}
