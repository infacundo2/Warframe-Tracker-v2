param(
    [Parameter(Mandatory = $true)]
    [ValidateRange(1, 10)]
    [int]$Numero,
    [ValidateRange(1, 15)]
    [int]$EsperaSegundos = 5,
    [string]$TituloVentana = ''
)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$screenshotsRoot = Join-Path $root 'screenshots-native'
$originalsRoot = Join-Path $screenshotsRoot 'originals'
if (-not (Test-Path -LiteralPath $screenshotsRoot)) {
    throw 'Ejecuta este script desde la carpeta generada de entrega.'
}

$names = @{
    1 = '01-native-window-en.jpg'
    2 = '02-gep-ready-en.jpg'
    3 = '03-inventory-captured-en.jpg'
    4 = '04-preview-en.jpg'
    5 = '05-command-center-en.jpg'
    6 = '06-warframes-en.jpg'
    7 = '07-relics-en.jpg'
    8 = '08-goal-planner-en.jpg'
    9 = '09-settings-es.jpg'
    10 = '10-offline-error-en.jpg'
}

Add-Type -AssemblyName System.Drawing
if (-not ('ActiveWindowCapture' -as [type])) {
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class ActiveWindowCapture {
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")]
    public static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hwnd);
    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hwnd, int command);
}
'@
}

Write-Host ''
Write-Host "Captura $Numero`: $($names[$Numero])" -ForegroundColor Cyan
Write-Host 'Ahora cambia a la ventana de Warframe Tracker y dejala visible.'
for ($seconds = $EsperaSegundos; $seconds -ge 1; $seconds--) {
    Write-Host "Capturando en $seconds..."
    Start-Sleep -Seconds 1
}

if ([string]::IsNullOrWhiteSpace($TituloVentana)) {
    $handle = [ActiveWindowCapture]::GetForegroundWindow()
} else {
    $process = Get-Process | Where-Object { $_.MainWindowTitle -like "*$TituloVentana*" } |
        Select-Object -First 1
    if (-not $process) { throw "No se encontro una ventana con el titulo: $TituloVentana" }
    $handle = $process.MainWindowHandle
    [void][ActiveWindowCapture]::ShowWindow($handle, 9)
    [void][ActiveWindowCapture]::SetForegroundWindow($handle)
    Start-Sleep -Milliseconds 800
}
$rect = New-Object ActiveWindowCapture+Rect
if ($handle -eq [IntPtr]::Zero -or -not [ActiveWindowCapture]::GetWindowRect($handle, [ref]$rect)) {
    throw 'No se pudo detectar la ventana activa.'
}
$width = $rect.Right - $rect.Left
$height = $rect.Bottom - $rect.Top
if ($width -lt 640 -or $height -lt 480) {
    throw "La ventana activa es demasiado pequena: ${width}x${height}."
}

New-Item -ItemType Directory -Path $originalsRoot -Force | Out-Null
$originalPath = Join-Path $originalsRoot ($names[$Numero] -replace '\.jpg$', '.png')
$bitmap = New-Object System.Drawing.Bitmap $width, $height
try {
    $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $graphics.CopyFromScreen($rect.Left, $rect.Top, 0, 0, $bitmap.Size)
    } finally { $graphics.Dispose() }
    $bitmap.Save($originalPath, [System.Drawing.Imaging.ImageFormat]::Png)
} finally { $bitmap.Dispose() }

$targetPath = Join-Path $screenshotsRoot $names[$Numero]
$converter = Join-Path $root 'PREPARAR-CAPTURA.ps1'
if (-not (Test-Path -LiteralPath $converter)) {
    throw 'Falta PREPARAR-CAPTURA.ps1 en la carpeta de entrega.'
}
& $converter -InputPath $originalPath -OutputPath $targetPath
Write-Host ''
Write-Host "Captura guardada: $targetPath" -ForegroundColor Green
Write-Host 'Abrela y comprueba que no muestre correo, claves, ID de cuenta ni JSON bruto.' -ForegroundColor Yellow
