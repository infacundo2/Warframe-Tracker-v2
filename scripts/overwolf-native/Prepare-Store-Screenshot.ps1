param(
    [Parameter(Mandatory = $true)][string]$InputPath,
    [Parameter(Mandatory = $true)][string]$OutputPath
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$sourcePath = (Resolve-Path -LiteralPath $InputPath).Path
$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
$outputDirectory = Split-Path -Parent $outputFullPath
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$source = [System.Drawing.Image]::FromFile($sourcePath)
try {
    $targetRatio = 16.0 / 9.0
    $sourceRatio = $source.Width / [double]$source.Height
    if ($sourceRatio -gt $targetRatio) {
        $cropHeight = $source.Height
        $cropWidth = [int][math]::Round($cropHeight * $targetRatio)
        $cropX = [int][math]::Floor(($source.Width - $cropWidth) / 2)
        $cropY = 0
    } else {
        $cropWidth = $source.Width
        $cropHeight = [int][math]::Round($cropWidth / $targetRatio)
        $cropX = 0
        $cropY = [int][math]::Floor(($source.Height - $cropHeight) / 2)
    }

    $bitmap = New-Object System.Drawing.Bitmap 1200, 675
    try {
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.DrawImage($source,
                (New-Object System.Drawing.Rectangle 0, 0, 1200, 675),
                (New-Object System.Drawing.Rectangle $cropX, $cropY, $cropWidth, $cropHeight),
                [System.Drawing.GraphicsUnit]::Pixel)
        } finally { $graphics.Dispose() }

        $jpeg = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() |
            Where-Object MimeType -eq 'image/jpeg'
        $quality = 88L
        do {
            $parameters = New-Object System.Drawing.Imaging.EncoderParameters 1
            $parameters.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter(
                [System.Drawing.Imaging.Encoder]::Quality, $quality)
            $bitmap.Save($outputFullPath, $jpeg, $parameters)
            $parameters.Dispose()
            $quality -= 6
        } while ((Get-Item -LiteralPath $outputFullPath).Length -gt 100KB -and $quality -ge 40)
    } finally { $bitmap.Dispose() }
} finally { $source.Dispose() }

$file = Get-Item -LiteralPath $outputFullPath
if ($file.Length -gt 100KB) { throw "The JPG is still larger than 100 KB: $($file.Length) bytes." }
Write-Host "Store screenshot ready: $($file.FullName) ($($file.Length) bytes, 1200x675)"
