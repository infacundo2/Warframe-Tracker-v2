# TrackerAgent — análisis de arquitectura (Fase 0)

Fecha del análisis: 12 de agosto de 2026.

## 1. Alcance y decisión

Este documento cubre únicamente la **Fase 0**. No implementa el agente, no
modifica contratos, no cambia Electron ni Overwolf y no altera la base de
datos.

La arquitectura recomendada es la opción B: un ejecutable independiente.

```text
WarframeTracker.Desktop.exe  -> interfaz del usuario (existente/futura)
WarframeTracker.Agent.exe    -> detección y captura local
Warframe Tracker Web         -> ASP.NET Core en Render
```

`WarframeTracker.Agent.exe` debe ser otra fuente de datos. No reemplaza a
Overwolf Native, a Electron, a la importación manual ni a AlecaFrame.

La separación es especialmente conveniente en este repositorio porque el
cliente Electron actual está marcado como legado, depende del runtime de
Overwolf Electron y combina interfaz, captura GEP y un backend ASP.NET/SQLite
local. Alojar allí un nuevo método de captura volvería a acoplar su ciclo de
vida a una tecnología que no es la distribución actual.

## 2. Arquitectura encontrada

### 2.1 Aplicación web y backend

El proyecto `WarframeInventory/WarframeInventory` es una aplicación ASP.NET
Core 8 con Blazor Server, MudBlazor, Identity y Entity Framework Core.

Puede trabajar en dos modos:

- MySQL: servicio web de Render y base compartida.
- SQLite: backend local utilizado por la variante Electron y por QA.

`Program.cs` registra los controladores, Identity por cookie, el limitador de
tasa, los servicios de inventario y el `DbContextFactory`. Render despliega la
misma aplicación mediante Docker. La Web no expone actualmente una API general
de inventario autenticada con tokens; las rutas remotas usan la cookie de
Identity del navegador.

La base de datos de producción comparte el esquema `cja3651_ACNH` con otras
aplicaciones. Cualquier futura migración del agente deberá limitarse a tablas
propias de Warframe Tracker e Identity relacionadas con el Tracker.

### 2.2 Electron

`desktop-electron` es una variante legada basada en Overwolf Electron. Su
proceso principal realiza actualmente estas tareas:

1. Reserva un puerto de loopback aleatorio.
2. Genera una clave efímera de 256 bits.
3. Inicia `WarframeInventory.exe` en modo escritorio con SQLite local.
4. Muestra ese backend local en una ventana Electron.
5. Recibe `match_info.inventory` mediante Overwolf GEP.
6. Deduplica capturas con SHA-256.
7. Envía el JSON al endpoint local `/api/desktop-bridge/inventory`.

No inicia sesión en Render ni sincroniza la base SQLite con la cuenta web. La
autenticación que ve el usuario dentro de Electron pertenece al Identity del
backend local. La clave `WARFRAME_DESKTOP_BRIDGE_KEY` solo protege la conexión
entre Electron y el proceso ASP.NET del mismo equipo; no es una credencial de
usuario ni debe reutilizarse por Internet.

### 2.3 Overwolf Native

`overwolf-native` es el cliente publicado actualmente. Utiliza el Game ID 8954
y las funciones GEP `game_info` y `match_info` para recibir el inventario.

Su flujo es:

```text
Overwolf GEP
  -> normalización básica a JSON
  -> SHA-256 para deduplicación local
  -> IndexedDB (máximo 30 minutos)
  -> envío explícito a iframe de Warframe Tracker
  -> POST /api/native-inventory/capture
  -> vista previa en /native-sync
  -> confirmación manual
  -> transacción en MySQL
```

El iframe contiene la Web de Render. El usuario inicia sesión allí usando la
cookie normal de Identity. El canal `postMessage` valida el origen del Tracker
y usa un nonce por página. El JSON bruto no se registra.

### 2.4 AlecaFrame e importación manual

`RelicSyncService` vincula un token público de AlecaFrame, prepara una vista
previa de reliquias/cuenta y aplica los cambios después de confirmación. Es un
flujo distinto al inventario GEP y no debe usarse como autenticación del nuevo
agente.

`InventoryToolsService` permite exportar e importar un contrato
`InventoryTransfer` versión 1. Es útil como referencia de un modelo normalizado
portable, pero su importación realiza numerosas consultas individuales y no
incluye recursos, cuenta, metadatos de fuente ni idempotencia. No es adecuado
como endpoint automático sin adaptarlo.

## 3. Cómo recibe hoy el servidor el inventario

Existen dos puentes de captura:

| Ruta | Cliente | Autenticación | Destino | Aplicación |
| --- | --- | --- | --- | --- |
| `POST /api/desktop-bridge/inventory` | Electron legado | Solo loopback + clave efímera | Backend SQLite local | Vista previa manual |
| `POST /api/native-inventory/capture` | Overwolf Native | Cookie Identity + cabecera de puente | Render/MySQL | Vista previa manual |

También existen:

- `GET /api/desktop-bridge/health`: salud del puente local; rechaza tráfico no
  loopback.
- `GET /health`: salud pública y superficial del servidor.
- Formularios MVC de login, registro y logout: producen una cookie de navegador,
  no un token de API.

Ningún endpoint actual es suficiente por sí solo para un agente independiente:

- El puente Desktop no acepta conexiones remotas y su clave no representa al
  usuario.
- El puente Native necesita una cookie del navegador y deja la captura en
  memoria para revisión desde Blazor.
- `/health` sirve para conectividad, pero no autentica al agente ni valida la
  base.

Por lo tanto, la comunicación de TrackerAgent sí requiere endpoints nuevos y
versionados. Esto no es duplicación innecesaria: es un límite de seguridad
distinto. Los endpoints actuales deben permanecer sin cambios.

## 4. Componentes que se pueden reutilizar

### 4.1 Reutilización directa

- `IDbContextFactory<ApplicationDbContext>` para operaciones breves y seguras.
- Entidades de catálogo `Warframe`, `Weapon`, `Mod`, `Relic` y
  `RelicReward`.
- Entidades de usuario `UserWarframe`, `UserWeapon`, `UserMod`, `UserRelic`,
  `UserComponent` y `UserResource`.
- `AlecaAccountSnapshot` para créditos, Endo, platino, ducados, Aya y rango de
  maestría. El nombre de la entidad es histórico; antes de reutilizarla como
  modelo general debe aclararse la procedencia de cada campo.
- `InventoryEvent` y el registro automático desde `ApplicationDbContext` para
  el historial básico de cambios.
- La estrategia transaccional y de reintentos de
  `DesktopInventorySyncService.ApplyAsync`.
- Los límites existentes: máximo 20 MB, profundidad JSON 128, capturas
  parciales/autoritativas y lista acotada de desconocidos.
- Los tests de rollback y aplicación completa de
  `DesktopInventorySyncServiceTests` como red de regresión.

### 4.2 Lógica que debe extraerse, no duplicarse

`DesktopInventorySyncService` ya contiene el núcleo que se necesita:

- lectura recursiva de `ItemType`, `ItemCount` y `XP`;
- clasificación de secciones GEP;
- relación de identificadores con el catálogo;
- reconocimiento de componentes y recursos;
- cálculo de diferencias;
- tratamiento de capturas parciales;
- aplicación atómica a todas las tablas de inventario.

El problema es que mezcla tres responsabilidades:

1. mantiene una única captura mutable en memoria;
2. normaliza y compara;
3. aplica a la base de datos.

Antes de conectar el inventario real del agente, conviene extraer el núcleo a
un servicio sin estado, por ejemplo `InventoryIngestionService`. El servicio
actual seguiría llamándolo y conservaría sus métodos y DTO públicos, de modo
que Electron y Overwolf no cambien.

### 4.3 Componentes que no existen todavía

No se encontró una implementación existente de:

- detección de `Warframe.x64.exe` como servicio;
- seguimiento incremental de `EE.log`;
- parser de eventos de `EE.log`;
- snapshots locales A/B persistentes;
- cola offline del agente;
- token de dispositivo para aplicaciones nativas;
- idempotencia persistente por captura/evento;
- precedencia persistente entre Overwolf, Electron, TrackerAgent y edición
  manual.

`tools/DevGepLauncher` consulta procesos para evitar abrir dos launchers, pero
es una herramienta WinForms de QA. Puede servir como referencia de manejo de
excepciones al inspeccionar procesos; no es una abstracción reutilizable del
agente.

## 5. Snapshots y diferencias actuales

Sí existe una forma parcial de diff:

- `DesktopInventorySyncService.PreviewAsync` compara la captura recibida con
  el inventario persistido del usuario.
- Devuelve `DesktopInventoryChange` con categoría, identificador, nombre,
  cantidad anterior y cantidad nueva.
- `RelicSyncService` tiene una vista previa equivalente para reliquias de
  AlecaFrame.

No existe un sistema genérico de `Snapshot A` contra `Snapshot B`. Tampoco se
persiste el snapshot bruto o normalizado completo. `InventoryEvent` guarda
cambios individuales después de `SaveChanges`, pero actualmente:

- no registra la fuente;
- no registra un `BatchId` o `EventId` externo;
- usa la hora del servidor;
- no incluye recursos;
- no permite reconstruir de forma inequívoca un snapshot completo;
- no impide que se aplique dos veces una captura después de reiniciar.

La lógica de comparación existente debe convertirse en el núcleo de un
`IInventoryDiffService`; no se debe escribir un comparador paralelo dentro del
agente. El agente puede calcular un diff local para mostrar actividad, pero el
servidor debe volver a validar y calcular el cambio autoritativo antes de
escribir MySQL.

## 6. Autenticación recomendada para TrackerAgent

Electron y Overwolf no comparten un mecanismo reutilizable por un proceso
Windows independiente:

- Electron usa una clave efímera local y una cuenta SQLite local.
- Overwolf usa la cookie web dentro del iframe.

TrackerAgent debe usar la misma cuenta `AspNetUsers`, pero necesita una
credencial de dispositivo revocable. Se recomienda un flujo de emparejamiento
similar al Device Authorization Flow:

1. El agente solicita un código de emparejamiento temporal.
2. Abre el navegador del sistema en la Web de Render.
3. El usuario inicia sesión con el Identity existente y aprueba el dispositivo.
4. El agente intercambia el código una sola vez por un token aleatorio propio
   del Tracker.
5. El servidor guarda únicamente el hash del token y lo vincula al mismo
   `IdentityUser`.
6. Windows guarda el token protegido para el usuario actual, mediante DPAPI o
   Windows Credential Manager.
7. El usuario puede revocar el dispositivo desde la Web.

No se deben entregar usuario y contraseña al agente, reutilizar cookies del
navegador, crear otra tabla de cuentas ni utilizar una clave global compartida
por todas las instalaciones.

El token debe tener alcance reducido, por ejemplo `agent:sync`, y nunca dar
acceso administrativo o a credenciales de Warframe.

## 7. API mínima propuesta

Todas las rutas nuevas deben vivir bajo `/api/agent/v1` para no cambiar los
contratos existentes.

### Fase de emparejamiento

```text
POST /api/agent/v1/pairing/start
POST /api/agent/v1/pairing/token
GET  /agent/connect                 (página web autenticada de aprobación)
POST /api/agent/v1/devices/revoke   (o acción equivalente desde la cuenta)
```

Los códigos deben caducar, ser de un solo uso, estar limitados por tasa y
guardarse como hash.

### Estado y sincronización

```text
GET  /api/agent/v1/status
POST /api/agent/v1/inventory/preview
POST /api/agent/v1/inventory/apply
```

En la primera iteración conviene mantener la separación vista previa/aplicación
ya utilizada por Overwolf. La aplicación automática debe habilitarse después
de implementar idempotencia, precedencia y cola offline.

Un sobre de sincronización debería incluir al menos:

```text
schemaVersion
batchId
deviceId
deviceSequence
capturedUtc
source
isAuthoritative
contentHash
payload
```

El `payload` puede aceptar inicialmente el formato GEP que ya entiende el
servidor. Cuando un provider no produzca ese formato, debe convertirlo a un
contrato normalizado versionado del Tracker; no debe fingir que es GEP.

## 8. Cambios mínimos necesarios en el backend

No hacen falta cambios para la Fase 1 de detección. Para las fases posteriores
serán necesarios, como mínimo:

1. Autenticación de dispositivo vinculada a Identity.
2. Controladores versionados `/api/agent/v1`.
3. Extracción sin ruptura del parser/diff/aplicación actualmente contenido en
   `DesktopInventorySyncService`.
4. Idempotencia persistente y registro de fuente.
5. Reglas para descartar snapshots antiguos.
6. Limitación de tasa por dispositivo/usuario, además de por IP.
7. Auditoría segura sin guardar el JSON bruto en logs.

Tablas Warframe propuestas para una migración futura:

### `AgentDevices`

- `Id`/`DeviceId`.
- `UserId` con FK a `AspNetUsers`.
- `DisplayName`.
- `TokenHash`.
- `CreatedUtc`, `LastSeenUtc`, `RevokedUtc`.
- `LastSequence`.

### `InventorySyncBatches`

- `BatchId` único.
- `UserId` y `DeviceId`.
- `Source`, `SchemaVersion`, `DeviceSequence`.
- `CapturedUtc`, `ReceivedUtc`, `AppliedUtc`.
- `ContentHash`, `IsAuthoritative`, `Status`.
- Conteos y mensaje de error seguro, nunca el secreto o credenciales.

No es necesario agregar `Source` a cada fila de inventario si
`InventorySyncBatches` conserva la procedencia y las reglas se aplican de forma
atómica. Si se necesita procedencia por objeto, debe diseñarse después de tener
casos reales; añadirla ahora multiplicaría la migración y los conflictos.

## 9. Precedencia entre fuentes

El estado actual no tiene precedencia persistente. `Source` vive solamente en
la captura/vista previa, y una aplicación posterior puede sobrescribir datos
anteriores. La deduplicación SHA-256 de Electron y Overwolf es únicamente local
y se pierde al reiniciar.

Regla recomendada:

1. Ninguna fuente recibe prioridad absoluta solo por su nombre.
2. Una captura completa puede marcar ausencias únicamente si es
   `IsAuthoritative=true`.
3. Una captura parcial solo agrega o actualiza los objetos presentes.
4. El servidor acepta cada `BatchId` una sola vez.
5. `DeviceSequence` debe crecer por dispositivo; un número repetido o menor se
   rechaza como antiguo.
6. Una captura completa encolada no puede sobrescribir otra captura completa
   más reciente del usuario.
7. La hora del cliente se valida con tolerancia porque el reloj de Windows
   puede estar desajustado; no debe ser el único criterio.
8. Si hay conflicto entre edición manual y sincronización automática, la Web
   debe mostrarlo o permitir bloquear la automatización para ese usuario.

Una respuesta `already_applied` debe considerarse éxito y permitir eliminar el
elemento de la cola local.

## 10. Arquitectura interna propuesta del agente

`WarframeTracker.Agent` debe ser un host .NET 8 para Windows x64, inicialmente
sin interfaz compleja. El proceso principal solo coordina estados y servicios.

```text
AgentHost
  |
  +-- IWarframeProcessDetector
  |     +-- WindowsWarframeProcessDetector
  |
  +-- IWarframeSessionCoordinator
  |
  +-- IInventoryProvider
  |     +-- ExperimentalInventoryProvider   [desactivado por defecto]
  |     +-- FutureInventoryProvider
  |
  +-- IEELogSource
  |     +-- EELogTailReader
  |           +-- IEELogParser
  |
  +-- IInventorySnapshotStore
  +-- IInventoryDiffService
  +-- ITrackerApiClient
  +-- IOfflineSyncQueue
  +-- IAgentCredentialStore
  +-- ILogger<T>
```

No se encontró una fuente local segura que entregue el inventario completo sin
Overwolf. Por eso `IInventoryProvider` debe aceptar que no haya providers
disponibles. `EE.log` puede producir eventos que disparen una actualización,
pero no debe asumirse que contiene un inventario completo.

Cualquier provider que inspeccione el proceso debe estar en un assembly o
módulo separado, marcado como experimental, solo lectura, desactivado por
defecto y sustituible. No se utilizarán inyección, hooks, escritura de memoria,
automatización ni modificación de archivos del juego.

### Detección de proceso

La implementación más simple y resistente para la Fase 1 es un
`BackgroundService` con `PeriodicTimer` configurable, por defecto cada cinco
segundos, que consulte `Process.GetProcessesByName("Warframe.x64")` y emita
eventos solamente al cambiar de estado.

Debe disponer cada objeto `Process`, tolerar `Win32Exception` y verificar la
identidad por nombre/PID. No se recomienda WMI inicialmente: agrega otra
dependencia y una consulta cada cinco segundos tiene un costo despreciable sin
ser polling agresivo.

### Sesión de Warframe

El coordinador crea un `CancellationTokenSource` por sesión:

```text
esperando -> proceso detectado -> providers iniciados
          -> proceso cerrado   -> cancelar sesión
                                -> cerrar archivos
                                -> limpiar temporales de sesión
                                -> esperando
```

Abrir y cerrar Warframe repetidamente no debe reiniciar el proceso del agente.

### EE.log

No existe código equivalente en el repositorio. El lector deberá:

- resolver la ruta desde `LocalApplicationData` y permitir override de
  configuración;
- abrir con `FileShare.ReadWrite | FileShare.Delete`;
- conservar offset y fragmento de línea incompleta;
- leer solo los bytes agregados;
- detectar `Length < offset` como truncamiento;
- reabrir cuando el archivo sea reemplazado/rotado;
- combinar `FileSystemWatcher` con una comprobación periódica lenta, porque el
  watcher puede perder eventos;
- producir eventos tipados y no exponer líneas sensibles completas en logs.

### Cache y cola local

Ruta propuesta:

```text
%LocalAppData%/WarframeTracker/Agent/
  settings.json
  state/
  queue/
  logs/
```

El token no debe estar dentro de `settings.json`; se almacena protegido por el
usuario de Windows. Las escrituras de snapshot/cola deben ser atómicas. La cola
debe tener límites por cantidad, bytes y antigüedad, reintento con backoff y
jitter, y no incluir credenciales de Warframe.

## 11. Feature flags propuestas

Configuración local de ejemplo, sin secretos:

```json
{
  "TrackerAgent": {
    "Enabled": true,
    "ProcessDetectionIntervalSeconds": 5,
    "InventoryProviderEnabled": true,
    "EELogProviderEnabled": true,
    "ExperimentalProviderEnabled": false,
    "AutomaticSyncEnabled": false,
    "SyncIntervalMinutes": 15,
    "ServerBaseUrl": "https://warframe-inventory.onrender.com"
  }
}
```

La configuración remota nunca puede activar silenciosamente un provider
experimental que el usuario haya desactivado localmente.

## 12. Estructura exacta propuesta

Se conserva la organización actual; no se mueve ningún proyecto existente.

```text
Warframe-Tracker-v2/
  Warframe-Tracker-v2.sln
  WarframeInventory/
    WarframeInventory/
      Controllers/
        AgentV1/
          AgentPairingController.cs
          AgentInventoryController.cs
      Contracts/
        AgentV1/
          AgentPairingContracts.cs
          AgentInventoryContracts.cs
      Models/
        AgentDevice.cs
        InventorySyncBatch.cs
      Services/
        AgentAuthenticationService.cs
        InventoryIngestionService.cs
      Migrations/
      DesktopMigrations/
    WarframeInventory.Tests/
      AgentAuthenticationTests.cs
      AgentInventoryApiTests.cs
      InventoryIngestionServiceTests.cs
  WarframeTracker.Agent/
    WarframeTracker.Agent.csproj
    Program.cs
    Configuration/
      TrackerAgentOptions.cs
    Hosting/
      AgentWorker.cs
      WarframeSessionCoordinator.cs
    ProcessDetection/
      IWarframeProcessDetector.cs
      WindowsWarframeProcessDetector.cs
    Providers/
      Inventory/
        IInventoryProvider.cs
        InventoryProviderResult.cs
      EELog/
        IEELogSource.cs
        EELogTailReader.cs
        IEELogParser.cs
      Experimental/
        ExperimentalInventoryProvider.cs
    Inventory/
      InventorySnapshot.cs
      InventoryDiff.cs
      InventorySnapshotStore.cs
    Sync/
      TrackerApiClient.cs
      OfflineSyncQueue.cs
    Security/
      AgentCredentialStore.cs
    Diagnostics/
      AgentState.cs
  WarframeTracker.Agent.Tests/
    ProcessDetection/
    Providers/
    Inventory/
    Sync/
```

Los DTO versionados de API pueden empezar dentro del backend. Si más de un
cliente .NET termina consumiéndolos, entonces se justificará un proyecto
pequeño `WarframeTracker.Contracts`; no hace falta crearlo durante el skeleton.

`WarframeTracker.Desktop.exe` no se modifica en estas fases. En el futuro puede
consultar el estado del agente mediante un canal local autenticado, como named
pipes, pero esa integración debe ser opcional y no forma parte del MVP.

## 13. Archivos existentes que se modificarían después de la Fase 0

### Fase 1

- `Warframe-Tracker-v2.sln`: agregar agente y tests.
- Ningún archivo de Web, Electron u Overwolf.

### Fase 2

- `WarframeInventory/WarframeInventory/Program.cs`: registrar autenticación y
  servicios del agente, políticas y rate limiting.
- `ApplicationDbContext.cs`: nuevos `DbSet`, índices y FK.
- Migraciones MySQL y SQLite equivalentes.
- Controladores/servicios nuevos, sin cambiar rutas existentes.

### Fases 4 a 6

- `DesktopInventorySyncService.cs`: delegar parser/diff/aplicación al núcleo
  extraído sin cambiar su interfaz externa.
- Tests existentes: conservarlos y agregar regresiones para ambos canales.
- `README.md` y `docs/REPOSITORY_STRUCTURE.md`: documentar el cuarto componente.

No se prevén cambios necesarios en `desktop-electron` ni `overwolf-native` para
que TrackerAgent funcione.

## 14. Riesgos técnicos

1. **No hay fuente local completa confirmada.** Detectar el proceso y leer
   `EE.log` es viable, pero eso no garantiza acceso al inventario completo.
2. **Fragilidad del provider experimental.** Una actualización de Warframe
   puede cambiar formatos o hacer que deje de funcionar.
3. **Compatibilidad y reglas del juego.** Antes de distribuir inspección del
   proceso debe revisarse su compatibilidad con las reglas vigentes y
   anticheat. El diseño debe funcionar aunque ese provider no se publique.
4. **Autenticación nativa ausente.** Guardar contraseñas o copiar cookies sería
   inseguro; el emparejamiento es trabajo obligatorio.
5. **Render gratuito.** Los arranques en frío y caídas temporales exigen una
   cola local y reintentos sin duplicación.
6. **Estado actual en memoria.** Las capturas Native se pierden al reiniciar el
   servidor; TrackerAgent necesita batches persistentes.
7. **Conflictos de fuente.** Hoy no se persisten fuente, secuencia ni timestamp
   de cada actualización de inventario.
8. **Reloj del cliente.** No se debe confiar únicamente en `capturedUtc`.
9. **Catálogo incompleto.** Los identificadores desconocidos deben omitirse y
   diagnosticarse sin invalidar el resto, como hace el flujo actual.
10. **Historial incompleto.** `InventoryEvent` no registra cambios de recursos
    ni procedencia.
11. **Datos sensibles locales.** Inventario, logs y tokens deben protegerse y
    tener retención limitada.
12. **Privilegios distintos.** Si Warframe corre elevado, algunas consultas de
    proceso/archivos pueden fallar; el agente debe degradarse sin cerrarse.
13. **Instancias múltiples.** Debe usarse mutex por usuario o una coordinación
    equivalente para evitar dos agentes y dos colas simultáneas.
14. **Tamaño del payload.** Mantener límites, streaming cuando corresponda y
    compresión HTTP sin permitir bombas de descompresión.

## 15. Plan de implementación por fases

### Fase 1 — Skeleton y detección

- Crear `WarframeTracker.Agent` y su proyecto de tests.
- Host genérico .NET 8, DI, opciones y `ILogger<T>`.
- Detector de proceso con transición Started/Stopped.
- Coordinador de sesión y shutdown limpio.
- Estado `WAITING_FOR_WARFRAME`/`WARFRAME_RUNNING`.
- Feature flags; provider experimental desactivado.
- Sin backend, inventario, EE.log, tray o instalador.

Criterio de salida: pruebas automatizadas de detección simulada y prueba manual
abriendo/cerrando Warframe varias veces sin fugas ni cierre del agente.

### Fase 2 — Emparejamiento y comunicación

- Crear autenticación de dispositivo vinculada a Identity.
- Guardar token con protección de Windows.
- Implementar `/api/agent/v1/status`.
- Agregar revocación y rate limiting.
- Probar caída de Render, token inválido y token revocado.

Criterio de salida: el agente identifica al usuario correcto sin recibir ni
guardar su contraseña.

### Fase 3 — EE.log

- Resolver ruta, tail incremental, rotación/truncamiento.
- Parser tipado y eventos de misión.
- Fixtures de logs anonimizados; ningún log real versionado.
- EE.log solo dispara eventos, no se considera inventario autoritativo.

### Fase 4 — Provider de inventario

- Definir `IInventoryProvider` y resultado versionado.
- Extraer el normalizador existente a un servicio sin estado.
- Implementar primero el provider seguro que realmente pueda confirmarse.
- Mantener cualquier inspección de proceso en el módulo experimental.
- Fallback sin detener el host.

No debe avanzarse con un provider directo hasta demostrar una fuente de datos
real, de solo lectura y distribuible.

### Fase 5 — Snapshots y diff

- Snapshot normalizado local.
- Comparación Added/Removed/Changed basada en el núcleo existente.
- Hash y secuencia de dispositivo.
- Persistencia limitada y escrituras atómicas.
- Pruebas de capturas parciales, desconocidos y grandes.

### Fase 6 — Sincronización automática resiliente

- Batches idempotentes y reglas de precedencia.
- Cola offline acotada, backoff exponencial y jitter.
- Preview por defecto; autoaplicación solo como opción explícita.
- Auditoría de fuente sin payload bruto.
- Pruebas de reenvío, concurrencia, batch antiguo y rollback.

### Fase posterior — Experiencia de escritorio

- Estado básico o tray si sigue siendo necesario.
- Integración opcional con `WarframeTracker.Desktop.exe` por IPC local.
- Empaquetado y firma de código.
- Autoupdate únicamente después de estabilizar contratos y firma.

## 16. Conclusión

La decisión correcta para este repositorio es mantener
`WarframeTracker.Agent.exe` independiente. Electron técnicamente podría detectar
procesos y leer archivos mediante Node, pero hacerlo allí obligaría a que la
captura dependa del cliente legado, de Overwolf Electron y de su backend SQLite
local. La separación permite cerrar la interfaz, sustituir providers y mantener
la captura activa sin afectar la Web ni Overwolf Native.

La pieza de mayor valor para reutilizar es
`DesktopInventorySyncService`; la pieza que falta antes de una sincronización
real es autenticación de dispositivo + idempotencia persistente. La Fase 1
puede comenzar sin tocar ningún sistema existente y debe limitarse al host,
configuración, logging, detección de `Warframe.x64.exe` y cierre limpio.
