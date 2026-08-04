[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$repositoryRoot = $PSScriptRoot
$desktopRoot = Join-Path $repositoryRoot "desktop"
$reportRoot = Join-Path $repositoryRoot "docs\publishing\reports\resolutions"
$previousElectronMode = $env:ELECTRON_RUN_AS_NODE
$matrix = @(
    "1366x720",
    "1366x768",
    "1920x1080",
    "2560x1440",
    "3840x2160"
)

New-Item -ItemType Directory -Path $reportRoot -Force | Out-Null
try {
    Push-Location $desktopRoot
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "Falló la compilación OW-Electron." }
    $env:ELECTRON_RUN_AS_NODE = $null

    foreach ($size in $matrix) {
        $report = Join-Path $reportRoot "$size.json"
        Remove-Item -LiteralPath $report -Force -ErrorAction SilentlyContinue
        & ".\node_modules\.bin\ow-electron.cmd" "." `
            "--qa-route=/welcome?revisar=true" `
            "--qa-size=$size" `
            "--qa-layout-report=$report"
        if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $report)) {
            throw "No se generó el informe para $size."
        }
        $metrics = Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
        if ($metrics.horizontalOverflow) {
            throw "$size produjo desbordamiento horizontal: contenido $($metrics.contentWidth), viewport $($metrics.viewportWidth)."
        }
        Write-Host "OK  $size  viewport $($metrics.viewportWidth)x$($metrics.viewportHeight)  sin desbordamiento horizontal"
    }
}
finally {
    Pop-Location
    $env:ELECTRON_RUN_AS_NODE = $previousElectronMode
}

Write-Host ""
Write-Host "La prueba automatizada valida el layout. Para completar la matriz oficial,"
Write-Host "cambia también resolución y escala en Configuración > Sistema > Pantalla:"
Write-Host "1366x720 100%, 1366x768 100%, 1920x1080 125%, 2560x1440 100% y 3840x2160 150%."

