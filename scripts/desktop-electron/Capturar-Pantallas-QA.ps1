[CmdletBinding()]
param(
    [string]$OutputDirectory = "docs\publishing\screenshots"
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$desktopRoot = Join-Path $repositoryRoot "desktop-electron"
$targetRoot = Join-Path $repositoryRoot $OutputDirectory
$previousElectronMode = $env:ELECTRON_RUN_AS_NODE

$captures = @(
    @{ File = "01-bienvenida.jpg"; Route = "/welcome?revisar=true"; Wait = 4000 },
    @{ File = "02-captura-segura.jpg"; Route = "/welcome?paso=1"; Wait = 4000 },
    @{ File = "03-privacidad-local.jpg"; Route = "/welcome?paso=2"; Wait = 4000 },
    @{ File = "04-perfil-local.jpg"; Route = "/welcome?paso=3"; Wait = 4000 },
    @{ File = "05-politica-privacidad.jpg"; Route = "/privacy"; Wait = 4000 },
    @{ File = "06-catalogo-warframes.jpg"; Route = "/warframes"; Wait = 22000 },
    @{ File = "07-reliquias.jpg"; Route = "/relics"; Wait = 22000 },
    @{ File = "08-recursos.jpg"; Route = "/resources"; Wait = 8000 }
)

New-Item -ItemType Directory -Path $targetRoot -Force | Out-Null

try {
    Push-Location $desktopRoot
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "Falló la compilación OW-Electron." }

    $env:ELECTRON_RUN_AS_NODE = $null
    foreach ($capture in $captures) {
        $target = Join-Path $targetRoot $capture.File
        & ".\node_modules\.bin\ow-electron.cmd" "." `
            "--qa-route=$($capture.Route)" `
            "--qa-language=en" `
            "--qa-size=1200x675" `
            "--qa-wait=$($capture.Wait)" `
            "--qa-screenshot=$target"
        if ($LASTEXITCODE -ne 0) {
            throw "No se pudo generar $($capture.File)."
        }
    }
}
finally {
    Pop-Location
    $env:ELECTRON_RUN_AS_NODE = $previousElectronMode
}

Add-Type -AssemblyName System.Drawing
foreach ($capture in $captures) {
    $target = Join-Path $targetRoot $capture.File
    $image = [System.Drawing.Image]::FromFile($target)
    try {
        if ($image.Width -ne 1200 -or $image.Height -ne 675) {
            throw "$($capture.File) no mide 1200x675."
        }
        $length = (Get-Item -LiteralPath $target).Length
        if ($length -gt 102400) {
            throw "$($capture.File) supera 100 KB ($length bytes)."
        }
        Write-Host "OK  $($capture.File)  1200x675  $length bytes"
    }
    finally {
        $image.Dispose()
    }
}
