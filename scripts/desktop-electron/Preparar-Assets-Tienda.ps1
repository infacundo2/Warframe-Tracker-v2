[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$sourcePath = Join-Path $repositoryRoot "desktop-electron\build\icon.png"
$outputRoot = Join-Path $repositoryRoot "docs\publishing\store-assets"
New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null

Add-Type -AssemblyName System.Drawing
$source = [System.Drawing.Image]::FromFile($sourcePath)
try {
    $icon = [System.Drawing.Bitmap]::new(55, 55)
    $graphics = [System.Drawing.Graphics]::FromImage($icon)
    try {
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.DrawImage($source, 0, 0, 55, 55)
        $icon.Save((Join-Path $outputRoot "app-icon.png"), [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally { $graphics.Dispose(); $icon.Dispose() }

    $tile = [System.Drawing.Bitmap]::new(258, 198)
    $graphics = [System.Drawing.Graphics]::FromImage($tile)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $rectangle = [System.Drawing.Rectangle]::new(0, 0, 258, 198)
        $background = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
            $rectangle,
            [System.Drawing.Color]::FromArgb(5, 11, 18),
            [System.Drawing.Color]::FromArgb(10, 37, 49),
            32)
        $graphics.FillRectangle($background, $rectangle)
        $background.Dispose()
        $graphics.DrawImage($source, 79, 18, 100, 100)
        $titleFont = [System.Drawing.Font]::new("Segoe UI", 15, [System.Drawing.FontStyle]::Bold)
        $subtitleFont = [System.Drawing.Font]::new("Segoe UI", 8, [System.Drawing.FontStyle]::Regular)
        $center = [System.Drawing.StringFormat]::new()
        $center.Alignment = [System.Drawing.StringAlignment]::Center
        $graphics.DrawString("WARFRAME TRACKER", $titleFont, [System.Drawing.Brushes]::White,
            [System.Drawing.RectangleF]::new(0, 126, 258, 30), $center)
        $cyanBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(119, 231, 255))
        $graphics.DrawString("INVENTARIO - RELIQUIAS - FARMEO", $subtitleFont, $cyanBrush,
            [System.Drawing.RectangleF]::new(0, 158, 258, 22), $center)
        $cyanBrush.Dispose(); $center.Dispose(); $titleFont.Dispose(); $subtitleFont.Dispose()
        $encoder = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() |
            Where-Object MimeType -eq "image/jpeg"
        $parameters = [System.Drawing.Imaging.EncoderParameters]::new(1)
        $parameters.Param[0] = [System.Drawing.Imaging.EncoderParameter]::new(
            [System.Drawing.Imaging.Encoder]::Quality, [long]88)
        $tile.Save((Join-Path $outputRoot "tile-258x198.jpg"), $encoder, $parameters)
        $parameters.Dispose()
    }
    finally { $graphics.Dispose(); $tile.Dispose() }
}
finally { $source.Dispose() }

Get-ChildItem -LiteralPath $outputRoot | Select-Object Name,Length
