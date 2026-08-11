$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$repositoryRoot = Split-Path -Parent $projectRoot
$dist = Join-Path $projectRoot 'dist'
$outputDirectory = Join-Path $repositoryRoot 'out\overwolf-native'
$manifestPath = Join-Path $dist 'manifest.json'

if (-not (Test-Path -LiteralPath $manifestPath)) {
    throw 'Run npm run build before packaging.'
}
$version = (Get-Content -LiteralPath $manifestPath -Raw -Encoding utf8 | ConvertFrom-Json).meta.version
if ([string]::IsNullOrWhiteSpace($version)) { throw 'The manifest does not define meta.version.' }
$zipPath = Join-Path $outputDirectory "Warframe-Tracker-Native-$version.zip"
$opkPath = Join-Path $outputDirectory "Warframe-Tracker-Native-$version.opk"
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
# Keep one distributable OPK only. Older versions remain reproducible from Git tags.
Get-ChildItem -LiteralPath $outputDirectory -Filter 'Warframe-Tracker-Native-*.opk' -File |
    Where-Object FullName -ne $opkPath |
    Remove-Item -Force
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
if (Test-Path -LiteralPath $opkPath) { Remove-Item -LiteralPath $opkPath -Force }
Compress-Archive -Path (Join-Path $dist '*') -DestinationPath $zipPath -CompressionLevel Optimal
Move-Item -LiteralPath $zipPath -Destination $opkPath
Write-Host "OPK created: $opkPath"
Get-FileHash -Algorithm SHA256 -LiteralPath $opkPath | Format-List Path, Hash
