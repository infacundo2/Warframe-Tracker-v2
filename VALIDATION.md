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

El laboratorio se probó con una reliquia normalizada real: respondió HTTP 200
en 3,08 s incluyendo seis precios limitados, valor esperado e historial manual.
La integración usa `/v2/orders/item/{slug}/top`, caché de cinco minutos y una
cadencia inferior al límite público de tres solicitudes por segundo.

Después de añadir estados de propiedad y duplicados, `/warframes`, `/weapons`,
`/mods` e `/inventory-tools` se repitieron en Release con HTTP 200.

El catálogo se resincronizó correctamente después de incorporar `itemCount`.
La compilación Release posterior terminó con 0 errores y 0 advertencias; los
detalles, objetivos, planificador y análisis de construcción usan ahora la
cantidad requerida de cada componente o recurso.

La matriz final volvió a comprobar 15 rutas en Release, incluidas autenticación,
centro de mando, catálogos, objetivos, Worldstate, builds y herramientas de
inventario: todas respondieron HTTP 200.

## Sincronización de AlecaFrame — 2026-07-27

- Compilación Release: 0 errores y 0 advertencias.
- Migración `20260727092214_AddRelicSyncProfile` aplicada correctamente.
- El decodificador binario se verificó con Lith Intacta, Axi Radiante y Réquiem
  Perfecta, incluidas cantidades y nombres de dos y tres caracteres.
- El catálogo español fue auditado: 3.072 variantes válidas y cero códigos
  vacíos. La duplicación externa conocida de Lith G12 se conserva sin sobrescribir
  ni poner en cero hasta que la fuente la desambigüe.
- Smoke test Release HTTP 200 en `/`, `/relics`, `/relic-sync`,
  `/inventory-tools` y `/auth/login`, sin indicador de error Blazor.
- La API rechaza tokens inválidos sin aplicar cambios y el cliente no registra
  la URL que contiene el token.
- El navegador integrado volvió a estar indisponible por la política de sandbox;
  la pantalla se validó mediante compilación y smoke HTTP.

## Limitación del entorno

El navegador integrado no pudo inicializarse por falta de la política de sandbox
del propio entorno. La validación visual interactiva debe repetirse en un
navegador disponible antes de fusionar a `main`; no se sustituye por el smoke test.
