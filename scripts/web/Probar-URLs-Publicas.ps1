[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [ValidatePattern('^https://')]
    [string]$BaseUrl
)

$ErrorActionPreference = "Stop"
$base = $BaseUrl.TrimEnd('/')
$checks = @(
    @{ Path = "/privacy.html"; Text = "Warframe Tracker Privacy Policy" },
    @{ Path = "/support.html"; Text = "Warframe Tracker Support" }
)

foreach ($check in $checks) {
    $uri = "$base$($check.Path)"
    $response = Invoke-WebRequest -Uri $uri -MaximumRedirection 3 -UseBasicParsing
    if ($response.StatusCode -ne 200) {
        throw "$uri respondió HTTP $($response.StatusCode)."
    }
    if ($response.BaseResponse.ResponseUri.Scheme -ne "https") {
        throw "$uri dejó de usar HTTPS después de redirigir."
    }
    if ($response.Content -notmatch [regex]::Escape($check.Text)) {
        throw "$uri no contiene el texto esperado '$($check.Text)'."
    }
    if ($response.Content -match "Identifícate" -or $response.Content -match "Iniciar enlace") {
        throw "$uri exige autenticación y no es una página pública."
    }
    Write-Host "OK  $uri  HTTP 200  HTTPS  acceso público"
}
