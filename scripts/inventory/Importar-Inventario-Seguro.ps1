[CmdletBinding()]
param(
    [Parameter(Mandatory = $true, Position = 0)]
    [ValidateNotNullOrEmpty()]
    [string]$Archivo,

    [switch]$NoAbrirPagina
)

$ErrorActionPreference = "Stop"
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$projectDirectory = Join-Path $repoRoot "WarframeInventory\WarframeInventory"
$projectFile = Join-Path $projectDirectory "WarframeInventory.csproj"
$inventoryPath = (Resolve-Path -LiteralPath $Archivo).Path

if ([System.IO.Path]::GetExtension($inventoryPath) -ne ".json") {
    throw "El archivo debe tener extensión .json."
}

$fileInfo = Get-Item -LiteralPath $inventoryPath
if ($fileInfo.Length -gt 20MB) {
    throw "El inventario supera el límite seguro de 20 MB."
}

$inventoryJson = (Get-Content -LiteralPath $inventoryPath -Raw -Encoding UTF8).ToString()
try {
    $null = $inventoryJson | ConvertFrom-Json
}
catch {
    throw "El archivo no contiene JSON válido."
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw "Falta .NET 8. Instálalo desde https://dotnet.microsoft.com/download/dotnet/8.0"
}

Write-Host "Preparando el servidor local seguro..." -ForegroundColor Cyan
dotnet build $projectFile -c Release --nologo
if ($LASTEXITCODE -ne 0) {
    throw "La aplicación no compiló. Revisa los errores mostrados arriba."
}

$listener = [System.Net.Sockets.TcpListener]::new(
    [System.Net.IPAddress]::Loopback, 0)
$listener.Start()
$port = ([System.Net.IPEndPoint]$listener.LocalEndpoint).Port
$listener.Stop()

$randomBytes = New-Object byte[] 32
$randomGenerator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
$randomGenerator.GetBytes($randomBytes)
$randomGenerator.Dispose()
$bridgeKey = [Convert]::ToBase64String($randomBytes)
$localUrl = "http://127.0.0.1:$port"
$dataDirectory = Join-Path $env:LOCALAPPDATA "WarframeTracker"
$backendDll = Join-Path $projectDirectory "bin\Release\net8.0\WarframeInventory.dll"

$startInfo = [System.Diagnostics.ProcessStartInfo]::new()
$startInfo.FileName = "dotnet"
$startInfo.Arguments = "`"$backendDll`""
$startInfo.WorkingDirectory = $projectDirectory
$startInfo.UseShellExecute = $false
$startInfo.CreateNoWindow = $true
$startInfo.RedirectStandardOutput = $true
$startInfo.RedirectStandardError = $true
$startInfo.EnvironmentVariables["ASPNETCORE_ENVIRONMENT"] = "Production"
$startInfo.EnvironmentVariables["WARFRAME_TRACKER_DESKTOP"] = "1"
$startInfo.EnvironmentVariables["WARFRAME_TRACKER_URL"] = $localUrl
$startInfo.EnvironmentVariables["WARFRAME_TRACKER_DATA_DIR"] = $dataDirectory
$startInfo.EnvironmentVariables["WARFRAME_DESKTOP_BRIDGE_KEY"] = $bridgeKey

$server = [System.Diagnostics.Process]::Start($startInfo)
if ($null -eq $server) {
    throw "No se pudo iniciar el servidor local."
}

$ready = $false
for ($attempt = 0; $attempt -lt 80; $attempt++) {
    Start-Sleep -Milliseconds 250
    if ($server.HasExited) {
        $backendError = $server.StandardError.ReadToEnd()
        throw "El servidor local terminó antes de estar listo. $backendError"
    }
    try {
        $health = Invoke-RestMethod -Uri "$localUrl/api/desktop-bridge/health" `
            -Method Get -TimeoutSec 1
        if ($health.ready) {
            $ready = $true
            break
        }
    }
    catch {
        # El proceso todavía está creando o migrando la base local.
    }
}

if (-not $ready) {
    $server.Kill()
    throw "El servidor local no respondió dentro de 20 segundos."
}

$body = @{
    inventoryJson = $inventoryJson
    source = "powershell-safe-import"
} | ConvertTo-Json -Compress
$headers = @{ "X-Warframe-Bridge-Key" = $bridgeKey }

try {
    $receipt = Invoke-RestMethod -Uri "$localUrl/api/desktop-bridge/inventory" `
        -Method Post -Headers $headers -ContentType "application/json" `
        -Body $body -TimeoutSec 30
}
catch {
    $server.Kill()
    $serverDetail = ""
    if ($null -ne $_.Exception.Response) {
        $responseStream = $_.Exception.Response.GetResponseStream()
        if ($null -ne $responseStream) {
            $reader = New-Object System.IO.StreamReader($responseStream)
            $serverDetail = $reader.ReadToEnd()
            $reader.Dispose()
        }
    }
    throw "El servidor rechazó el inventario: $($_.Exception.Message) $serverDetail"
}

if (-not $NoAbrirPagina) {
    Start-Process "$localUrl/desktop-sync"
}
Write-Host ""
Write-Host "Inventario recibido correctamente." -ForegroundColor Green
Write-Host "Objetos distintos: $($receipt.distinctItems)"
Write-Host "Cobertura completa: $($receipt.isAuthoritative)"
Write-Host "Servidor local: PID $($server.Id)"
Write-Host "Dirección local: $localUrl/desktop-sync"
Write-Host ""
Write-Host "Revisa y confirma los cambios en la página que acaba de abrirse."
Write-Host "El script no lee memoria, archivos privados ni tráfico de Warframe."
