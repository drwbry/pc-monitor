# Generates icon.ico for PcMonitor.App at 16/32/48/256px.
# Run once from repo root: powershell -ExecutionPolicy Bypass -File .\app\install\generate-icon.ps1
$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing

function New-PulseIconBitmap([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap($size, $size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.Clear([System.Drawing.Color]::Transparent)

    # rounded-rect background #161B22
    $r   = [int]($size * 0.18)
    $bg  = New-Object System.Drawing.SolidBrush(
               [System.Drawing.Color]::FromArgb(255, 22, 27, 34))
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0,          0,          $r*2, $r*2, 180, 90)
    $path.AddArc($size-$r*2, 0,          $r*2, $r*2, 270, 90)
    $path.AddArc($size-$r*2, $size-$r*2, $r*2, $r*2,   0, 90)
    $path.AddArc(0,          $size-$r*2, $r*2, $r*2,  90, 90)
    $path.CloseFigure()
    $g.FillPath($bg, $path)

    # subtle border #30363D
    $borderWidth = [float][math]::Max(1, $size * 0.025)
    $border = New-Object System.Drawing.Pen(
                  [System.Drawing.Color]::FromArgb(80, 48, 54, 61), $borderWidth)
    $g.DrawPath($border, $path)

    # EKG pulse line in #3FB950
    $lw  = [float][math]::Max(1.5, $size * 0.07)
    $pen = New-Object System.Drawing.Pen(
               [System.Drawing.Color]::FromArgb(255, 63, 185, 80), $lw)
    $pen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round

    $pad = $size * 0.18
    $cx  = $size * 0.5
    $cy  = $size * 0.52

    $pts = @(
        [System.Drawing.PointF]::new([float]$pad,              [float]$cy),
        [System.Drawing.PointF]::new([float]($cx * 0.68),      [float]$cy),
        [System.Drawing.PointF]::new([float]($cx * 0.84),      [float]($size * 0.26)),
        [System.Drawing.PointF]::new([float]$cx,               [float]($size * 0.74)),
        [System.Drawing.PointF]::new([float]($cx * 1.16),      [float]($size * 0.26)),
        [System.Drawing.PointF]::new([float]($cx * 1.32),      [float]$cy),
        [System.Drawing.PointF]::new([float]($size - $pad),    [float]$cy)
    )
    $g.DrawLines($pen, $pts)

    $g.Dispose()
    return $bmp
}

# Encode each size as PNG bytes
$sizes = @(256, 48, 32, 16)
$pngs  = foreach ($s in $sizes) {
    $bmp = New-PulseIconBitmap $s
    $ms  = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    , $ms.ToArray()
}

# Assemble ICO (header + directory + PNG payloads)
$out = New-Object System.IO.MemoryStream
$bw  = New-Object System.IO.BinaryWriter($out)

$bw.Write([uint16]0)             # reserved
$bw.Write([uint16]1)             # type = icon
$bw.Write([uint16]$sizes.Count)

$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $bw.Write([byte]$(if ($s -ge 256) { 0 } else { $s }))
    $bw.Write([byte]$(if ($s -ge 256) { 0 } else { $s }))
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write([uint32]$pngs[$i].Length)
    $bw.Write([uint32]$offset)
    $offset += $pngs[$i].Length
}
foreach ($png in $pngs) { $bw.Write($png) }
$bw.Flush()

$root    = Split-Path -Parent $PSScriptRoot
$outPath = Join-Path $root "src\PcMonitor.App\icon.ico"
[System.IO.File]::WriteAllBytes($outPath, $out.ToArray())
Write-Host "Icon written to $outPath"
