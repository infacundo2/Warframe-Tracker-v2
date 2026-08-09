$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$repositoryRoot = Split-Path -Parent $projectRoot
$dist = Join-Path $projectRoot 'dist'
$outputDirectory = Join-Path $repositoryRoot 'out\overwolf-native'
$zipPath = Join-Path $outputDirectory 'Warframe-Tracker-Native-0.1.0.zip'
$opkPath = Join-Path $outputDirectory 'Warframe-Tracker-Native-0.1.0.opk'

if (-not (Test-Path -LiteralPath (Join-Path $dist 'manifest.json'))) {
    throw 'Run npm run build before packaging.'
}
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null
if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force }
if (Test-Path -LiteralPath $opkPath) { Remove-Item -LiteralPath $opkPath -Force }
Compress-Archive -Path (Join-Path $dist '*') -DestinationPath $zipPath -CompressionLevel Optimal
Move-Item -LiteralPath $zipPath -Destination $opkPath
Write-Host "OPK created: $opkPath"
Get-FileHash -Algorithm SHA256 -LiteralPath $opkPath | Format-List Path, Hash
