# Contexto de continuidad para Codex

## Objetivo del proyecto

Warframe Tracker es una aplicación ASP.NET Core 8 + Blazor Server + MudBlazor
que permite explorar públicamente Warframes, armas, mods y reliquias. Los
usuarios autenticados pueden registrar propiedad y cantidades de inventario.

## Estado actual

- Rama principal: `main`.
- Base compartida: `cja3651_ACNH`.
- No modificar tablas ajenas a Warframe.
- Compilación Release verificada con 0 errores y 0 advertencias.
- Rutas `/`, `/warframes`, `/weapons`, `/mods`, `/relics`, `/auth/login` y
  `/auth/register` verificadas con HTTP 200.
- .NET SDK utilizado: 8.0.423.
- Las migraciones están aplicadas hasta
  `20260728001253_AddAlecaAccountSnapshot`.
- Ocho filas de inventario que referenciaban usuarios inexistentes fueron
  preservadas en `OrphanedWarframeInventory`; no se perdió el contenido.

## Tablas pertenecientes a Warframe

Catálogo:

- `Warframes`
- `Weapons`
- `Mods`
- `Relics`
- `RelicRewards`
- `DataSyncStates`
- `RelicSyncProfiles`
- `AlecaAccountSnapshots`

Inventario:

- `UserWarframes`
- `UserWeapons`
- `UserMods`
- `UserRelics`
- `UserComponents`
- `OrphanedWarframeInventory`

La aplicación también usa las tablas `AspNet*` de Identity y
`__EFMigrationsHistory`. No tocar las demás tablas de `cja3651_ACNH`.

## Decisiones implementadas

- Catálogo público; edición de inventario disponible solo al iniciar sesión.
- Sincronización en segundo plano como máximo cada 24 horas.
- En Render gratuito, si la instancia está suspendida, sincroniza al despertar
  cuando corresponda.
- La sincronización descarga los cuatro catálogos en paralelo y realiza upsert
  desde diccionarios, evitando una consulta SQL por elemento.
- Las recompensas de reliquias están normalizadas en `RelicRewards`; se mantiene
  `RewardsJson` temporalmente por compatibilidad y respaldo.
- `AddPooledDbContextFactory` para trabajos cortos y conexiones agrupadas.
- Caché en memoria para métricas del catálogo, invalidada tras sincronización.
- Búsquedas SQL con debounce y `EF.Functions.Like`.
- Imágenes lazy, async decode, dimensiones reservadas y placeholder local.
- Diseño oscuro futurista inspirado en Orokin/Tenno mediante CSS nativo y
  MudBlazor. No se añadió Tailwind para evitar peso y colisiones.
- Se eliminó el controlador de autenticación API duplicado.
- Login con rate limiting, lockout, email único y contraseña mínima reforzada.
- Registro y acceso completamente en español, con errores específicos por
  campo y conservación del usuario/correo válidos tras una validación fallida.
- `WarframeSpanishText` traduce al presentar zonas, tipos de misión, rarezas y
  componentes de reliquias. Las claves originales de la API permanecen en la
  base para no romper relaciones, búsquedas ni enlaces de Warframe Market.
- Nunca volver a registrar cookies, tokens o cadenas de conexión.
- Sincronización de reliquias mediante el token público con permiso `Relics`
  de AlecaFrame. El token se cifra con ASP.NET Core Data Protection, nunca se
  imprime en logs y no se solicitan credenciales de Warframe.
- La importación externa siempre crea una vista previa y solo aplica las filas
  confirmadas. Respuestas vacías, formatos inválidos o catálogos con demasiadas
  entradas desconocidas se bloquean sin modificar el inventario.
- El cliente AlecaFrame corrige una inconsistencia de su formato actual: el
  encabezado puede contar variantes que luego Aleca omite parcialmente. Esos
  registros de 8 bytes se saltan, el resto se recupera y la vista previa entra
  en modo seguro, sin poner reliquias ausentes en cero.
- Algunas versiones también cuentan registros que luego omiten por completo.
  La diferencia se contabiliza como omitida, conservando el mismo modo seguro.
- `ApplyAsync` captura fallos inesperados, deja que la transacción se revierta,
  registra el tipo y mensaje raíz en `LastError` y no expone detalles SQL en la
  interfaz.
- El mismo enlace público puede sincronizar el último perfil compartido por
  AlecaFrame: créditos, Endo, ducados, Aya, platino, rango de maestría,
  porcentaje de colección y reliquias abiertas. Se guarda un único snapshot
  confirmado por usuario; no se persiste el historial de intercambios.
- La API pública de AlecaFrame no expone la colección completa de Warframes,
  armas, mods ni recursos individuales. La interfaz lo informa explícitamente
  y no simula esos datos.

## Configuración local

`appsettings.json` y `appsettings.Development.json` están ignorados por Git y
deben recrearse en el nuevo PC. Usar `appsettings.example.json` como plantilla.
No escribir contraseñas en commits, documentación o mensajes.

Variables requeridas en Render:

```text
ConnectionStrings__DB_HOST
ConnectionStrings__DB_USER
ConnectionStrings__DB_PASS
ConnectionStrings__DB_NAME
ASPNETCORE_FORWARDEDHEADERS_ENABLED=true
```

## Puesta en marcha en un PC nuevo

```powershell
git clone https://github.com/infacundo2/Warframe-Tracker-v2.git
cd Warframe-Tracker-v2
Copy-Item WarframeInventory/WarframeInventory/appsettings.example.json `
  WarframeInventory/WarframeInventory/appsettings.json
# Completar las credenciales locales sin subir el archivo.
dotnet restore Warframe-Tracker-v2.sln
dotnet build Warframe-Tracker-v2.sln -c Release
dotnet run --project WarframeInventory/WarframeInventory/WarframeInventory.csproj
```

No volver a ejecutar `EnsureCreated`. Para cambios futuros:

```powershell
dotnet ef migrations add NombreMigracion `
  --project WarframeInventory/WarframeInventory/WarframeInventory.csproj
dotnet ef database update `
  --project WarframeInventory/WarframeInventory/WarframeInventory.csproj
```

Auditar duplicados y referencias antes de cualquier migración con índices
únicos o claves foráneas.

## Detalle pendiente conocido

La sincronización contra `https://api.warframestat.us` no pudo completarse en
el sandbox Windows original por un error local de credenciales TLS/Schannel.
El código compila y el fallo se registra de manera segura en `DataSyncStates`.
En el nuevo PC o en Render se debe verificar que una ejecución complete y
pueble `RelicRewards`. No borrar los catálogos actuales si la API falla.

Un conector propio de inventario completo no puede ejecutarse dentro del
navegador. La vía técnicamente viable es una extensión Overwolf propia que lea
`match_info.inventory` y lo envíe solo a `localhost`; para cargar una extensión
sin publicar, Overwolf exige una cuenta de desarrollador autorizada. No usar
lectura de memoria, interceptación de red ni APIs privadas de Warframe.

Overwolf también documenta GEP para aplicaciones Electron y lista Warframe con
`match_info.inventory`. Una futura versión de escritorio puede conservar el
backend ASP.NET local dentro de una carcasa Electron y sustituir AlecaFrame por
un paquete GEP propio. El proveedor de `match_info` seguiría siendo Overwolf;
no intentar replicarlo leyendo memoria o tráfico del juego.

## Archivos clave

- `Program.cs`: composición de servicios, seguridad, compresión y middleware.
- `Services/DataSyncService.cs`: upsert optimizado.
- `Services/CatalogSyncBackgroundService.cs`: programación cada 24 horas.
- `Services/WarfarmeApiService.cs`: descarga y parsing de la API.
- `Services/WarframeSpanishText.cs`: traducción segura de datos residuales en
  inglés sin modificar identificadores técnicos.
- `Data/ApplicationDbContext.cs`: modelo e índices.
- `Migrations/20260727092214_AddRelicSyncProfile.cs`: migración más reciente.
- `Services/AlecaFrameRelicClient.cs`: descarga y decodificación binaria segura.
- `Services/RelicSyncService.cs`: vista previa, protección del token y aplicación.
- `Pages/RelicSync.razor`: flujo de conexión y confirmación del usuario.
- `Shared/MainLayout.razor` y `wwwroot/css/site.css`: diseño principal.
- `render.yaml` y `Dockerfile`: despliegue.

## Instrucción sugerida para una nueva sesión

> Lee completamente `CODEX_HANDOFF.md`, revisa `git status`, compila primero y
> continúa desde el estado actual. No modifiques tablas ajenas a Warframe en
> `cja3651_ACNH`, no expongas secretos y no uses `EnsureCreated`. Antes de
> aplicar migraciones, audita los datos y garantiza que sean reversibles o que
> preserven cualquier fila afectada.
