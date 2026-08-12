param([Parameter(Mandatory = $true)][string]$VirusTotalUrl)

$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
if (-not (Test-Path -LiteralPath (Join-Path $root 'build\Warframe-Tracker-Native-0.1.2.opk'))) {
    throw 'Run this script from the generated submission folder.'
}
$opkPath = Join-Path $root 'build\Warframe-Tracker-Native-0.1.2.opk'
if ($VirusTotalUrl -notmatch '^https://www\.virustotal\.com/gui/file/([a-fA-F0-9]{64})(?:/|$)') {
    throw 'VirusTotalUrl must be a file report URL containing its SHA-256 hash.'
}
$reportHash = $Matches[1].ToUpperInvariant()
$opkHash = (Get-FileHash -LiteralPath $opkPath -Algorithm SHA256).Hash.ToUpperInvariant()
if ($reportHash -ne $opkHash) {
    throw "The VirusTotal report belongs to $reportHash, but the current OPK is $opkHash. Upload the current OPK and use its report URL."
}

Add-Type -AssemblyName System.Drawing
$requiredScreenshots = @(
    '01-native-window-en.jpg','02-gep-ready-en.jpg','03-inventory-captured-en.jpg',
    '04-preview-en.jpg','05-command-center-en.jpg','06-warframes-en.jpg',
    '07-relics-en.jpg','08-goal-planner-en.jpg','09-settings-es.jpg',
    '10-offline-error-en.jpg'
)
$missingScreenshots = @()
foreach ($name in $requiredScreenshots) {
    $path = Join-Path $root "screenshots-native\$name"
    if (-not (Test-Path -LiteralPath $path)) {
        $missingScreenshots += $name
        continue
    }
    $image = [System.Drawing.Image]::FromFile($path)
    try {
        if ($image.Width -ne 1200 -or $image.Height -ne 675) {
            throw "$name must be exactly 1200x675."
        }
    } finally { $image.Dispose() }
    if ((Get-Item -LiteralPath $path).Length -gt 100KB) { throw "$name exceeds 100 KB." }
}
if ($missingScreenshots.Count -gt 0) {
    $list = $missingScreenshots -join [Environment]::NewLine
    throw "Faltan capturas reales. Ejecuta CAPTURAR-PANTALLA.ps1 para cada numero pendiente:$([Environment]::NewLine)$list"
}

$qaPath = Join-Path $root 'guide\NATIVE_QA_RESULTS.md'
$qa = Get-Content -LiteralPath $qaPath -Raw -Encoding utf8
if ($qa -match '\| ___ \|' -or $qa -match '______' -or
    $qa -match '\bNOT TESTED\b' -or $qa -match '\bPARTIAL\b' -or
    $qa -match '\bINCOMPLETE\b' -or $qa -match '\bINVALID FOR CURRENT OPK\b') {
    throw 'NATIVE_QA_RESULTS.md still contains pending, partial, invalid or blank checks. Complete real QA before finalizing.'
}
if (-not (Test-Path -LiteralPath (Join-Path $root 'reports\virustotal-0-detections.png'))) {
    throw 'Missing reports/virustotal-0-detections.png.'
}

$securityPath = Join-Path $root 'reports\NATIVE_SECURITY_REPORT.md'
$security = Get-Content -LiteralPath $securityPath -Raw -Encoding utf8
$security = $security -replace 'VirusTotal: PENDING MANUAL FINAL-UPLOAD CHECK', "VirusTotal: 0 detections - $VirusTotalUrl"
Set-Content -LiteralPath $securityPath -Value $security -Encoding utf8

$qa = $qa -replace 'URL:\s*\|', "URL: $VirusTotalUrl |"
Set-Content -LiteralPath $qaPath -Value $qa -Encoding utf8

$checksumPath = Join-Path $root 'CHECKSUMS-SHA256.txt'
Get-ChildItem -LiteralPath $root -Recurse -File |
    Where-Object FullName -ne $checksumPath |
    Get-FileHash -Algorithm SHA256 |
    ForEach-Object { "$($_.Hash)  $($_.Path.Substring($root.Length + 1).Replace('\','/'))" } |
    Set-Content -LiteralPath $checksumPath -Encoding ascii

$archiveBase = Join-Path (Split-Path -Parent $root) (Split-Path -Leaf $root)
$zip = "$archiveBase.zip"
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $root '*') -DestinationPath $zip -CompressionLevel Optimal

$rarExecutable = @('C:\Program Files\WinRAR\Rar.exe','C:\Program Files (x86)\WinRAR\Rar.exe') |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if ($rarExecutable) {
    $rar = "$archiveBase.rar"
    if (Test-Path -LiteralPath $rar) { Remove-Item -LiteralPath $rar -Force }
    & $rarExecutable a -r -ep1 $rar (Join-Path $root '*') | Out-Host
    Write-Host "Final RAR: $rar"
}
Write-Host "Final ZIP: $zip"
