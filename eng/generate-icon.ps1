[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$assetDirectory = Join-Path $repositoryRoot "assets"
$iconPath = Join-Path $assetDirectory "app-icon.ico"
$previewPath = Join-Path $assetDirectory "app-icon.png"

Add-Type -AssemblyName PresentationCore
Add-Type -AssemblyName WindowsBase

function New-IconPngBytes {
    param([Parameter(Mandatory)][int]$Size)

    $scale = $Size / 256.0
    $visual = [System.Windows.Media.DrawingVisual]::new()
    $drawing = $visual.RenderOpen()
    try {
        $background = [System.Windows.Media.SolidColorBrush]::new(
            [System.Windows.Media.Color]::FromRgb(7, 26, 61))
        $background.Freeze()
        $drawing.DrawRoundedRectangle(
            $background,
            $null,
            [System.Windows.Rect]::new(8 * $scale, 8 * $scale, 240 * $scale, 240 * $scale),
            55 * $scale,
            55 * $scale)

        $pen = [System.Windows.Media.Pen]::new(
            [System.Windows.Media.Brushes]::White,
            [Math]::Max(1, 14 * $scale))
        $pen.StartLineCap = [System.Windows.Media.PenLineCap]::Round
        $pen.EndLineCap = [System.Windows.Media.PenLineCap]::Round
        $pen.LineJoin = [System.Windows.Media.PenLineJoin]::Round
        $pen.Freeze()

        function Draw-Line([double]$x1, [double]$y1, [double]$x2, [double]$y2) {
            $drawing.DrawLine(
                $pen,
                [System.Windows.Point]::new($x1 * $scale, $y1 * $scale),
                [System.Windows.Point]::new($x2 * $scale, $y2 * $scale))
        }

        Draw-Line 96 72 72 72
        Draw-Line 72 72 72 96
        Draw-Line 160 72 184 72
        Draw-Line 184 72 184 96
        Draw-Line 72 160 72 184
        Draw-Line 72 184 96 184
        Draw-Line 160 184 184 184
        Draw-Line 184 184 184 160

        $arcPen = [System.Windows.Media.Pen]::new(
            [System.Windows.Media.SolidColorBrush]::new([System.Windows.Media.Color]::FromRgb(28, 200, 255)),
            [Math]::Max(1, 13 * $scale))
        $arcPen.StartLineCap = [System.Windows.Media.PenLineCap]::Round
        $arcPen.EndLineCap = [System.Windows.Media.PenLineCap]::Round
        $arcPen.Freeze()
        $arc = [System.Windows.Media.StreamGeometry]::new()
        $arcContext = $arc.Open()
        try {
            $arcContext.BeginFigure([System.Windows.Point]::new(78 * $scale, 169 * $scale), $false, $false)
            $arcContext.BezierTo(
                [System.Windows.Point]::new(84 * $scale, 121 * $scale),
                [System.Windows.Point]::new(124 * $scale, 91 * $scale),
                [System.Windows.Point]::new(169 * $scale, 88 * $scale),
                $true,
                $true)
        }
        finally {
            $arcContext.Close()
        }
        $arc.Freeze()
        $drawing.DrawGeometry($null, $arcPen, $arc)

        $spark = [System.Windows.Media.StreamGeometry]::new()
        $sparkContext = $spark.Open()
        try {
            $sparkContext.BeginFigure([System.Windows.Point]::new(181 * $scale, 67 * $scale), $true, $true)
            $sparkPoints = [System.Collections.Generic.List[System.Windows.Point]]::new()
            $sparkPoints.Add([System.Windows.Point]::new(186 * $scale, 79 * $scale))
            $sparkPoints.Add([System.Windows.Point]::new(198 * $scale, 84 * $scale))
            $sparkPoints.Add([System.Windows.Point]::new(186 * $scale, 89 * $scale))
            $sparkPoints.Add([System.Windows.Point]::new(181 * $scale, 101 * $scale))
            $sparkPoints.Add([System.Windows.Point]::new(176 * $scale, 89 * $scale))
            $sparkPoints.Add([System.Windows.Point]::new(164 * $scale, 84 * $scale))
            $sparkPoints.Add([System.Windows.Point]::new(176 * $scale, 79 * $scale))
            $sparkContext.PolyLineTo($sparkPoints, $true, $true)
        }
        finally {
            $sparkContext.Close()
        }
        $spark.Freeze()
        $sparkBrush = [System.Windows.Media.SolidColorBrush]::new(
            [System.Windows.Media.Color]::FromRgb(139, 92, 246))
        $sparkBrush.Freeze()
        $drawing.DrawGeometry($sparkBrush, $null, $spark)
    }
    finally {
        $drawing.Close()
    }

    $bitmap = [System.Windows.Media.Imaging.RenderTargetBitmap]::new(
        $Size,
        $Size,
        96,
        96,
        [System.Windows.Media.PixelFormats]::Pbgra32)
    $bitmap.Render($visual)
    $bitmap.Freeze()

    $encoder = [System.Windows.Media.Imaging.PngBitmapEncoder]::new()
    $encoder.Frames.Add([System.Windows.Media.Imaging.BitmapFrame]::Create($bitmap))
    $stream = [System.IO.MemoryStream]::new()
    try {
        $encoder.Save($stream)
        Write-Output -NoEnumerate $stream.ToArray()
    }
    finally {
        $stream.Dispose()
    }
}

New-Item -ItemType Directory -Path $assetDirectory -Force | Out-Null
$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$frames = foreach ($size in $sizes) {
    $pngBytes = [byte[]](New-IconPngBytes -Size $size)
    [pscustomobject]@{ Size = $size; Bytes = $pngBytes }
}

$iconStream = [System.IO.MemoryStream]::new()
$writer = [System.IO.BinaryWriter]::new($iconStream)
try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$frames.Count)
    $offset = 6 + (16 * $frames.Count)
    foreach ($frame in $frames) {
        $writer.Write([byte]$(if ($frame.Size -ge 256) { 0 } else { $frame.Size }))
        $writer.Write([byte]$(if ($frame.Size -ge 256) { 0 } else { $frame.Size }))
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$frame.Bytes.Length)
        $writer.Write([uint32]$offset)
        $offset += $frame.Bytes.Length
    }

    foreach ($frame in $frames) {
        $writer.Write([byte[]]$frame.Bytes)
    }

    $writer.Flush()
    [System.IO.File]::WriteAllBytes($iconPath, $iconStream.ToArray())
}
finally {
    $writer.Dispose()
    $iconStream.Dispose()
}

[System.IO.File]::WriteAllBytes($previewPath, [byte[]](New-IconPngBytes -Size 512))
Write-Host "Generated $iconPath and $previewPath"
