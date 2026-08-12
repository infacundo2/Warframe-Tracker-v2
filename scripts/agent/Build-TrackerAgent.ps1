param(
    [string]$OutputDirectory = "",
    [switch]$FrameworkDependent
)

$ErrorActionPreference = "Stop"
$repository = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$project = Join-Path $repository "WarframeTracker.Agent\WarframeTracker.Agent.csproj"
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $repository "out\tracker-agent"
}
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)
$selfContained = if ($FrameworkDependent) { "false" } else { "true" }

dotnet publish $project -c Release -r win-x64 --self-contained $selfContained `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $OutputDirectory
if ($LASTEXITCODE -ne 0) { throw "No se pudo publicar TrackerAgent." }

Write-Host "TrackerAgent publicado correctamente:" -ForegroundColor Cyan
Write-Host (Join-Path $OutputDirectory "WarframeTracker.Agent.exe")
Write-Host "La configuración permanece en trackeragentsettings.json junto al ejecutable."
