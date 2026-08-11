# Estructura del repositorio

El repositorio contiene tres aplicaciones relacionadas, pero independientes:

| Ruta | Responsabilidad | Distribución |
| --- | --- | --- |
| `WarframeInventory/` | Aplicación ASP.NET Core/Blazor, API, Identity y acceso a MySQL/SQLite | Docker en Render |
| `overwolf-native/` | Cliente actual de Overwolf Native que recibe GEP y muestra la web | OPK |
| `desktop-electron/` | Cliente OW Electron conservado para un posible desarrollo futuro | EXE/instalador firmado |

## Directorios auxiliares

- `scripts/web/`: arranque, diagnóstico y secretos de la aplicación web.
- `scripts/desktop-electron/`: empaquetado y QA de la variante Electron.
- `scripts/inventory/`: importación manual segura de inventario.
- `tools/`: herramientas de desarrollo; no forman parte de Render ni del OPK.
- `docs/publishing/`: material de Overwolf, privacidad, capturas y publicación.
- `docs/qa/`: notas históricas de QA.
- `out/`: artefactos regenerables; no se versiona.

## Archivos que permanecen en la raíz

- `Warframe-Tracker-v2.sln`: solución .NET.
- `Dockerfile` y `render.yaml`: despliegue web.
- `Iniciar-Warframe-Tracker.ps1`: acceso rápido al servidor local.
- `README.md`: entrada principal del proyecto.
- `CODEX_HANDOFF.md`: contexto de continuidad del desarrollo.

## Política de artefactos

Los directorios `node_modules`, `bin`, `obj`, `dist`, `.tools`, `.dotnet-home` y
`out` se regeneran y están ignorados por Git. `npm run package:opk` elimina OPK
anteriores de `out/overwolf-native` y conserva solamente la versión declarada
en el manifiesto actual.

No deben guardarse credenciales, bases SQLite, payloads GEP reales, certificados
PFX ni instaladores dentro del repositorio.
