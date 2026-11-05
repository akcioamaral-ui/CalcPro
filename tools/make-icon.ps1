# Gera src\app.ico (PNG 256 px embutido em ICO) com System.Drawing
param([string]$Out = "$PSScriptRoot\..\src\app.ico")
Add-Type -AssemblyName System.Drawing

function New-Png([int]$size) {
    $bmp = New-Object System.Drawing.Bitmap $size, $size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = 'AntiAlias'
    $g.TextRenderingHint = 'AntiAliasGridFit'
    $g.Clear([System.Drawing.Color]::Transparent)
    $r = [single]($size * 0.2)
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $m = [single]($size * 0.04); $w = [single]($size - 2 * $m)
    $path.AddArc($m, $m, 2 * $r, 2 * $r, 180, 90)
    $path.AddArc($m + $w - 2 * $r, $m, 2 * $r, 2 * $r, 270, 90)
    $path.AddArc($m + $w - 2 * $r, $m + $w - 2 * $r, 2 * $r, 2 * $r, 0, 90)
    $path.AddArc($m, $m + $w - 2 * $r, 2 * $r, 2 * $r, 90, 90)
    $path.CloseFigure()
    $bg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(30, 32, 38))
    $g.FillPath($bg, $path)
    $orange = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 149, 0))
    $white = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(240, 240, 240))
    # visor
    $g.FillRectangle((New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(14, 15, 18))), [single]($size*0.16), [single]($size*0.15), [single]($size*0.68), [single]($size*0.22))
    $font = New-Object System.Drawing.Font 'Segoe UI', ([single]($size * 0.16)), ([System.Drawing.FontStyle]::Bold), ([System.Drawing.GraphicsUnit]::Pixel)
    $sf = New-Object System.Drawing.StringFormat; $sf.Alignment = 'Far'; $sf.LineAlignment = 'Center'
    $g.DrawString([string][char]0x3C0, $font, $orange, (New-Object System.Drawing.RectangleF ([single]($size*0.16)), ([single]($size*0.15)), ([single]($size*0.64)), ([single]($size*0.22))), $sf)
    # teclas 3x2
    $kw = $size * 0.19; $gap = $size * 0.055; $x0 = $size * 0.16; $y0 = $size * 0.45
    for ($row = 0; $row -lt 2; $row++) {
        for ($col = 0; $col -lt 3; $col++) {
            $brush = $white
            if ($col -eq 2) { $brush = $orange }
            $g.FillEllipse($brush, [single]($x0 + $col * ($kw + $gap)), [single]($y0 + $row * ($kw + $gap)), [single]$kw, [single]$kw)
        }
    }
    $g.Dispose()
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    return ,$ms.ToArray()
}

$sizes = 256, 48, 32, 16
$pngs = @(); foreach ($s in $sizes) { $pngs += ,(New-Png $s) }
$fs = [System.IO.File]::Create($Out)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$sizes.Count)
$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]; $d = $pngs[$i]
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([byte]($(if ($s -ge 256) { 0 } else { $s })))
    $bw.Write([byte]0); $bw.Write([byte]0)
    $bw.Write([uint16]1); $bw.Write([uint16]32)
    $bw.Write([uint32]$d.Length); $bw.Write([uint32]$offset)
    $offset += $d.Length
}
foreach ($d in $pngs) { $bw.Write($d) }
$bw.Close()
Write-Host "Ícone gerado: $Out"
