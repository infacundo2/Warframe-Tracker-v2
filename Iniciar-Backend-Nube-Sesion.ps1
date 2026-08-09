param(
    [int]$PuertoTracker = 43127
)

$ErrorActionPreference = "Stop"
$raizProyecto = $PSScriptRoot
$archivoProyecto = Join-Path $raizProyecto "WarframeInventory\WarframeInventory\WarframeInventory.csproj"

if (-not (Test-Path -LiteralPath $archivoProyecto)) {
    throw "No se encontro el proyecto del Tracker."
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "No se encontro .NET SDK 8."
}

Write-Host ""
Write-Host "WARFRAME TRACKER - CONEXION DE NUBE TEMPORAL" -ForegroundColor Cyan
Write-Host "Los datos se guardan solo en este proceso y se eliminan al cerrarlo." -ForegroundColor DarkGray
Write-Host ""

$servidor = Read-Host "Servidor MySQL"
$puertoBase = Read-Host "Puerto MySQL [3306]"
if ([string]::IsNullOrWhiteSpace($puertoBase)) {
    $puertoBase = "3306"
}
$baseDatos = Read-Host "Nombre de la base de datos"
$credencial = Get-Credential -Message "Ingresa el usuario y la contrasena de MySQL (solo para esta sesion)"

if ([string]::IsNullOrWhiteSpace($servidor) -or
    [string]::IsNullOrWhiteSpace($baseDatos) -or
    $null -eq $credencial -or
    [string]::IsNullOrWhiteSpace($credencial.UserName)) {
    throw "La conexion fue cancelada o tiene campos vacios."
}

$contrasenaTemporal = $credencial.GetNetworkCredential().Password

try {
    $env:WARFRAME_TRACKER_DESKTOP = "1"
    $env:WARFRAME_TRACKER_DATABASE_PROVIDER = "MySql"
    $env:WARFRAME_TRACKER_URL = "http://127.0.0.1:$PuertoTracker"
    $env:WARFRAME_TRACKER_DB_HOST = $servidor
    $env:WARFRAME_TRACKER_DB_PORT = $puertoBase
    $env:WARFRAME_TRACKER_DB_USER = $credencial.UserName
    $env:WARFRAME_TRACKER_DB_PASS = $contrasenaTemporal
    $env:WARFRAME_TRACKER_DB_NAME = $baseDatos

    Write-Host ""
    Write-Host "Iniciando en http://127.0.0.1:$PuertoTracker" -ForegroundColor Green
    Write-Host "Presiona Ctrl+C para cerrar y borrar la conexion de esta sesion." -ForegroundColor Yellow
    Write-Host ""

    Set-Location -LiteralPath $raizProyecto
    dotnet run --project $archivoProyecto -c Release --no-launch-profile
}
finally {
    @(
        "WARFRAME_TRACKER_DESKTOP",
        "WARFRAME_TRACKER_DATABASE_PROVIDER",
        "WARFRAME_TRACKER_URL",
        "WARFRAME_TRACKER_DB_HOST",
        "WARFRAME_TRACKER_DB_PORT",
        "WARFRAME_TRACKER_DB_USER",
        "WARFRAME_TRACKER_DB_PASS",
        "WARFRAME_TRACKER_DB_NAME"
    ) | ForEach-Object {
        Remove-Item -LiteralPath "Env:$_" -ErrorAction SilentlyContinue
    }

    $contrasenaTemporal = $null
    $credencial = $null
    Write-Host "La conexion temporal fue eliminada de la sesion." -ForegroundColor DarkGray
}
