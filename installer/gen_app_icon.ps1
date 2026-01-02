# Generate a simple application icon (monitor/screen with streaming indicator)
Add-Type -AssemblyName System.Drawing

function Create-Icon {
    param([string]$OutPath, [int]$Size)
    
    $bmp = New-Object System.Drawing.Bitmap($Size, $Size)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.Clear([System.Drawing.Color]::Transparent)
    
    # Scale factor
    $s = $Size / 256.0
    
    # Monitor body (dark gray)
    $monitorBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(60, 60, 60))
    $monitorRect = New-Object System.Drawing.Rectangle([int](20*$s), [int](30*$s), [int](216*$s), [int](140*$s))
    $g.FillRectangle($monitorBrush, $monitorRect)
    
    # Screen (gradient blue)
    $screenRect = New-Object System.Drawing.Rectangle([int](30*$s), [int](40*$s), [int](196*$s), [int](110*$s))
    $gradientBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $screenRect,
        [System.Drawing.Color]::FromArgb(0, 120, 215),
        [System.Drawing.Color]::FromArgb(0, 80, 160),
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical
    )
    $g.FillRectangle($gradientBrush, $screenRect)
    
    # Stand base
    $standBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(80, 80, 80))
    $standPoints = @(
        (New-Object System.Drawing.Point([int](100*$s), [int](170*$s))),
        (New-Object System.Drawing.Point([int](156*$s), [int](170*$s))),
        (New-Object System.Drawing.Point([int](170*$s), [int](210*$s))),
        (New-Object System.Drawing.Point([int](86*$s), [int](210*$s)))
    )
    $g.FillPolygon($standBrush, $standPoints)
    
    # Stand foot
    $footRect = New-Object System.Drawing.Rectangle([int](60*$s), [int](210*$s), [int](136*$s), [int](16*$s))
    $g.FillRectangle($standBrush, $footRect)
    
    # WiFi/streaming icon on screen (white)
    $wifiBrush = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::White)
    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, [int](6*$s))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
    
    # Draw arcs for WiFi symbol
    $cx = [int](128*$s)
    $cy = [int](105*$s)
    for ($i = 1; $i -le 3; $i++) {
        $r = [int](20*$i*$s)
        $arcRect = New-Object System.Drawing.Rectangle(($cx - $r), ($cy - $r), ($r*2), ($r*2))
        $g.DrawArc($pen, $arcRect, 225, 90)
    }
    
    # Center dot
    $dotSize = [int](12*$s)
    $g.FillEllipse($wifiBrush, ($cx - $dotSize/2), ($cy - $dotSize/2), $dotSize, $dotSize)
    
    # Cleanup
    $g.Dispose()
    $monitorBrush.Dispose()
    $gradientBrush.Dispose()
    $standBrush.Dispose()
    $wifiBrush.Dispose()
    $pen.Dispose()
    
    return $bmp
}

function Save-Icon {
    param([string]$OutPath)
    
    # Create bitmaps at multiple sizes
    $sizes = @(256, 48, 32, 16)
    $bitmaps = @()
    foreach ($size in $sizes) {
        $bitmaps += Create-Icon -OutPath $OutPath -Size $size
    }
    
    # Write ICO file manually
    $fs = [System.IO.File]::Create($OutPath)
    $bw = New-Object System.IO.BinaryWriter($fs)
    
    # ICO header
    $bw.Write([UInt16]0)      # Reserved
    $bw.Write([UInt16]1)      # Type: 1 = ICO
    $bw.Write([UInt16]$sizes.Count)  # Number of images
    
    # Calculate offsets
    $headerSize = 6 + ($sizes.Count * 16)
    $offset = $headerSize
    $pngData = @()
    
    foreach ($bmp in $bitmaps) {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngData += ,($ms.ToArray())
        $ms.Dispose()
    }
    
    # Write directory entries
    for ($i = 0; $i -lt $sizes.Count; $i++) {
        $size = $sizes[$i]
        $data = $pngData[$i]
        
        $bw.Write([byte]($size -band 0xFF))  # Width (0 = 256)
        $bw.Write([byte]($size -band 0xFF))  # Height
        $bw.Write([byte]0)   # Color palette
        $bw.Write([byte]0)   # Reserved
        $bw.Write([UInt16]1) # Color planes
        $bw.Write([UInt16]32) # Bits per pixel
        $bw.Write([UInt32]$data.Length) # Size of image data
        $bw.Write([UInt32]$offset)      # Offset to image data
        
        $offset += $data.Length
    }
    
    # Write image data
    foreach ($data in $pngData) {
        $bw.Write($data)
    }
    
    $bw.Close()
    $fs.Close()
    
    # Cleanup bitmaps
    foreach ($bmp in $bitmaps) {
        $bmp.Dispose()
    }
    
    Write-Host "Icon saved to: $OutPath"
}

$icoPath = Join-Path $PSScriptRoot '..\Win7App\app.ico'
Save-Icon -OutPath $icoPath
Write-Host "Done."
