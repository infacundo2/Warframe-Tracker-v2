# Warframe Tracker v2

Aplicación de inventario, reliquias, objetivos y planificación de farmeo para
Warframe. El repositorio contiene dos modos:

- `WarframeInventory`: aplicación web Blazor con MySQL.
- `desktop`: cliente OW Electron con backend local ASP.NET y SQLite.

## Aplicación de escritorio

```powershell
cd desktop
npm install
npm run start
```

La captura real de `match_info.inventory` requiere una propuesta aprobada y
credenciales de desarrollo de Overwolf:

```powershell
$env:OW_DEV_KEY = "token-de-desarrollo"
npm run start:dev-gep
```

Para generar un instalador de prueba:

```powershell
npm run package
```

El artefacto queda en `out/desktop-installer`. Sin las claves de Overwolf y un
certificado de firma de código, el instalador sirve para QA local, pero el
paquete GEP no se activa en producción.

## Importación segura alternativa

```powershell
.\Importar-Inventario-Seguro.ps1 .\inventario.json
```

El PS1 importa un JSON ya obtenido; no lee memoria ni tráfico de Warframe. Un
script común no puede generar por sí solo el evento privado de Overwolf.

## Documentación

- [Arquitectura de escritorio](desktop/README.md)
- [Publicación en Overwolf](docs/publishing/OVERWOLF_SUBMISSION.md)
- [Propuesta de aplicación](docs/publishing/APP_PROPOSAL.md)
- [Ficha de tienda](docs/publishing/STORE_LISTING.md)
- [Checklist QA](docs/publishing/QA_CHECKLIST.md)
- [Límites del método alternativo](docs/FALLBACK_IMPORT.md)
