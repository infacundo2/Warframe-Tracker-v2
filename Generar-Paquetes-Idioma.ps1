[CmdletBinding()]
param(
    [switch]$ReuseExistingEnglish
)

$ErrorActionPreference = "Stop"
$root = $PSScriptRoot
$uiRoot = Join-Path $root "WarframeInventory\WarframeInventory"
$output = Join-Path $uiRoot "wwwroot\i18n"
New-Item -ItemType Directory -Path $output -Force | Out-Null

$files = @(
    Get-ChildItem (Join-Path $uiRoot "Pages") -Recurse -Filter "*.razor"
    Get-Item (Join-Path $uiRoot "App.razor")
    Get-Item (Join-Path $uiRoot "Shared\MainLayout.razor")
)

$strings = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
function Add-Phrase([string]$value) {
    if ([string]::IsNullOrWhiteSpace($value)) { return }
    $decoded = [Net.WebUtility]::HtmlDecode($value)
    $normalized = [regex]::Replace($decoded, '\s+', ' ').Trim()
    if ($normalized.Length -lt 2 -or $normalized.Length -gt 500) { return }
    if ($normalized -match '^[@/#.]' -or $normalized -match '^(https?:|[a-z]+/[a-z]+)') { return }
    if ($normalized -match '[@{};]' -or $normalized -match '\b(private|foreach|if|else|await|return)\b') { return }
    if ($normalized -notmatch '[A-Za-zÁÉÍÓÚÑÜáéíóúñü¿¡]') { return }
    [void]$strings.Add($normalized)
}

foreach ($file in $files) {
    $content = Get-Content -LiteralPath $file.FullName -Raw -Encoding UTF8
    foreach ($match in [regex]::Matches($content, '>(?<text>[^<>]+)<')) {
        $text = $match.Groups['text'].Value
        if ($text -notmatch '^\s*@' -and $text -notmatch '@\(') { Add-Phrase $text }
    }
    foreach ($match in [regex]::Matches(
        $content,
        '(?i)(?:Label|Placeholder|Title|AriaLabel|aria-label|title)\s*=\s*"(?<text>[^"]+)"')) {
        $text = $match.Groups['text'].Value
        if ($text -notmatch '^@') { Add-Phrase $text }
    }
    foreach ($match in [regex]::Matches($content, '"(?<text>[^"\r\n]+)"')) {
        $text = $match.Groups['text'].Value
        if ($text -match '\s' -or $text -match '[ÁÉÍÓÚÑÜáéíóúñü¿¡]') { Add-Phrase $text }
    }
}

$manualEnglish = [ordered]@{
    "Warframe Registro" = "Warframe Tracker"
    "REGISTRO" = "TRACKER"
    "Centro de mando" = "Command Center"
    "Armas" = "Weapons"
    "Recursos" = "Resources"
    "Reliquias" = "Relics"
    "Comparador" = "Compare"
    "Ajustes" = "Settings"
    "Objetivos" = "Goals"
    "Construibles" = "Buildable"
    "Privacidad" = "Privacy"
    "Soporte" = "Support"
    "Buscar" = "Search"
    "Salir" = "Sign out"
    "Ingresar" = "Sign in"
    "Crear cuenta" = "Create account"
    "Crear perfil local" = "Create local profile"
    "Ya tengo un perfil" = "I already have a profile"
    "Anterior" = "Back"
    "Siguiente" = "Next"
    "Omitir tutorial" = "Skip tutorial"
    "Disponible" = "Available"
    "En bóveda" = "Vaulted"
    "Todas" = "All"
    "Todos" = "All"
    "Poseídos" = "Owned"
    "Faltantes" = "Missing"
    "Sin datos" = "No data"
    "Sin datos." = "No data."
    "A UNA PIEZA" = "ONE PIECE AWAY"
    "Ver detalle" = "View details"
    "Abrir detalle" = "Open details"
    "Guardar" = "Save"
    "Cancelar" = "Cancel"
    "Eliminar" = "Delete"
    "Editar" = "Edit"
    "Cantidad" = "Quantity"
    "Probabilidad" = "Drop chance"
    "Rareza" = "Rarity"
    "Común" = "Common"
    "Poco común" = "Uncommon"
    "Raro" = "Rare"
    "Intacta" = "Intact"
    "Excepcional" = "Exceptional"
    "Perfecta" = "Flawless"
    "Radiante" = "Radiant"
    "Planetario" = "Planetary"
    "Drop específico" = "Specific drop"
    "Especial" = "Special"
    "Enemigo" = "Enemy"
    "Mejor fuente:" = "Best source:"
    "Sin ubicación normalizada en el catálogo." = "No standardized location in the catalog."
    ". Esta acción es irreversible y elimina perfiles, inventario, objetivos y preferencias guardadas únicamente en ese PC." = ". This action is irreversible and deletes profiles, inventory, goals, and preferences stored only on that PC."
    "NAVEGACIÓN" = "NAVIGATION"
    "VENTANA" = "WINDOW"
    "Inventario automático" = "Automatic inventory"
    "Sincronizar cuenta" = "Sync account"
    "Transferir inventario" = "Transfer inventory"
    "Editor e historial" = "Editor and history"
}
foreach ($key in $manualEnglish.Keys) { [void]$strings.Add($key) }

$spanishTranslations = [ordered]@{}
foreach ($phrase in ($strings | Sort-Object)) { $spanishTranslations[$phrase] = $phrase }

$englishTranslations = [ordered]@{}
$existingPath = Join-Path $output "en.json"
if ($ReuseExistingEnglish -and (Test-Path $existingPath)) {
    $existing = Get-Content -LiteralPath $existingPath -Raw | ConvertFrom-Json
    foreach ($property in $existing.translations.PSObject.Properties) {
        $englishTranslations[$property.Name] = [string]$property.Value
    }
}

$pending = @($strings | Where-Object { -not $englishTranslations.Contains($_) } | Sort-Object)
$separator = "`n###WF-STRING###`n"
for ($offset = 0; $offset -lt $pending.Count; $offset += 18) {
    $batch = @($pending[$offset..([Math]::Min($pending.Count - 1, $offset + 17))])
    $query = $batch -join $separator
    $uri = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=es&tl=en&dt=t&q=$([uri]::EscapeDataString($query))"
    $response = Invoke-RestMethod -Uri $uri
    $translated = (($response[0] | ForEach-Object { $_[0] }) -join '') -split [regex]::Escape($separator)
    if ($translated.Count -ne $batch.Count) {
        throw "La traducción por lotes devolvió $($translated.Count) frases para $($batch.Count) entradas."
    }
    for ($index = 0; $index -lt $batch.Count; $index++) {
        $englishTranslations[$batch[$index]] = $translated[$index].Trim()
    }
    Start-Sleep -Milliseconds 100
}

foreach ($key in $manualEnglish.Keys) { $englishTranslations[$key] = $manualEnglish[$key] }
$orderedEnglish = [ordered]@{}
foreach ($key in ($englishTranslations.Keys | Sort-Object)) { $orderedEnglish[$key] = $englishTranslations[$key] }

$patternsEs = @()
$patternsEn = @(
    [ordered]@{ pattern = '^Paso (\d+) de (\d+)$'; replacement = 'Step `$1 of `$2' },
    [ordered]@{ pattern = '^(\d+) componentes pendientes$'; replacement = '`$1 components remaining' },
    [ordered]@{ pattern = '^(\d+) copias$'; replacement = '`$1 copies' },
    [ordered]@{ pattern = '^Total (\d+)$'; replacement = 'Total `$1' },
    [ordered]@{ pattern = '^Falta: (.+)$'; replacement = 'Missing: `$1' }
)
$segmentsEs = @()
$segmentsEn = @(
    [ordered]@{ source = 'Inventario aplicado correctamente:'; target = 'Inventory applied successfully:' },
    [ordered]@{ source = 'registros modificados.'; target = 'records changed.' },
    [ordered]@{ source = 'objetivos de mods creados.'; target = 'mod goals created.' },
    [ordered]@{ source = 'entradas combinadas correctamente.'; target = 'entries merged successfully.' },
    [ordered]@{ source = 'componentes pendientes'; target = 'components remaining' },
    [ordered]@{ source = 'copias'; target = 'copies' },
    [ordered]@{ source = 'vestigios'; target = 'traces' },
    [ordered]@{ source = 'aperturas'; target = 'openings' },
    [ordered]@{ source = 'Mejor fuente:'; target = 'Best source:' },
    [ordered]@{ source = 'Rotación'; target = 'Rotation' },
    [ordered]@{ source = 'Fuente destacada de'; target = 'Highlighted source for' },
    [ordered]@{ source = 'Fuente regional de'; target = 'Regional source for' },
    [ordered]@{ source = 'la tasa exacta no está publicada'; target = 'the exact rate is not published' }
)

$spanishPack = [ordered]@{
    code = "es"; name = "Spanish"; nativeName = "Español"; direction = "ltr"
    translations = $spanishTranslations; patterns = $patternsEs; segments = $segmentsEs
}
$englishPack = [ordered]@{
    code = "en"; name = "English"; nativeName = "English"; direction = "ltr"
    translations = $orderedEnglish; patterns = $patternsEn; segments = $segmentsEn
}

$spanishPack | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $output "es.json") -Encoding utf8
$englishPack | ConvertTo-Json -Depth 6 | Set-Content (Join-Path $output "en.json") -Encoding utf8
Write-Host "Paquetes generados: $($strings.Count) frases en español e inglés."







