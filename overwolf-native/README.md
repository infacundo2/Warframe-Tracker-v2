# Warframe Tracker — Overwolf Native

Esta es la variante **Overwolf Native** del Tracker. Vive en una carpeta
independiente y no reemplaza ni modifica el cliente `desktop` basado en
ow-Electron. Ambos pueden mantenerse y evolucionar en paralelo.

## Estado actual

La base Native está implementada y preparada para QA:

- Ventana de escritorio visible y redimensionable; es la ventana raíz, por lo
  que cerrar la ventana cierra toda la aplicación.
- Warframe declarado con Game ID `8954`.
- Funciones GEP `game_info` y `match_info`, incluida la captura oficial
  `match_info.inventory`.
- Diez reintentos de `setRequiredFeatures()` y consulta periódica de
  `getInfo()` mientras Warframe está abierto.
- Recepción de `onInfoUpdates2`, deduplicación SHA-256 y límite de 20 MB.
- Captura temporal en IndexedDB durante un máximo de 30 minutos.
- El JSON bruto no se imprime en consola ni se incorpora a logs.
- El usuario debe confirmar antes de enviar la captura al sitio web.
- Canal `postMessage` con origen exacto y nonce aleatorio por página.
- Endpoint ASP.NET autenticado, limitado por tasa y aislado por usuario.
- Vista previa y aplicación transaccional reutilizando `/native-sync`.
- Atajo `Ctrl+Shift+T` declarado en el manifest.
- Iconos de tienda validados por peso y paquete OPK automatizado.

Todavía faltan dos datos externos que no podemos inventar:

1. Confirmación de Overwolf de que la propuesta/GEP aprobados se transfieren de
   ow-Electron a Native.
2. URL HTTPS definitiva donde estará alojado el Tracker ASP.NET.

No se necesita `OW_DEV_KEY` para la aplicación Native publicada ni un
certificado OV/EV propio. Para cargar builds no publicadas, la cuenta de
Overwolf sí debe continuar en la lista blanca de desarrolladores.

## 1. Requisitos del equipo

- Windows 11 x64.
- Overwolf instalado y sesión iniciada.
- Cuenta de Overwolf habilitada para desarrollo.
- Node.js 18 o superior.
- .NET SDK 8 para ejecutar el servidor local durante las pruebas.

Comprobar herramientas:

```powershell
node --version
npm --version
dotnet --version
```

## 2. Instalar y validar el proyecto

Desde la raíz del repositorio:

```powershell
cd overwolf-native
npm install
npm run typecheck
npm run validate
npm run validate:official
npm test
```

`validate` comprueba archivos, manifest, IDs, permisos, iconos, tamaños y
posibles credenciales. `validate:official` descarga el esquema oficial actual
de Overwolf y valida `dist/manifest.json` con ese esquema.

## 3. Probar con el Tracker local

Primero configura la URL local:

```powershell
.\Configurar-Tracker.ps1 -TrackerUrl "http://127.0.0.1:43127/native-sync"
npm run build
```

En otra terminal, desde la raíz del repositorio, inicia el backend local con
SQLite:

```powershell
$env:WARFRAME_TRACKER_DESKTOP = "1"
$env:WARFRAME_TRACKER_DATABASE_PROVIDER = "Sqlite"
$env:WARFRAME_TRACKER_URL = "http://127.0.0.1:43127"
dotnet run --project .\WarframeInventory\WarframeInventory\WarframeInventory.csproj -c Release --no-launch-profile
```

La primera ejecución crea una base local y descarga el catálogo. Crea una
cuenta de prueba dentro del Tracker antes de probar el envío Native.

## 4. Cargar como extensión sin empaquetar

1. Abre Overwolf e inicia sesión.
2. Haz clic derecho en el icono de Overwolf y abre **Settings / Configuración**.
3. Entra a **About / Acerca de**.
4. Abre **Development options**.
5. Pulsa **Load unpacked extension**.
6. Selecciona exactamente esta carpeta:

   ```text
   overwolf-native\dist
   ```

7. Busca Warframe Tracker en el dock de Overwolf y pulsa su icono.

Si aparece `Unauthorized App`, confirma que estás conectado con la cuenta
incluida en la whitelist. No selecciones la carpeta fuente: Overwolf debe cargar
`dist`, donde `manifest.json` queda en la raíz.

## 5. Prueba manual de inventario real

1. Abre Warframe Tracker Native.
2. Inicia sesión en el Tracker que aparece dentro de la ventana.
3. Inicia Warframe.
4. Espera hasta ver `GEP READY`.
5. Abre el inventario o entra y sal de un Repetidor, Dojo o misión.
6. Comprueba que cambie a `INVENTORY CAPTURED`.
7. Pulsa **Send to Tracker for review**.
8. En `/native-sync`, pulsa **Buscar captura**.
9. Pulsa **Analizar inventario** y revisa todos los cambios.
10. Aplica solamente después de revisar la vista previa.
11. Comprueba Créditos, Endo, Aya, Ducados, recursos, reliquias por
    refinamiento y maestría de equipo.

Pruebas de fallo obligatorias:

- Cerrar Warframe antes y después de una captura.
- Cerrar la ventana Native y comprobar que no quede un proceso invisible.
- Interrumpir Internet antes de enviar: la captura debe permanecer local.
- Cerrar sesión en el iframe: el servidor debe pedir inicio de sesión y no
  aplicar nada.
- Enviar dos veces la misma captura: el cliente debe deduplicarla.
- Usar dos usuarios diferentes: sus capturas nunca deben mezclarse.
- Esperar más de 30 minutos: la captura local debe caducar.

## 6. Probar con herramientas de Overwolf

Para abrir las herramientas CEF, activa las Developer Tools de Overwolf y, con
la ventana enfocada, pulsa `Ctrl+Shift+I`. El inventario completo no debe
aparecer nunca en Console.

También se puede usar el **Overwolf GEP Simulator** o el **Events Recorder and
Player**. Para ERP la aplicación debe permanecer cargada como extensión
unpacked; no se utiliza el OPK instalado. Reproduce `onInfoUpdates2` de Warframe
con feature `match_info` y key `inventory`.

## 7. Configurar la URL HTTPS definitiva

Cuando tengamos el dominio real:

```powershell
.\Configurar-Tracker.ps1 -TrackerUrl "https://tracker.example.com/native-sync"
npm run validate:official
npm test
```

El script actualiza simultáneamente:

- `public/runtime-config.js`;
- `externally_connectable.matches` del manifest;
- `frame-src` de la política CSP local.

No uses comodines amplios. La URL debe ser HTTPS y el servidor debe permitir
ser mostrado dentro del iframe Native. La excepción HTTP solo se admite para
`localhost` y `127.0.0.1` durante desarrollo.

En el servidor de producción, MySQL debe migrarse administrativamente antes de
publicar. La app Native no ejecuta migraciones ni contiene credenciales.

## 8. Crear el OPK para Overwolf

```powershell
npm run package:opk
```

Resultado:

```text
out\overwolf-native\Warframe-Tracker-Native-0.1.0.opk
```

El script vuelve a compilar, valida y deja `manifest.json` en la raíz del OPK.
También imprime el SHA-256. Antes de enviarlo, abre el archivo como ZIP y
confirma que no existe una carpeta contenedora adicional.

## 9. Qué enviar a Overwolf cuando respondan

- Confirmación escrita de que cambiamos el framework a Overwolf Native.
- Archivo OPK.
- Guía de QA y capturas de cada ventana/función.
- URL de privacidad, soporte y términos ya preparadas.
- Instrucciones de captura: abrir Tracker antes de Warframe, provocar una
  pantalla de carga, enviar, analizar y confirmar.
- Aclaración: no hay anuncios ni contenedor publicitario en este MVP.
- Aclaración: no hay plugin DLL, ejecutable auxiliar ni certificado externo.

Preguntas que deben contestarnos:

1. ¿Se conserva el acceso aprobado de Warframe `match_info.inventory`?
2. ¿Debemos usar el mismo formulario de revisión MVP para el OPK Native?
3. ¿Asignarán un nuevo App UID o migrarán el proyecto existente?
4. ¿Hay una versión mínima de GEP específica que debamos declarar?

## Referencias oficiales

- Frameworks: https://dev.overwolf.com/ow-native/getting-started/onboarding-resources/framework-overview/
- Manifest: https://dev.overwolf.com/ow-native/reference/manifest/manifest-json/
- Validación: https://dev.overwolf.com/ow-native/reference/manifest/validate-your-manifest-json/
- GEP: https://dev.overwolf.com/ow-native/live-game-data-gep/live-game-data-gep-intro/
- Warframe GEP: https://dev.overwolf.com/ow-native/live-game-data-gep/supported-games/warframe
- Cargar unpacked: https://dev.overwolf.com/ow-native/getting-started/onboarding-resources/basic-sample-app/
- Publicación OPK: https://dev.overwolf.com/ow-native/getting-started/release-your-app/

## Volver a ow-Electron en el futuro

No hay que revertir nada. La implementación anterior permanece en `desktop/` y
sus artefactos continúan separados. La integración compartida del servidor usa
endpoints diferentes:

- Electron local: `/api/desktop-bridge`.
- Overwolf Native autenticado: `/api/native-inventory`.

Esto permite retomar ow-Electron en el futuro si aparece un certificado viable,
sin perder el trabajo Native ni mezclar sus credenciales o ciclos de entrega.
