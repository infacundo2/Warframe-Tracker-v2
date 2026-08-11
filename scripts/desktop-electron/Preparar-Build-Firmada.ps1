[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$CertificatePfx
)

$ErrorActionPreference = "Stop"
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$certificatePath = (Resolve-Path -LiteralPath $CertificatePfx).Path
$requiredOverwolfVariables = @("OW_CLI_EMAIL", "OW_CLI_API_KEY", "OW_BUILD_KEY")
$missing = $requiredOverwolfVariables | Where-Object {
    [string]::IsNullOrWhiteSpace([Environment]::GetEnvironmentVariable($_, "Process"))
}
if ($missing.Count -gt 0) {
    throw "Faltan credenciales de Overwolf en esta sesión: $($missing -join ', ')."
}

$securePassword = Read-Host "Contraseña del certificado PFX" -AsSecureString
$pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($securePassword)
$plainPassword = $null
try {
    $plainPassword = [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    $certificate = [Security.Cryptography.X509Certificates.X509Certificate2]::new(
        $certificatePath,
        $plainPassword,
        [Security.Cryptography.X509Certificates.X509KeyStorageFlags]::EphemeralKeySet)

    if (-not $certificate.HasPrivateKey) { throw "El PFX no contiene la clave privada." }
    if ($certificate.NotAfter -le [DateTime]::Now) { throw "El certificado está vencido." }
    $codeSigningOid = "1.3.6.1.5.5.7.3.3"
    $canSignCode = $certificate.Extensions |
        Where-Object { $_ -is [Security.Cryptography.X509Certificates.X509EnhancedKeyUsageExtension] } |
        ForEach-Object { $_.EnhancedKeyUsages } |
        Where-Object { $_.Value -eq $codeSigningOid }
    if (-not $canSignCode) { throw "El certificado no permite firma de código." }

    $env:CSC_LINK = $certificatePath
    $env:CSC_KEY_PASSWORD = $plainPassword
    Push-Location (Join-Path $repositoryRoot "desktop-electron")
    try {
        npm run package
        if ($LASTEXITCODE -ne 0) { throw "Falló la construcción firmada." }
    }
    finally {
        Pop-Location
    }

    $installer = Get-ChildItem (Join-Path $repositoryRoot "out\desktop-installer") `
        -Filter "Warframe-Tracker-Setup-*.exe" -File |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    $signature = Get-AuthenticodeSignature -LiteralPath $installer.FullName
    if ($signature.Status -ne "Valid") {
        throw "La firma final no es válida: $($signature.StatusMessage)"
    }
    Write-Host "Build firmada y verificada: $($installer.FullName)"
}
finally {
    if ($pointer -ne [IntPtr]::Zero) {
        [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
    }
    $plainPassword = $null
    Remove-Item Env:CSC_LINK -ErrorAction SilentlyContinue
    Remove-Item Env:CSC_KEY_PASSWORD -ErrorAction SilentlyContinue
}
