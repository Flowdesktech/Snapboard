<#
Generates Snapboard's app icon (a selection frame with a blurred square accent)
at multiple resolutions and packages them into a single Windows .ico file.

Run once; the resulting snapboard.ico is committed to Assets\ and referenced
by Snapboard.csproj as <ApplicationIcon>.
#>

param(
    [string]$OutFile = (Join-Path $PSScriptRoot "..\Snapboard\Assets\snapboard.ico")
)

Add-Type -AssemblyName System.Drawing

function New-SnapboardBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    # --- Rounded-rectangle background with gradient ---
    $radius = [int]($size * 0.22)
    $rect = [System.Drawing.RectangleF]::new(0, 0, $size, $size)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $radius * 2
    $path.AddArc($rect.X,               $rect.Y,                $d, $d, 180, 90)
    $path.AddArc($rect.Right  - $d,     $rect.Y,                $d, $d, 270, 90)
    $path.AddArc($rect.Right  - $d,     $rect.Bottom - $d,      $d, $d,   0, 90)
    $path.AddArc($rect.X,               $rect.Bottom - $d,      $d, $d,  90, 90)
    $path.CloseAllFigures()

    $grad = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect,
        [System.Drawing.Color]::FromArgb(255, 61, 169, 252),   # #3DA9FC
        [System.Drawing.Color]::FromArgb(255,  30, 122, 208),  # darker blue
        [System.Drawing.Drawing2D.LinearGradientMode]::ForwardDiagonal)
    $g.FillPath($grad, $path)
    $grad.Dispose()

    # Subtle inner highlight
    $hl = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $rect,
        [System.Drawing.Color]::FromArgb(35,  255, 255, 255),
        [System.Drawing.Color]::FromArgb(0,   255, 255, 255),
        [System.Drawing.Drawing2D.LinearGradientMode]::Vertical)
    $g.FillPath($hl, $path)
    $hl.Dispose()

    # --- Corner brackets (selection frame) ---
    $bracketLen   = [Math]::Max(3, [int]($size * 0.22))
    $bracketThick = [Math]::Max(2, [int]($size * 0.085))
    $margin       = [int]($size * 0.27)

    $pen = New-Object System.Drawing.Pen(
        [System.Drawing.Color]::FromArgb(245, 255, 255, 255),
        $bracketThick)
    $pen.StartCap    = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap      = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.LineJoin    = [System.Drawing.Drawing2D.LineJoin]::Round

    $corners = @(
        @([System.Drawing.PointF]::new($margin,                   $margin + $bracketLen),
          [System.Drawing.PointF]::new($margin,                   $margin),
          [System.Drawing.PointF]::new($margin + $bracketLen,     $margin)),
        @([System.Drawing.PointF]::new($size - $margin - $bracketLen, $margin),
          [System.Drawing.PointF]::new($size - $margin,               $margin),
          [System.Drawing.PointF]::new($size - $margin,               $margin + $bracketLen)),
        @([System.Drawing.PointF]::new($size - $margin,               $size - $margin - $bracketLen),
          [System.Drawing.PointF]::new($size - $margin,               $size - $margin),
          [System.Drawing.PointF]::new($size - $margin - $bracketLen, $size - $margin)),
        @([System.Drawing.PointF]::new($margin + $bracketLen,     $size - $margin),
          [System.Drawing.PointF]::new($margin,                   $size - $margin),
          [System.Drawing.PointF]::new($margin,                   $size - $margin - $bracketLen))
    )
    foreach ($pts in $corners) {
        $g.DrawLines($pen, [System.Drawing.PointF[]]$pts)
    }
    $pen.Dispose()

    # --- Blur-feature hint: soft squircle dot in the center (only if roomy enough) ---
    if ($size -ge 32) {
        $dotSize = [int]($size * 0.28)
        $cx = ($size - $dotSize) / 2
        $cy = ($size - $dotSize) / 2
        $dotRect = [System.Drawing.RectangleF]::new($cx, $cy, $dotSize, $dotSize)

        $dotPath = New-Object System.Drawing.Drawing2D.GraphicsPath
        $dr = [int]($dotSize * 0.35)
        $dd = $dr * 2
        $dotPath.AddArc($dotRect.X,                      $dotRect.Y,                      $dd, $dd, 180, 90)
        $dotPath.AddArc($dotRect.Right  - $dd,           $dotRect.Y,                      $dd, $dd, 270, 90)
        $dotPath.AddArc($dotRect.Right  - $dd,           $dotRect.Bottom - $dd,           $dd, $dd,   0, 90)
        $dotPath.AddArc($dotRect.X,                      $dotRect.Bottom - $dd,           $dd, $dd,  90, 90)
        $dotPath.CloseAllFigures()

        $dotBrush = New-Object System.Drawing.Drawing2D.PathGradientBrush($dotPath)
        $dotBrush.CenterColor   = [System.Drawing.Color]::FromArgb(230, 255, 255, 255)
        $dotBrush.SurroundColors = @([System.Drawing.Color]::FromArgb(80, 255, 255, 255))
        $g.FillPath($dotBrush, $dotPath)
        $dotBrush.Dispose()
        $dotPath.Dispose()
    }

    $g.Dispose()
    $path.Dispose()
    return $bmp
}

function Write-IcoFromBitmaps([System.Drawing.Bitmap[]]$bitmaps, [string]$outPath) {
    $pngBlobs = @()
    foreach ($bmp in $bitmaps) {
        $ms = New-Object System.IO.MemoryStream
        $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
        $pngBlobs += ,($ms.ToArray())
        $ms.Dispose()
    }

    $count = $bitmaps.Count
    $headerSize = 6 + 16 * $count
    $offset = $headerSize

    $dir = Split-Path -Parent $outPath
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir | Out-Null }

    $fs = [System.IO.File]::Create($outPath)
    $bw = New-Object System.IO.BinaryWriter($fs)

    # ICONDIR
    $bw.Write([UInt16]0)      # reserved
    $bw.Write([UInt16]1)      # type = 1 (icon)
    $bw.Write([UInt16]$count) # image count

    # ICONDIRENTRY[count]
    for ($i = 0; $i -lt $count; $i++) {
        $s = $bitmaps[$i].Width
        $dim = if ($s -ge 256) { 0 } else { [byte]$s }   # 0 represents 256
        $bw.Write([byte]$dim)       # width
        $bw.Write([byte]$dim)       # height
        $bw.Write([byte]0)          # palette colors
        $bw.Write([byte]0)          # reserved
        $bw.Write([UInt16]1)        # color planes
        $bw.Write([UInt16]32)       # bits per pixel
        $bw.Write([UInt32]$pngBlobs[$i].Length)
        $bw.Write([UInt32]$offset)
        $offset += $pngBlobs[$i].Length
    }

    # PNG blobs (modern Windows supports PNG entries in ICO)
    for ($i = 0; $i -lt $count; $i++) {
        $bw.Write($pngBlobs[$i])
    }

    $bw.Flush()
    $bw.Dispose()
    $fs.Dispose()
}

$sizes = @(16, 24, 32, 48, 64, 128, 256)
$bitmaps = $sizes | ForEach-Object { New-SnapboardBitmap $_ }
Write-IcoFromBitmaps $bitmaps $OutFile
$bitmaps | ForEach-Object { $_.Dispose() }

Write-Host "Wrote $OutFile  ($([Math]::Round((Get-Item $OutFile).Length / 1KB, 1)) KB, $($sizes.Count) sizes)"
