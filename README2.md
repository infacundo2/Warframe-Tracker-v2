# Warframe Tracker — estado, continuidad y trabajo pendiente

Última actualización de este documento: 12 de agosto de 2026.

Este README complementa al `README.md` principal. Su objetivo es dejar un
contexto completo de lo que ya se construyó, las decisiones tomadas, el estado
real de cada componente y el orden en que continuará el desarrollo.

## 1. Objetivo general

Warframe Tracker es una plataforma para administrar y analizar el progreso de
una cuenta de Warframe:

- Warframes, armas, maestría y estados de propiedad.
- Mods, reliquias, componentes y recursos con cantidades.
- Recompensas normalizadas de reliquias.
- Objetivos, builds, objetos construibles y planificación de farmeo.
- Comparaciones, relaciones entre objetos y estado del mundo.
- Importación manual y captura automática de inventario.
- Sincronización desde distintas fuentes sin reemplazar los métodos existentes.

La arquitectura objetivo queda separada por responsabilidades:

```text
WarframeTracker.Desktop.exe  -> interfaz local opcional
WarframeTracker.Agent.exe    -> detección y captura local independiente
Overwolf Native              -> captura oficial mediante GEP
Warframe Tracker Web         -> interfaz y backend en Render
MySQL                        -> datos del Tracker dentro de cja3651_ACNH
```

El Agent es una fuente adicional. No reemplaza Overwolf Native, Electron, la
Web, AlecaFrame ni la importación manual.

## 2. Componentes actuales

### `WarframeInventory`

Aplicación ASP.NET Core 8 con:

- Blazor Server.
- MudBlazor.
- ASP.NET Core Identity.
- Entity Framework Core.
- MySQL en Render.
- SQLite en el modo local de Electron/QA.
- API, catálogos, inventario, objetivos, builds y planificación.

### `overwolf-native`

Cliente actual de Overwolf. Obtiene `match_info.inventory` mediante GEP,
deduplica capturas, las conserva temporalmente en IndexedDB y las entrega a la
Web autenticada para revisión y aplicación manual.

### `desktop-electron`

Cliente legado de Overwolf Electron. Inicia un backend ASP.NET local con
SQLite, genera una clave efímera de loopback y envía allí las capturas GEP.
Permanece disponible, pero no es la distribución principal y no se integrará
automáticamente con TrackerAgent.

### `WarframeTracker.Agent`

Ejecutable .NET 8 para Windows. Detecta `Warframe.x64.exe`, se empareja con
Render mediante un token de dispositivo protegido con DPAPI, puede leer
eventos resumidos de `EE.log` y contiene el pipeline de snapshots y cola
offline. La captura directa permanece apagada hasta disponer de una fuente
local aprobada; el provider de bandeja segura sirve para QA sin leer el juego.

## 3. Trabajo realizado en la Web y backend

### Arquitectura y consultas

- Migración a consultas proyectadas y paginadas para los catálogos.
- Carga del inventario correspondiente a la página visible mediante consultas
  agrupadas, evitando una consulta por tarjeta.
- Uso de `IDbContextFactory` en los servicios nuevos.
- Reintentos de MySQL y pool de conexiones limitado.
- Compatibilidad entre MySQL y SQLite mediante contextos/migraciones separados.
- Operaciones críticas de inventario dentro de una transacción atómica.
- Rollback comprobado cuando una aplicación de inventario no alcanza el commit.

Todavía quedan páginas antiguas que inyectan directamente
`ApplicationDbContext`; se migrarán gradualmente a contextos breves creados por
la fábrica.

### Inventario

- Tablas independientes por usuario para Warframes, armas, mods, reliquias,
  componentes y recursos.
- Cantidades de mods, reliquias, componentes y recursos.
- Estados `missing`, `blueprint`, `set` y `built` donde corresponde.
- Maestría y XP de Warframes y armas.
- Historial `InventoryEvents` y posibilidad de deshacer ciertos cambios.
- Metadatos, notas y etiquetas.
- Importación/exportación JSON y CSV.
- Vista previa antes de aplicar capturas automáticas.
- Tratamiento seguro de capturas parciales: agregan/actualizan, pero no marcan
  objetos ausentes.
- Los identificadores desconocidos se muestran y omiten sin bloquear el resto.

### Reliquias y recompensas

- Las recompensas están normalizadas en `RelicRewards`.
- Existe una FK entre recompensa y reliquia.
- `RewardsJson` se conserva como compatibilidad/respaldo de la respuesta
  original, pero las búsquedas funcionales deben utilizar la tabla normalizada.
- Se optimizó la relación masiva entre componentes y reliquias.
- Se implementó inteligencia de reliquias, laboratorio, refinamientos,
  recompensas y rutas de farmeo.
- Se corrigieron cargas repetidas en detalles de Warframes y armas.

### Objetivos, builds y planificación

- Objetivos con cantidades requeridas y progreso.
- Análisis acotado para evitar bloqueos con objetivos grandes.
- Builds guardadas y asociación con objetivos.
- Planificador de farmeo con componentes faltantes.
- Relación entre recompensa, reliquia y ubicación.
- Vista de objetos construibles con progreso del inventario.
- Comparador y mapa de relaciones.

### Catálogo y sincronización

- Catálogos de Warframes, armas, mods y reliquias.
- Índices únicos por `UniqueName` e índices de búsqueda/categoría.
- Servicio automático de sincronización del catálogo.
- Comprobación cada 30 minutos y actualización cuando han pasado 24 horas
  desde la última sincronización correcta.
- Esto funciona sin que un usuario visite una página, siempre que la instancia
  de Render esté despierta.
- Si Render despierta después de más de 24 horas, la sincronización se realiza
  en esa nueva sesión del servidor.

Pendiente: bloqueo distribuido si en el futuro se ejecutan varias instancias y
política explícita para elementos retirados de la API.

### Diseño e idiomas

- Diseño futurista inspirado en Orokin/Warframe.
- Paneles, tarjetas, navegación, estados, animaciones y modo de energía
  reducida.
- Diseño adaptable para escritorio, tablet y móvil.
- Música ambiental y efectos opcionales.
- Paquetes español/inglés.
- Traducción específica de zonas, tipos de misión, rarezas y recompensas.
- Traducción de nombres como planos, sistemas, chasis, neurópticas y piezas de
  armas donde existe una regla fiable.

Pendiente: reemplazar gradualmente el traductor DOM basado en
`MutationObserver` por localización de servidor con `IStringLocalizer`, definir
el idioma correcto desde el primer HTML y limpiar claves generadas desde
fragmentos de código.

### Registro y autenticación

- Reglas de usuario permitidas y mensajes específicos en español.
- Las contraseñas requieren al menos diez caracteres, un número y una letra
  minúscula.
- Email único.
- Bloqueo temporal tras cinco intentos fallidos.
- Los formularios conservan los campos correctos y limpian únicamente las
  contraseñas cuando hay un error.
- Cookies `HttpOnly`, seguras en producción y persistencia de claves de Data
  Protection cifradas.
- Limitación por tasa en autenticación y recepción Native.

Pendiente prioritario:

- Añadir antiforgery a login, registro y logout.
- Eliminar el logout mediante GET.
- Proteger con `[Authorize]` objetivos, construibles y planificador.
- Restringir o retirar `/whoami` en producción.

### Caché, imágenes y rendimiento

- Compresión Brotli/Gzip.
- Versionado de CSS y JavaScript propios.
- Caché de catálogos y servicios externos.
- Paginación y proyecciones para reducir transferencia y memoria.
- Varias listas ya utilizan carga diferida de imágenes.

Pendiente:

- Aplicar `immutable` solamente a URLs realmente versionadas.
- Añadir dimensiones, `loading="lazy"` y `decoding="async"` a imágenes de
  detalles y comparaciones.
- Autoalojar la fuente Rajdhani o cargarla sin `@import` bloqueante.
- Optimizar el MP3 ambiental de aproximadamente 10 MB.
- Reducir CSS y diccionarios de idioma no utilizados.
- Separar health checks de vida y disponibilidad de MySQL.

## 4. Trabajo realizado en la base de datos

Se aplicaron migraciones versionadas sin eliminar datos válidos.

- Índices únicos para impedir duplicados de catálogo e inventario por usuario.
- Foreign keys desde tablas de usuario hacia Identity.
- Foreign key de `RelicRewards` hacia `Relics`.
- Archivo reversible de registros huérfanos encontrados durante la
  optimización.
- Tabla de recursos de usuario.
- Maestría de equipo.
- Historial de inventario.
- Builds, objetivos, metadatos y sincronización de reliquias.
- Persistencia de claves de Data Protection.
- Modelos y migraciones sincronizados tanto para MySQL como para SQLite.

Última auditoría conocida del área Warframe:

```text
Warframes:             126
Armas:                 640
Mods:                1.812
Reliquias:           3.096
Recompensas:        18.532
Usuarios:                7
Eventos inventario:  1.472
Builds:                  3
Objetivos:              11
Tamaño aproximado:    35 MB
```

En esa auditoría no había duplicados de catálogo ni inventarios huérfanos.
Estas cifras son una fotografía del 12 de agosto de 2026 y cambiarán con el
uso y nuevas sincronizaciones.

Pendiente para el Agent:

- `AgentDevices` para dispositivos emparejados y tokens hasheados.
- `InventorySyncBatches` para fuente, idempotencia, secuencia, timestamp y
  estado de cada sincronización.
- Registrar procedencia de cambios sin almacenar el JSON bruto.
- Incorporar recursos al historial de cambios.

Las futuras migraciones deben tocar solamente tablas de Warframe Tracker dentro
de `cja3651_ACNH`.

## 5. Trabajo realizado en Render y repositorio

- Docker multietapa para .NET 8.
- Lectura automática del puerto entregado por Render.
- Configuración segura detrás del proxy inverso.
- Aplicación opcional de migraciones durante el despliegue.
- Persistencia y cifrado de claves Identity/Data Protection.
- `appsettings.json`, certificados, claves, logs y bases locales ignorados por
  Git.
- Variables sensibles mediante secretos de entorno.
- Página de salud para Render.
- Documentación de estructura, QA, publicación y continuidad.
- Páginas públicas de privacidad y soporte publicables mediante GitHub Pages.

Riesgo pendiente importante: el endpoint MySQL actual obliga a desactivar TLS
desde Render porque falla su handshake. La solución definitiva es corregir TLS
en el alojamiento o migrar a un proveedor compatible y usar `VerifyFull`.

También falta un workflow de integración continua que compile y pruebe:

- solución .NET;
- Agent;
- Overwolf Native;
- Docker;
- migraciones;
- auditoría de dependencias y secretos.

## 6. Overwolf Native realizado

- Detección de Warframe con Game ID 8954.
- Funciones GEP `game_info` y `match_info`.
- Recepción por eventos y consulta periódica controlada.
- Deduplicación SHA-256.
- Límite de captura de 20 MB.
- Captura temporal en IndexedDB durante 30 minutos.
- El inventario bruto no se imprime en logs.
- Envío manual hacia la Web mediante `postMessage`.
- Validación de origen y nonce por página.
- Endpoint remoto autenticado por cookie Identity.
- Capturas aisladas por usuario.
- Vista previa y aplicación transaccional.
- Empaquetado OPK y validaciones de manifiesto.

Overwolf debe continuar funcionando sin depender del Agent.

## 7. Electron realizado y estado

- Backend ASP.NET/SQLite local incluido en el empaquetado.
- Puerto loopback aleatorio.
- Clave de puente efímera.
- Ventana de interfaz local.
- Captura GEP y deduplicación.
- Simulador de inventario para QA.
- Aplicación local mediante vista previa.

Electron queda como legado. Su cadena de empaquetado mantiene avisos altos de
dependencias transitivas sin corrección directa disponible. No se publicará un
nuevo instalador sin revisar esas dependencias y la firma correspondiente.

## 8. TrackerAgent — Fase 0 terminada

Se creó `docs/tracker-agent-analysis.md` con:

- arquitectura actual de Web, backend, Electron y Overwolf;
- endpoints y autenticación existentes;
- modelos y servicios reutilizables;
- estado de snapshots/diferencias;
- cambios mínimos del backend;
- diseño de autenticación por dispositivo;
- prioridad entre fuentes;
- estructura exacta de carpetas;
- riesgos y plan por fases.

Decisión final:

```text
WarframeTracker.Agent.exe independiente
```

No se integrará inicialmente dentro de Electron.

## 9. TrackerAgent — Fase 1 terminada

### Proyecto

Se agregó:

```text
WarframeTracker.Agent/
WarframeTracker.Agent.Tests/
```

Ambos están incluidos en `Warframe-Tracker-v2.sln`.

### Funcionalidad implementada

- Ejecutable .NET 8 para Windows.
- Host genérico y dependency injection.
- Configuración mediante `trackeragentsettings.json`.
- Logging estructurado en español.
- Detección de `Warframe.x64.exe` mediante APIs normales de .NET/Windows.
- Intervalo configurable entre 2 y 60 segundos; cinco segundos por defecto.
- Evento `WarframeStarted`.
- Evento `WarframeStopped`.
- Manejo de reemplazo de PID.
- Sin eventos duplicados mientras el mismo proceso continúa abierto.
- Conservación del estado anterior si Windows falla temporalmente al enumerar
  procesos.
- Coordinador de sesión con `CancellationTokenSource` por ejecución del juego.
- Cancelación y liberación de recursos cuando Warframe se cierra.
- Aperturas y cierres repetidos sin reiniciar el Agent.
- Apagado limpio mediante el host.
- Estados internos:

```text
Starting
Disabled
WaitingForWarframe
WarframeRunning
Stopping
Stopped
```

### Feature flags iniciales

```json
{
  "TrackerAgent": {
    "Enabled": true,
    "ProcessName": "Warframe.x64.exe",
    "ProcessDetectionIntervalSeconds": 5,
    "InventoryProviderEnabled": false,
    "EELogProviderEnabled": false,
    "ExperimentalProviderEnabled": false,
    "AutomaticSyncEnabled": false
  }
}
```

Los módulos todavía no implementados permanecen apagados.

### Pruebas realizadas

- Inicio/cierre sin eventos duplicados.
- Reemplazo de una instancia cerrada por otro PID.
- Fallo temporal al consultar procesos sin producir un falso cierre.
- Consulta real de procesos de Windows aceptando el sufijo `.exe`.
- Creación y cancelación de sesión.
- Agent desactivado sin iniciar sesión de Warframe.
- Compilación completa de la solución sin advertencias.
- Seis pruebas nuevas del Agent y seis pruebas existentes: **12/12 correctas**.
- Auditoría NuGet del Agent y tests sin vulnerabilidades conocidas.

### Ejecutar la Fase 1

```powershell
dotnet run --project .\WarframeTracker.Agent\WarframeTracker.Agent.csproj
```

Salida esperada:

```text
[Agent] Iniciado. Estado: esperando Warframe.
[Warframe] Proceso detectado. PID 1234. Sesión iniciada.
[Warframe] Proceso cerrado. PID 1234. Recursos de sesión liberados.
```

Usar `Ctrl+C` para apagarlo correctamente.

## 10. TrackerAgent — Fases 2 a 6 implementadas

### Fase 2 — Emparejamiento y comunicación con Render

Objetivo: conectar el Agent con la misma cuenta Identity de la Web sin guardar
la contraseña.

Implementado:

1. Flujo de emparejamiento mediante navegador.
2. Código temporal, de un solo uso y con expiración.
3. Dispositivo vinculado al `IdentityUser` existente.
4. Token aleatorio propio del Tracker; el servidor guardará solo su hash.
5. Token protegido localmente mediante DPAPI o Windows Credential Manager.
6. Revocación de dispositivos desde la cuenta web.
7. Rate limiting por dispositivo, usuario e IP.
8. Endpoint autenticado de estado.

Rutas propuestas:

```text
POST /api/agent/v1/pairing/start
POST /api/agent/v1/pairing/token
GET  /agent/connect
GET  /api/agent/v1/status
```

No se reutilizarán contraseñas, cookies copiadas ni una clave global.

### Fase 3 — EE.log

Se implementó un lector opcional que:

- localice `EE.log` usando `LocalApplicationData` y permita override;
- abra el archivo solo para lectura con escritura compartida;
- lea únicamente datos nuevos;
- conserve offset y líneas incompletas;
- soporte archivo inexistente, bloqueo temporal, truncamiento y rotación;
- compruebe periódicamente si existen datos nuevos sin mantener watchers;
- genere eventos internos de misión;
- muestre solo resúmenes de categoría con supresión de duplicados;
- comience al final del archivo existente para no reprocesar actividad histórica;
- no escriba líneas completas potencialmente sensibles en logs.

`EE.log` servirá principalmente como fuente de eventos. No se asumirá que
contiene un inventario completo.

### Fase 4 — Inventory Provider

Se definió:

```text
IInventoryProvider
InventoryProviderResult
```

También se extraerá el núcleo reutilizable de
`DesktopInventorySyncService` hacia un servicio sin estado. Electron y
Overwolf conservarán sus interfaces actuales y pasarán por el mismo parser,
normalizador, diff y aplicación transaccional.

El provider experimental seguro:

- permanecerá desactivado por defecto;
- será reemplazable;
- estará aislado del host principal;
- será exclusivamente de lectura;
- no usará inyección, hooks, escritura de memoria ni automatización;
- podrá fallar sin cerrar el Agent.

No se activó un método directo porque todavía no existe una fuente local
confirmada, segura y distribuible. Para QA se agregó una bandeja de entrada de
snapshots normalizados; no lee memoria ni tráfico del juego.

### Fase 5 — Snapshots y diferencias

Se agregó:

- snapshot normalizado local;
- comparación `Added`, `Removed` y `Changed`;
- reutilización del comparador actual del backend;
- hash de contenido;
- `BatchId` y secuencia monotónica por dispositivo;
- escritura local atómica;
- límites de cantidad, tamaño y antigüedad;
- pruebas de snapshots completos, parciales, desconocidos y grandes.

El servidor volverá a validar el snapshot y calculará el cambio autoritativo
antes de modificar MySQL.

### Fase 6 — Sincronización automática y modo offline

Se implementó:

- endpoints `/api/agent/v1/inventory/preview` y `apply`;
- batches idempotentes;
- cola offline local acotada;
- reintentos con backoff exponencial y jitter;
- respuesta `already_applied` tratada como éxito;
- rechazo de snapshots antiguos;
- reglas para capturas parciales y autoritativas;
- auditoría de fuente sin JSON bruto;
- rollback y pruebas de concurrencia;
- aplicación automática solamente si el usuario la habilita explícitamente.

Prioridad propuesta entre fuentes:

1. Ninguna fuente gana solamente por llamarse Overwolf o TrackerAgent.
2. La frescura, secuencia e idempotencia se validan en el servidor.
3. Solo un snapshot autoritativo puede marcar ausencias.
4. Una captura parcial únicamente agrega o actualiza.
5. Un batch antiguo nunca sobrescribe uno más reciente.
6. Los conflictos con edición manual deben mostrarse o poder bloquearse.

### Fases posteriores

- Estado básico en tray si sigue siendo necesario.
- Comunicación local opcional entre Desktop y Agent mediante named pipes.
- Instalador `WarframeTracker.Agent.Setup.exe`.
- Firma de código.
- Inicio automático configurable con Windows.
- Autoupdate únicamente después de estabilizar API, firma y rollback.

## 11. Estructura implementada y extensible

```text
WarframeTracker.Agent/
  Configuration/
  Diagnostics/
  Hosting/
  ProcessDetection/
  Providers/
    Inventory/
    EELog/
    Experimental/
  Inventory/
  Sync/
  Security/

WarframeTracker.Agent.Tests/
  Hosting/
  ProcessDetection/
  Providers/
  Inventory/
  Sync/

WarframeInventory/WarframeInventory/
  Controllers/AgentV1/
  Contracts/AgentV1/
  Models/AgentDevice.cs
  Models/InventorySyncBatch.cs
  Services/AgentAuthenticationService.cs
  Services/InventoryIngestionService.cs
```

No se moverán ni renombrarán proyectos existentes.

## 12. Prioridades generales antes de producción estable

Orden recomendado:

1. Desplegar y validar en vivo el emparejamiento Agent -> Render.
2. Confirmar una fuente de inventario local segura y distribuible.
3. Antiforgery, logout POST y autorización de páginas privadas.
4. Corregir la búsqueda en español para `bóveda`/`vaulted` y nombres de piezas.
5. Migrar páginas restantes a `IDbContextFactory`.
6. Health check real de MySQL y estado de migraciones.
7. TLS verificado entre Render y MySQL.
8. Workflow de integración continua.
9. Optimización final de imágenes, fuentes, audio, caché e idiomas.
10. Firma, instalador y distribución del Agent.

## 13. Reglas de seguridad y desarrollo

- No guardar contraseñas en Git.
- No incluir `appsettings.json`, certificados, tokens, cookies ni bases locales.
- No imprimir inventario bruto o credenciales en logs.
- No enviar credenciales de Warframe a Render.
- No usar inyección DLL, code injection, hooks invasivos o escritura de memoria.
- No modificar archivos ni memoria de Warframe.
- No automatizar gameplay.
- No acceder a las tablas ajenas dentro de `cja3651_ACNH`.
- Auditar datos antes de agregar índices o foreign keys.
- Mantener migraciones MySQL y SQLite equivalentes.
- Mantener Overwolf y Electron funcionando mientras se extrae lógica común.
- Crear cambios pequeños y comprobables.
- Ejecutar compilación y pruebas antes de avanzar de fase.
- No hacer commit ni push cuando el usuario haya pedido trabajar únicamente en
  el directorio local.

## 14. Validación recomendada en cada entrega

```powershell
dotnet restore .\Warframe-Tracker-v2.sln
dotnet build .\Warframe-Tracker-v2.sln -c Release --no-restore
dotnet test .\Warframe-Tracker-v2.sln -c Release --no-build --no-restore
dotnet list .\WarframeTracker.Agent\WarframeTracker.Agent.csproj package --vulnerable --include-transitive
```

Para Overwolf Native:

```powershell
cd .\overwolf-native
npm ci
npm run build
npm test
npm run validate
```

Electron se valida únicamente si se modifica:

```powershell
cd .\desktop-electron
npm ci
npm run typecheck
```

## 15. Documentos relacionados

- `README.md`: introducción y ejecución general.
- `docs/tracker-agent-analysis.md`: análisis detallado de la Fase 0.
- `WarframeTracker.Agent/README.md`: ejecución y QA de las Fases 1 a 6.
- `CODEX_HANDOFF.md`: contexto histórico para continuar con otra IA/equipo.
- `docs/REPOSITORY_STRUCTURE.md`: estructura del repositorio.
- `docs/ROADMAP.md`: roadmap previo del producto.
- `docs/VALIDATION.md`: validaciones históricas.
- `overwolf-native/README.md`: QA y empaquetado Native.
- `desktop-electron/README.md`: arquitectura Electron legada.

## 16. Próximo paso exacto

El siguiente paso es desplegar las migraciones y probar en vivo:

1. `Agent -> HTTPS -> Render -> IdentityUser correcto`.
2. Revocación inmediata desde `/agent/devices`.
3. Lectura de eventos reales de `EE.log` con el flag habilitado.
4. Un snapshot de QA parcial, primero con autoaplicación apagada.
5. Reintento offline e idempotencia con `AutomaticSyncEnabled=true` solamente
   después de revisar el snapshot.

La captura automática del inventario real no debe habilitarse hasta confirmar
una fuente permitida. Tray, instalador, firma y autoupdate siguen siendo fases
posteriores.

### Resultado de QA en Render

- Emparejamiento HTTPS confirmado contra Render.
- Dispositivo asociado a la cuenta correcta y token local protegido con DPAPI.
- Detección del proceso de Warframe confirmada.
- La primera transición de Repetidor no fue concluyente porque el ejecutable
  activo conservaba `EELogProviderEnabled=false`.
- La segunda transición confirmó la lectura incremental y reveló firmas
  diferenciadas `Hub + Join` y `Hub + Left/Exit/Shutdown`.
- Se agregaron eventos seguros específicos de entrada y salida de Repetidor
  para la validación final.
