# Generates Assets/app.ico – the StandReminder app icon.
# Green circle with a white standing figure, drawn at all standard sizes,
# matching the runtime-drawn tray icons in App.xaml.cs.
# Run from repo root:  powershell -File tools\generate-icon.ps1

Add-Type -AssemblyName System.Drawing

$repoRoot = Split-Path $PSScriptRoot -Parent
$assetsDir = Join-Path $repoRoot 'Assets'
New-Item -ItemType Directory -Path $assetsDir -Force | Out-Null

$sizes = 16, 24, 32, 48, 64, 128, 256
$frames = @()

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)

    $k = $s / 32.0   # scale factor: base drawing is designed on a 32x32 grid

    $bg = New-Object System.Drawing.SolidBrush([System.Drawing.Color]::FromArgb(70, 194, 142))
    $g.FillEllipse($bg, [float](1 * $k), [float](1 * $k), [float](30 * $k), [float](30 * $k))

    $pen = New-Object System.Drawing.Pen([System.Drawing.Color]::White, [float](2.8 * $k))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round

    # standing stick figure (same geometry as CreateStandIcon in App.xaml.cs)
    $g.FillEllipse([System.Drawing.Brushes]::White,
        [float](13.25 * $k), [float](3.5 * $k), [float](5.5 * $k), [float](5.5 * $k))
    $g.DrawLine($pen, [float](16 * $k), [float](10 * $k), [float](16 * $k), [float](19.5 * $k))
    $g.DrawLine($pen, [float](16 * $k), [float](12.5 * $k), [float](11.5 * $k), [float](16.5 * $k))
    $g.DrawLine($pen, [float](16 * $k), [float](12.5 * $k), [float](20.5 * $k), [float](16.5 * $k))
    $g.DrawLine($pen, [float](16 * $k), [float](19.5 * $k), [float](12.5 * $k), [float](27 * $k))
    $g.DrawLine($pen, [float](16 * $k), [float](19.5 * $k), [float](19.5 * $k), [float](27 * $k))

    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $frames += , @($s, $ms.ToArray())
    $bmp.Dispose()
}

# assemble ICO container (PNG-compressed entries, valid since Windows Vista)
$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($out)
$bw.Write([uint16]0)              # reserved
$bw.Write([uint16]1)              # type: icon
$bw.Write([uint16]$frames.Count)  # image count

$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $s = $f[0]; $data = $f[1]
    $dim = if ($s -ge 256) { 0 } else { $s }   # 0 means 256 in ICO format
    $bw.Write([byte]$dim)         # width
    $bw.Write([byte]$dim)         # height
    $bw.Write([byte]0)            # palette colors
    $bw.Write([byte]0)            # reserved
    $bw.Write([uint16]1)          # color planes
    $bw.Write([uint16]32)         # bits per pixel
    $bw.Write([uint32]$data.Length)
    $bw.Write([uint32]$offset)
    $offset += $data.Length
}
foreach ($f in $frames) { $bw.Write($f[1]) }

$icoPath = Join-Path $assetsDir 'app.ico'
[System.IO.File]::WriteAllBytes($icoPath, $out.ToArray())
Write-Host "OK: $icoPath ($($out.Length) B, sizes: $($sizes -join ', '))"
