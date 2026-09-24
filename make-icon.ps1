# 生成 app.ico（16 / 32 / 48 三尺寸，PNG 压缩格式）。
# 仅在缺少 app.ico 时由 build.ps1 调用，也可单独运行。
[CmdletBinding()]
param(
    [string]$OutPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $MyInvocation.MyCommand.Definition
if (-not $OutPath) { $OutPath = Join-Path $root 'app.ico' }

function New-NoteBitmap {
    param([int]$Size)

    $scale = $Size / 32.0
    $bmp = New-Object System.Drawing.Bitmap -ArgumentList $Size, $Size
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $g.Clear([System.Drawing.Color]::Transparent)

        $x = [float](3 * $scale)
        $y = [float](4 * $scale)
        $w = [float](24 * $scale)
        $h = [float](24 * $scale)

        $fill = New-Object System.Drawing.SolidBrush -ArgumentList ([System.Drawing.Color]::FromArgb(255, 205, 60))
        $g.FillRectangle($fill, $x, $y, $w, $h)
        $fill.Dispose()

        $border = New-Object System.Drawing.Pen -ArgumentList ([System.Drawing.Color]::FromArgb(150, 110, 0)), ([float](2 * $scale))
        $g.DrawRectangle($border, $x, $y, $w, $h)
        $border.Dispose()

        $line = New-Object System.Drawing.Pen -ArgumentList ([System.Drawing.Color]::FromArgb(168, 136, 40)), ([float](2 * $scale))
        for ($i = 0; $i -lt 3; $i++) {
            $ly = [float]((11 + $i * 6) * $scale)
            $g.DrawLine($line, [float](7 * $scale), $ly, [float](17 * $scale), $ly)
        }
        $line.Dispose()
    }
    finally {
        $g.Dispose()
    }
    return $bmp
}

$sizes = @(16, 32, 48)
$entries = @()

foreach ($size in $sizes) {
    $bmp = New-NoteBitmap -Size $size
    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $entries += , [pscustomobject]@{ Size = $size; Data = $ms.ToArray() }
    $ms.Dispose()
    $bmp.Dispose()
}

$stream = [System.IO.File]::Create($OutPath)
$writer = New-Object System.IO.BinaryWriter -ArgumentList $stream
try {
    $writer.Write([uint16]0)                 # reserved
    $writer.Write([uint16]1)                 # type: icon
    $writer.Write([uint16]$entries.Count)    # image count

    $offset = 6 + 16 * $entries.Count
    foreach ($entry in $entries) {
        $dim = if ($entry.Size -ge 256) { 0 } else { $entry.Size }
        $writer.Write([byte]$dim)            # width
        $writer.Write([byte]$dim)            # height
        $writer.Write([byte]0)               # palette
        $writer.Write([byte]0)               # reserved
        $writer.Write([uint16]1)             # color planes
        $writer.Write([uint16]32)            # bits per pixel
        $writer.Write([uint32]$entry.Data.Length)
        $writer.Write([uint32]$offset)
        $offset += $entry.Data.Length
    }
    foreach ($entry in $entries) { $writer.Write($entry.Data) }
}
finally {
    $writer.Flush()
    $writer.Close()
}

Write-Host ("图标已生成: {0} ({1} bytes)" -f $OutPath, (Get-Item -LiteralPath $OutPath).Length)
