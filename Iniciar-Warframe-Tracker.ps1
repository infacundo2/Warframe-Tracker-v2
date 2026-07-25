param(
    [int]$Puerto = 8080,
    [switch]$NoAbrirNavegador
)

$ErrorActionPreference = "Stop"

$raizProyecto = $PSScriptRoot
$archivoProyecto = Join-Path $raizProyecto "WarframeInventory\WarframeInventory\WarframeInventory.csproj"
$direccion = "http://localhost:$Puerto"

if (-not (Test-Path -LiteralPath $archivoProyecto)) {
    Write-Error "No se encontró el proyecto en: $archivoProyecto"
    exit 1
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "No se encontró .NET. Instala el SDK de .NET 8 y vuelve a intentarlo."
    exit 1
}

Set-Location -LiteralPath $raizProyecto

Write-Host ""
Write-Host "==========================================" -ForegroundColor DarkCyan
Write-Host "  WARFRAME TRACKER - TERMINAL DE ARRANQUE" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor DarkCyan
Write-Host ""
Write-Host "Dirección: $direccion" -ForegroundColor Yellow
Write-Host "Para detener la página, presiona Ctrl + C." -ForegroundColor DarkGray
Write-Host ""

$trabajoNavegador = $null

if (-not $NoAbrirNavegador) {
    $trabajoNavegador = Start-Job -ArgumentList $direccion -ScriptBlock {
        param($url)

        for ($intento = 0; $intento -lt 120; $intento++) {
            try {
                $respuesta = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 2
                if ($respuesta.StatusCode -eq 200) {
                    Start-Process $url
                    return
                }
            }
            catch {
                Start-Sleep -Milliseconds 500
            }
        }
    }
}

try {
    dotnet run --project $archivoProyecto --urls $direccion
}
finally {
    if ($null -ne $trabajoNavegador) {
        Stop-Job -Job $trabajoNavegador -ErrorAction SilentlyContinue
        Remove-Job -Job $trabajoNavegador -Force -ErrorAction SilentlyContinue
    }
}
