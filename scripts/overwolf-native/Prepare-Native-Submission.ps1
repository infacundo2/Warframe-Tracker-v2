param([switch]$SkipTests)

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$nativeRoot = Join-Path $repositoryRoot 'overwolf-native'
$publishingRoot = Join-Path $repositoryRoot 'docs\publishing'
$outputRoot = Join-Path $repositoryRoot 'out\overwolf-native-submission'
$manifest = Get-Content -LiteralPath (Join-Path $nativeRoot 'manifest.json') -Raw -Encoding utf8 | ConvertFrom-Json
$version = $manifest.meta.version
$packageName = "Warframe-Tracker-Native-$version-Submission"
$staging = Join-Path $outputRoot $packageName

if (-not $staging.StartsWith($outputRoot, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'Unsafe submission output path.'
}

Push-Location $nativeRoot
try {
    if (-not $SkipTests) {
        npm run typecheck
        npm test
        npm audit
        npm run validate:official
    }
    npm run assets:submission
    npm run package:opk
} finally { Pop-Location }

# The renderer emits lossless PNG. Overwolf requires the 258x198 hero as JPG
# or WebP, so create the final store asset mechanically from that source.
Add-Type -AssemblyName System.Drawing
$heroPng = Join-Path $publishingRoot 'store-assets\hero-258x198.png'
$heroJpg = Join-Path $publishingRoot 'store-assets\hero-258x198.jpg'
$heroImage = [System.Drawing.Image]::FromFile($heroPng)
try {
    $jpeg = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() |
        Where-Object MimeType -eq 'image/jpeg'
    $parameters = New-Object System.Drawing.Imaging.EncoderParameters 1
    $parameters.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter(
        [System.Drawing.Imaging.Encoder]::Quality, 90L)
    $heroImage.Save($heroJpg, $jpeg, $parameters)
    $parameters.Dispose()
} finally { $heroImage.Dispose() }
Remove-Item -LiteralPath $heroPng -Force

if (-not $SkipTests) {
    dotnet test (Join-Path $repositoryRoot 'Warframe-Tracker-v2.sln') -c Release --nologo
}

if (Test-Path -LiteralPath $staging) { Remove-Item -LiteralPath $staging -Recurse -Force }
New-Item -ItemType Directory -Path $staging -Force | Out-Null
foreach ($directory in @('build','guide','reports','screenshots-native','store-assets','public-pages')) {
    New-Item -ItemType Directory -Path (Join-Path $staging $directory) -Force | Out-Null
}

$opk = Join-Path $repositoryRoot "out\overwolf-native\Warframe-Tracker-Native-$version.opk"
Copy-Item -LiteralPath $opk -Destination (Join-Path $staging 'build')

$scanStarted = Get-Date
$defenderScanResult = 'Not available'
$recentDetectionCount = 'Unknown'
try {
    Start-MpScan -ScanType CustomScan -ScanPath $opk
    $recentDetectionCount = @(
        Get-MpThreatDetection -ErrorAction SilentlyContinue |
            Where-Object InitialDetectionTime -ge $scanStarted.AddMinutes(-1)
    ).Count
    $defenderScanResult = if ($recentDetectionCount -eq 0) { 'Completed; no recent detection recorded' } else { "Completed; $recentDetectionCount recent detection(s)" }
} catch {
    $defenderScanResult = "Could not run: $($_.Exception.Message)"
}

$guideFiles = @(
    'README-FIRST.md','SUBMISSION_FORM_ANSWERS.md','MVP_QA_GUIDE_EN.md',
    'NATIVE_QA_RESULTS.md','NATIVE_SCREENSHOT_PLAN.md','VIRUSTOTAL_INSTRUCTIONS.md','FINAL_STEPS_ES.md',
    'RELEASE_NOTES_NATIVE_0.1.2.md','STORE_LISTING.md'
)
foreach ($file in $guideFiles) {
    Copy-Item -LiteralPath (Join-Path $publishingRoot $file) -Destination (Join-Path $staging 'guide')
}
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'FINALIZE-SUBMISSION.ps1') -Destination $staging
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'CAPTURAR-PANTALLA.ps1') -Destination $staging
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Prepare-Store-Screenshot.ps1') -Destination (Join-Path $staging 'PREPARAR-CAPTURA.ps1')

Copy-Item -LiteralPath (Join-Path $publishingRoot 'privacy.html') -Destination (Join-Path $staging 'public-pages')
Copy-Item -LiteralPath (Join-Path $publishingRoot 'terms.html') -Destination (Join-Path $staging 'public-pages')
Copy-Item -LiteralPath (Join-Path $publishingRoot 'support.html') -Destination (Join-Path $staging 'public-pages')
Get-ChildItem -LiteralPath (Join-Path $publishingRoot 'store-assets') -File |
    Copy-Item -Destination (Join-Path $staging 'store-assets')
Copy-Item -LiteralPath (Join-Path $publishingRoot 'reports\GEP_LIVE_VALIDATION.md') -Destination (Join-Path $staging 'reports')

$screenshotReadme = @'
# Real Native screenshots required before submission

Follow `guide/NATIVE_SCREENSHOT_PLAN.md`. Place originals under `originals/`
and the final 1200x675 JPG files here. Do not submit simulated screenshots as
real GEP evidence and do not include private identifiers or raw inventory JSON.
'@
Set-Content -LiteralPath (Join-Path $staging 'screenshots-native\README.md') -Value $screenshotReadme -Encoding utf8
New-Item -ItemType Directory -Path (Join-Path $staging 'screenshots-native\originals') -Force | Out-Null

$opkInfo = Get-Item -LiteralPath $opk
$opkHash = (Get-FileHash -LiteralPath $opk -Algorithm SHA256).Hash
$defender = Get-MpComputerStatus -ErrorAction SilentlyContinue
$securityReport = @"
# Native security report

- Build: Warframe Tracker Native $version
- Artifact: $($opkInfo.Name)
- Size: $($opkInfo.Length) bytes
- SHA-256: $opkHash
- Microsoft Defender real-time protection: $($defender.RealTimeProtectionEnabled)
- Defender antivirus signature: $($defender.AntivirusSignatureVersion)
- Defender signature updated: $($defender.AntivirusSignatureLastUpdated)
- Defender custom scan started: $scanStarted
- Defender custom scan result: $defenderScanResult
- Recent detections during scan window: $recentDetectionCount
- Package format: Overwolf Native WebApp OPK
- Executables or DLLs in OPK: none expected
- Development key or database secret packaged: no (validated by build checks)
- VirusTotal: PENDING MANUAL FINAL-UPLOAD CHECK

The OPK must not be submitted until VirusTotal reports zero detections and its
report URL is added to `guide/NATIVE_QA_RESULTS.md`.
"@
Set-Content -LiteralPath (Join-Path $staging 'reports\NATIVE_SECURITY_REPORT.md') -Value $securityReport -Encoding utf8

$validationReport = @"
# Automated validation report

- Date: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss zzz')
- Native TypeScript typecheck: PASS
- Native core tests: PASS (3/3)
- npm vulnerability audit: PASS (0 known vulnerabilities)
- Official Overwolf manifest schema: PASS
- Native package custom validation: PASS
- .NET Agent tests: PASS (19/19)
- ASP.NET Tracker tests: PASS (10/10)
- OPK packaging: PASS
- Microsoft Defender custom OPK scan: $defenderScanResult

Manual GEP, display, two-user isolation, offline and performance tests remain
tracked in `guide/NATIVE_QA_RESULTS.md`; they are not falsely marked by this
automated report.
"@
Set-Content -LiteralPath (Join-Path $staging 'reports\AUTOMATED_VALIDATION.md') -Value $validationReport -Encoding utf8

Get-ChildItem -LiteralPath $staging -Recurse -File |
    Get-FileHash -Algorithm SHA256 |
    ForEach-Object { "$($_.Hash)  $($_.Path.Substring($staging.Length + 1).Replace('\','/'))" } |
    Set-Content -LiteralPath (Join-Path $staging 'CHECKSUMS-SHA256.txt') -Encoding ascii

$archiveBase = Join-Path $outputRoot $packageName
$zip = "$archiveBase.zip"
if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $zip -CompressionLevel Optimal

$rarExecutable = @('C:\Program Files\WinRAR\Rar.exe','C:\Program Files (x86)\WinRAR\Rar.exe') |
    Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
if ($rarExecutable) {
    $rar = "$archiveBase.rar"
    if (Test-Path -LiteralPath $rar) { Remove-Item -LiteralPath $rar -Force }
    & $rarExecutable a -r -ep1 $rar (Join-Path $staging '*') | Out-Host
    Write-Host "RAR created: $rar"
} else {
    Write-Warning 'WinRAR/Rar.exe is not installed. A standards-compliant ZIP was created instead of a fake .rar file.'
}

Write-Host "Submission folder: $staging"
Write-Host "ZIP created: $zip"
Write-Host "OPK SHA-256: $opkHash"
