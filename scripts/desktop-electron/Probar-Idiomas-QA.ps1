[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$root = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$desktop = Join-Path $root "desktop-electron"
$reportRoot = Join-Path $root "docs\publishing\reports\languages"
New-Item -ItemType Directory -Path $reportRoot -Force | Out-Null
$previousElectronMode = $env:ELECTRON_RUN_AS_NODE
$routes = @(
    @{ Name = "welcome"; Path = "/welcome?revisar=true" },
    @{ Name = "settings"; Path = "/settings" },
    @{ Name = "resources"; Path = "/resources" },
    @{ Name = "warframes"; Path = "/warframes" },
    @{ Name = "weapons"; Path = "/weapons" },
    @{ Name = "mods"; Path = "/mods" },
    @{ Name = "relics"; Path = "/relics" },
    @{ Name = "worldstate"; Path = "/worldstate" },
    @{ Name = "compare"; Path = "/compare" },
    @{ Name = "privacy"; Path = "/privacy" },
    @{ Name = "support"; Path = "/support" },
    @{ Name = "search"; Path = "/search" }
)

try {
    Push-Location $desktop
    npm run build
    if ($LASTEXITCODE -ne 0) { throw "OW-Electron build failed." }
    $env:ELECTRON_RUN_AS_NODE = $null

    foreach ($route in $routes) {
        $report = Join-Path $reportRoot "$($route.Name)-en.json"
        & ".\node_modules\.bin\ow-electron.cmd" "." `
            "--qa-route=$($route.Path)" "--qa-language=en" `
            "--qa-size=1366x768" "--qa-wait=5500" `
            "--qa-layout-report=$report"
        if ($LASTEXITCODE -ne 0) { throw "Could not test $($route.Path)." }
        $data = Get-Content -LiteralPath $report -Raw -Encoding UTF8 | ConvertFrom-Json
        if ($data.language -ne "en") { throw "$($route.Path) did not activate English." }
        if ($data.horizontalOverflow) { throw "$($route.Path) has horizontal overflow." }
        $remaining = @($data.spanishTextSamples | Where-Object { $_ -ne "Español" })
        if ($remaining.Count -gt 0) {
            Write-Warning "$($route.Path): remaining Spanish -> $($remaining -join ' | ')"
        }
        else {
            Write-Host "OK  EN  $($route.Path)"
        }
    }

    $spanishReport = Join-Path $reportRoot "welcome-es.json"
    & ".\node_modules\.bin\ow-electron.cmd" "." `
        "--qa-route=/welcome?revisar=true" "--qa-language=es" `
        "--qa-size=1366x768" "--qa-wait=4500" `
        "--qa-layout-report=$spanishReport"
    $spanish = Get-Content -LiteralPath $spanishReport -Raw -Encoding UTF8 | ConvertFrom-Json
    if ($spanish.language -ne "es" -or $spanish.spanishTextSamples.Count -eq 0) {
        throw "Spanish package smoke test failed."
    }
    Write-Host "OK  ES  /welcome"

    # Leave the QA profile in the same language as a fresh public installation.
    & ".\node_modules\.bin\ow-electron.cmd" "." `
        "--qa-route=/welcome?revisar=true" "--qa-language=en" `
        "--qa-size=1366x768" "--qa-wait=2000" `
        "--qa-layout-report=$(Join-Path $reportRoot 'final-en.json')"
}
finally {
    Pop-Location
    $env:ELECTRON_RUN_AS_NODE = $previousElectronMode
}

