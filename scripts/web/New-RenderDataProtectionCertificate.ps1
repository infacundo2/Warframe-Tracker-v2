param(
    [string]$OutputDirectory = (Join-Path $PSScriptRoot "..\..\.tools\render-secrets"),
    [int]$ValidYears = 5
)

$ErrorActionPreference = "Stop"

if ($ValidYears -lt 1) {
    throw "ValidYears debe ser al menos 1."
}

$password = Read-Host "Contrasena para proteger el certificado PFX" -AsSecureString
$credential = [System.Net.NetworkCredential]::new("certificate", $password)
if ([string]::IsNullOrWhiteSpace($credential.Password)) {
    throw "La contrasena del certificado no puede estar vacia."
}

$resolvedOutput = [System.IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $resolvedOutput -Force | Out-Null
$pfxPath = Join-Path $resolvedOutput "warframe-tracker-data-protection.pfx"
$base64Path = Join-Path $resolvedOutput "warframe-tracker-data-protection.base64.txt"

$certificate = New-SelfSignedCertificate `
    -Subject "CN=Warframe Tracker Data Protection" `
    -CertStoreLocation "Cert:\CurrentUser\My" `
    -KeyAlgorithm RSA `
    -KeyLength 3072 `
    -KeyExportPolicy Exportable `
    -KeyUsage KeyEncipherment, DataEncipherment `
    -NotAfter (Get-Date).AddYears($ValidYears)

try {
    Export-PfxCertificate `
        -Cert $certificate `
        -FilePath $pfxPath `
        -Password $password | Out-Null
    [Convert]::ToBase64String(
        [System.IO.File]::ReadAllBytes($pfxPath)) | `
        Set-Content -LiteralPath $base64Path -Encoding ascii
}
finally {
    Remove-Item -LiteralPath $certificate.PSPath -Force -ErrorAction SilentlyContinue
    $credential.Password = ""
}

Write-Host "Certificado generado localmente:" -ForegroundColor Green
Write-Host "  PFX: $pfxPath"
Write-Host "  Base64 para WARFRAME_TRACKER_DP_CERT_BASE64: $base64Path"
Write-Host "Usa como WARFRAME_TRACKER_DP_CERT_PASSWORD la contrasena que acabas de ingresar."
Write-Host "No subas ninguno de estos archivos a Git."
