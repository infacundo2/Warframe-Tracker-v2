# Warframe Tracker v2

Aplicación de inventario, reliquias, objetivos y planificación de farmeo para
Warframe. El repositorio contiene tres modos:

- `WarframeInventory`: aplicación web Blazor con MySQL.
- `desktop`: cliente OW Electron con backend local ASP.NET y SQLite.
- `overwolf-native`: cliente Overwolf Native independiente y preparado para
  distribución OPK sin reemplazar la variante Electron.

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

### Base de datos y migraciones

El alojamiento de escritorio y el proveedor de datos son opciones separadas.
La aplicación portable usa SQLite local por defecto y actualiza solamente su
propia base. Para una prueba administrativa con MySQL se configura el proveedor
desde el entorno, sin modificar binarios ni incluir contraseñas en el paquete:

```powershell
$env:WARFRAME_TRACKER_DATABASE_PROVIDER = "MySql"
$env:WARFRAME_TRACKER_DB_HOST = "servidor"
$env:WARFRAME_TRACKER_DB_PORT = "3306"
$env:WARFRAME_TRACKER_DB_USER = "usuario-qa-restringido"
$env:WARFRAME_TRACKER_DB_PASS = "contraseña"
$env:WARFRAME_TRACKER_DB_NAME = "base-qa"
```

Una base MySQL compartida solo se migra cuando existe una orden explícita. El
administrador puede ejecutar una vez el backend con `--migrate-database` o usar
`WARFRAME_TRACKER_APPLY_MIGRATIONS=1`. El Blueprint de Render activa esta última
opción para aplicar migraciones versionadas al desplegar una nueva build. No se
deben distribuir variables ni credenciales dentro de ZIP, instaladores o
archivos versionados.

La URL pública consumida por Overwolf Native se cambia en un solo lugar:
`overwolf-native/tracker.config.json`. `npm run build` regenera la URL del iframe,
el origen permitido del manifiesto y su CSP.

## Importación segura alternativa

```powershell
.\Importar-Inventario-Seguro.ps1 .\inventario.json
```

El PS1 importa un JSON ya obtenido; no lee memoria ni tráfico de Warframe. Un
script común no puede generar por sí solo el evento privado de Overwolf.

## Documentación

- [Arquitectura de escritorio](desktop/README.md)
- [Overwolf Native: instalación, QA y OPK](overwolf-native/README.md)
- [Publicación en Overwolf](docs/publishing/OVERWOLF_SUBMISSION.md)
- [Propuesta de aplicación](docs/publishing/APP_PROPOSAL.md)
- [Ficha de tienda](docs/publishing/STORE_LISTING.md)
- [Checklist QA](docs/publishing/QA_CHECKLIST.md)
- [Límites del método alternativo](docs/FALLBACK_IMPORT.md)
