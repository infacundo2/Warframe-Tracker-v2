# Warframe Tracker v2

Aplicación de inventario, reliquias, objetivos y planificación de farmeo para
Warframe. El repositorio contiene tres modos:

- `WarframeInventory`: aplicación web Blazor con MySQL.
- `overwolf-native`: cliente actual de Overwolf Native y fuente del OPK.
- `desktop-electron`: cliente OW Electron legado, conservado por si se retoma
  en el futuro; no es el paquete que se publica actualmente.

## Overwolf Native (actual)

```powershell
cd overwolf-native
npm ci
npm run package:opk
```

El resultado actual queda en
`out/overwolf-native/Warframe-Tracker-Native-0.1.2.opk`. El empaquetador limpia
automáticamente versiones OPK anteriores.

## OW Electron (legado)

```powershell
cd desktop-electron
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

### Producción en Render

Render entrega el puerto HTTP mediante `PORT`; la aplicación lo consume
automáticamente y escucha en `0.0.0.0`. No se debe definir manualmente
`ASPNETCORE_URLS` ni `ASPNETCORE_HTTP_PORTS` en el panel.

Las claves utilizadas por Identity y sus cookies se conservan en MySQL y se
cifran con un certificado PKCS#12. En Windows puede generarse una vez con:

```powershell
.\scripts\web\New-RenderDataProtectionCertificate.ps1
```

El script deja los archivos sensibles bajo `.tools/render-secrets`, carpeta
ignorada por Git. Copia el contenido del archivo `*.base64.txt` al secreto
`WARFRAME_TRACKER_DP_CERT_BASE64` de Render y usa la contraseña ingresada como
`WARFRAME_TRACKER_DP_CERT_PASSWORD`. Conserva el PFX en un respaldo privado: no
regeneres ni reemplaces el certificado sin planificar la rotación, porque las
claves históricas de sesión necesitan poder descifrarse.

Las credenciales MySQL se configuran exclusivamente mediante los secretos
`WARFRAME_TRACKER_DB_HOST`, `WARFRAME_TRACKER_DB_PORT`,
`WARFRAME_TRACKER_DB_USER`, `WARFRAME_TRACKER_DB_PASS` y
`WARFRAME_TRACKER_DB_NAME`. `WARFRAME_TRACKER_DB_SSL_MODE` acepta los modos de
MySqlConnector; usa `Required`, `VerifyCA` o `VerifyFull` cuando el proveedor de
la base entregue TLS verificable.

La URL pública consumida por Overwolf Native se cambia en un solo lugar:
`overwolf-native/tracker.config.json`. `npm run build` regenera la URL del iframe,
el origen permitido del manifiesto y su CSP.

## Importación segura alternativa

```powershell
.\scripts\inventory\Importar-Inventario-Seguro.ps1 .\inventario.json
```

El PS1 importa un JSON ya obtenido; no lee memoria ni tráfico de Warframe. Un
script común no puede generar por sí solo el evento privado de Overwolf.

## Documentación

- [Arquitectura Electron](desktop-electron/README.md)
- [Overwolf Native: instalación, QA y OPK](overwolf-native/README.md)
- [Mapa del repositorio](docs/REPOSITORY_STRUCTURE.md)
- [Roadmap](docs/ROADMAP.md)
- [Validaciones](docs/VALIDATION.md)
- [Publicación en Overwolf](docs/publishing/OVERWOLF_SUBMISSION.md)
- [Propuesta de aplicación](docs/publishing/APP_PROPOSAL.md)
- [Ficha de tienda](docs/publishing/STORE_LISTING.md)
- [Checklist QA](docs/publishing/QA_CHECKLIST.md)
- [Límites del método alternativo](docs/FALLBACK_IMPORT.md)
