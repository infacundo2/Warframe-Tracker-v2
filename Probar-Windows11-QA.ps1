[CmdletBinding()]
param(
    [switch]$BuildInstaller,
    [switch]$ScanInstaller
)

$ErrorActionPreference = "Stop"
$repositoryRoot = $PSScriptRoot
$reportDirectory = Join-Path $repositoryRoot "docs\publishing\reports"
$reportPath = Join-Path $reportDirectory "windows11-qa.txt"
New-Item -ItemType Directory -Path $reportDirectory -Force | Out-Null

$lines = [System.Collections.Generic.List[string]]::new()
function Add-Result([string]$Text) {
    $lines.Add($Text)
    Write-Host $Text
}

$os = Get-CimInstance Win32_OperatingSystem
$currentVersion = Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion"
$isWindows11 = [int]$os.BuildNumber -ge 22000
Add-Result "Warframe Tracker - comprobación Windows 11"
Add-Result "Fecha: $([DateTimeOffset]::Now.ToString('u'))"
Add-Result "Sistema: Windows $($currentVersion.DisplayVersion), build $($os.BuildNumber), $($os.OSArchitecture)"
Add-Result "Windows 11: $(if ($isWindows11) { 'APROBADO' } else { 'FALLÓ' })"
if (-not $isWindows11) { throw "Esta máquina no ejecuta Windows 11." }

Add-Type -AssemblyName System.Windows.Forms
foreach ($display in [System.Windows.Forms.Screen]::AllScreens) {
    Add-Result "Pantalla: $($display.Bounds.Width)x$($display.Bounds.Height); área útil $($display.WorkingArea.Width)x$($display.WorkingArea.Height); principal=$($display.Primary)"
}

Push-Location $repositoryRoot
try {
    dotnet test "Warframe-Tracker-v2.sln" -c Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw "Fallaron las pruebas .NET." }
    Add-Result "Pruebas .NET: APROBADO"

    Push-Location "desktop"
    try {
        npm run typecheck
        if ($LASTEXITCODE -ne 0) { throw "Falló TypeScript." }
        npm run build
        if ($LASTEXITCODE -ne 0) { throw "Falló el cliente OW-Electron." }
        Add-Result "TypeScript y OW-Electron: APROBADO"

        if ($BuildInstaller) {
            npm run package
            if ($LASTEXITCODE -ne 0) { throw "Falló el instalador." }
            Add-Result "Generación del instalador: APROBADO"
        }
    }
    finally {
        Pop-Location
    }

    $installer = Get-ChildItem (Join-Path $repositoryRoot "out\desktop-installer") `
        -Filter "Warframe-Tracker-Setup-*.exe" -File -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($installer) {
        $signature = Get-AuthenticodeSignature -LiteralPath $installer.FullName
        Add-Result "Instalador: $($installer.Name)"
        Add-Result "Firma Authenticode: $($signature.Status)"
        Add-Result "SHA-256: $((Get-FileHash -LiteralPath $installer.FullName -Algorithm SHA256).Hash)"

        if ($ScanInstaller) {
            $defender = Get-MpComputerStatus
            Add-Result "Microsoft Defender activo: $($defender.AntivirusEnabled)"
            Add-Result "Firmas Defender: $($defender.AntivirusSignatureVersion)"
            Start-MpScan -ScanType CustomScan -ScanPath $installer.FullName
            $recentThreat = Get-MpThreatDetection -ErrorAction SilentlyContinue |
                Where-Object { $_.Resources -contains $installer.FullName } |
                Select-Object -First 1
            Add-Result "Análisis Defender: $(if ($recentThreat) { 'DETECCIÓN ENCONTRADA' } else { 'SIN DETECCIONES' })"
        }
    }
    else {
        Add-Result "Instalador: NO ENCONTRADO"
    }
}
finally {
    Pop-Location
    $lines | Set-Content -LiteralPath $reportPath -Encoding utf8
}

Write-Host "Informe guardado en $reportPath"

