# Validación de la rama de desarrollo

Fecha: 2026-07-25  
Rama: `feature/operator-goals`

## Puertas automáticas

- `dotnet build -c Release --no-restore`: 0 errores, 0 advertencias.
- Migraciones aplicadas hasta `20260725054538_AddInventoryMetadataAndUndo`.
- Worldstate combinado de PC comprobado contra `api.warframestat.us`.

## Smoke test HTTP

Una instancia Release local respondió HTTP 200 en:

- `/`, `/warframes`, `/weapons`, `/mods`, `/relics`
- `/goals`, `/buildable`, `/worldstate`, `/search`, `/compare`
- `/inventory-tools`, `/inventory-manager`, `/builds`
- detalles reales de Warframe, arma, mod y reliquia
- laboratorio de una reliquia real

Durante la primera pasada `/builds` devolvió 500 por la captura del índice de
una ranura. Se corrigió usando un índice estable y la matriz completa se repitió
con resultado HTTP 200.

Una segunda pasada confirmó `/worldstate`, `/mods`, `/compare`, `/builds`,
`/inventory-tools` e `/inventory-manager`, el SVG orbital del inicio y el manejo
seguro de un enlace de build inválido, todos con HTTP 200.

## Limitación del entorno

El navegador integrado no pudo inicializarse por falta de la política de sandbox
del propio entorno. La validación visual interactiva debe repetirse en un
navegador disponible antes de fusionar a `main`; no se sustituye por el smoke test.
