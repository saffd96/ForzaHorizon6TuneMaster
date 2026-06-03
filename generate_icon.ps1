Add-Type -AssemblyName System.Drawing

function Draw-Torii($g, $size) {
    $bg     = [System.Drawing.Color]::FromArgb(255, 0x0C, 0x0E, 0x16)
    $orange = [System.Drawing.Color]::FromArgb(255, 0xFF, 0x5E, 0x0E)
    $cyan   = [System.Drawing.Color]::FromArgb(230, 0x1A, 0xBC, 0xFE)

    $s = $size / 32.0
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

    # Background rounded rect
    $r = [int](6 * $s)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc(0, 0, $r*2, $r*2, 180, 90)
    $path.AddArc($size - $r*2, 0, $r*2, $r*2, 270, 90)
    $path.AddArc($size - $r*2, $size - $r*2, $r*2, $r*2, 0, 90)
    $path.AddArc(0, $size - $r*2, $r*2, $r*2, 90, 90)
    $path.CloseFigure()
    $g.FillPath((New-Object System.Drawing.SolidBrush($bg)), $path)

    # Sun circle
    $g.FillEllipse(
        (New-Object System.Drawing.SolidBrush($cyan)),
        [int](11 * $s), [int](1.5 * $s), [int](10 * $s), [int](10 * $s))

    $br = New-Object System.Drawing.SolidBrush($orange)

    # Kasagi (top crossbar)
    $g.FillRectangle($br, [int](3 * $s), [int](10 * $s), [int](26 * $s), [int](3.5 * $s))

    # Nuki (mid crossbar)
    $g.FillRectangle($br, [int](6.5 * $s), [int](16 * $s), [int](19 * $s), [int](2.5 * $s))

    # Left pillar
    $g.FillRectangle($br, [int](7.5 * $s), [int](10 * $s), [int](4 * $s), [int](20 * $s))

    # Right pillar
    $g.FillRectangle($br, [int](20.5 * $s), [int](10 * $s), [int](4 * $s), [int](20 * $s))
}

function Get-PngBytes($bmp) {
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    return $ms.ToArray()
}

function Add-U16($list, $val) {
    $list.AddRange([byte[]][System.BitConverter]::GetBytes([uint16]$val))
}
function Add-U32($list, $val) {
    $list.AddRange([byte[]][System.BitConverter]::GetBytes([uint32]$val))
}

$sizes = @(256, 48, 32, 16)
$pngList = [System.Collections.Generic.List[byte[]]]::new()

foreach ($sz in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($sz, $sz, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::Transparent)
    Draw-Torii $g $sz
    $g.Dispose()
    $pngList.Add([byte[]](Get-PngBytes $bmp))
    $bmp.Dispose()
}

# Build ICO binary
$ico = [System.Collections.Generic.List[byte]]::new()

Add-U16 $ico 0               # reserved
Add-U16 $ico 1               # type: ICO
Add-U16 $ico $sizes.Count    # image count

# Directory entries
$offset = 6 + $sizes.Count * 16
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $sz   = $sizes[$i]
    $data = [byte[]]$pngList[$i]
    $ico.Add([byte]$(if ($sz -ge 256) { 0 } else { $sz }))  # width  (0 = 256)
    $ico.Add([byte]$(if ($sz -ge 256) { 0 } else { $sz }))  # height
    $ico.Add([byte]0)   # color count
    $ico.Add([byte]0)   # reserved
    Add-U16 $ico 1      # planes
    Add-U16 $ico 32     # bits per pixel
    Add-U32 $ico $data.Length
    Add-U32 $ico $offset
    $offset += $data.Length
}

# PNG image data
foreach ($data in $pngList) { $ico.AddRange([byte[]]$data) }

$outPath = Join-Path $PSScriptRoot "Forza Horizon 6 Tune Master\icon.ico"
[System.IO.File]::WriteAllBytes($outPath, $ico.ToArray())
Write-Host "Written: $outPath  ($($ico.Count) bytes)"
