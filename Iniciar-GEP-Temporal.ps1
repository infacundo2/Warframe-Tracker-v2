[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$desktopDirectory = Join-Path $PSScriptRoot "desktop"
$packageFile = Join-Path $desktopDirectory "package.json"

if (-not (Test-Path -LiteralPath $packageFile)) {
    throw "No se encontró el proyecto de escritorio en $desktopDirectory."
}
if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    throw "No se encontró npm. Instala Node.js 22 o posterior."
}

Write-Host "Warframe Tracker // prueba GEP temporal" -ForegroundColor Cyan
Write-Host "La clave se mantendrá solo en la memoria de este proceso y no se guardará." -ForegroundColor DarkGray
$secureKey = Read-Host "Pega tu OW_DEV_KEY temporal" -AsSecureString
$keyPointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secureKey)

try {
    $env:OW_DEV_KEY = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($keyPointer)
    if ([string]::IsNullOrWhiteSpace($env:OW_DEV_KEY)) {
        throw "La clave no puede estar vacía."
    }

    Push-Location -LiteralPath $desktopDirectory
    try {
        npm run start:dev-gep
    }
    finally {
        Pop-Location
    }
}
finally {
    Remove-Item Env:OW_DEV_KEY -ErrorAction SilentlyContinue
    [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($keyPointer)
}
