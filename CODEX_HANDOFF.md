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
- La migración `20260724190740_OptimizeWarframeSchema` ya fue aplicada a la BD.
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
- Nunca volver a registrar cookies, tokens o cadenas de conexión.

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

## Archivos clave

- `Program.cs`: composición de servicios, seguridad, compresión y middleware.
- `Services/DataSyncService.cs`: upsert optimizado.
- `Services/CatalogSyncBackgroundService.cs`: programación cada 24 horas.
- `Services/WarfarmeApiService.cs`: descarga y parsing de la API.
- `Data/ApplicationDbContext.cs`: modelo e índices.
- `Migrations/20260724190740_OptimizeWarframeSchema.cs`: migración aplicada.
- `Shared/MainLayout.razor` y `wwwroot/css/site.css`: diseño principal.
- `render.yaml` y `Dockerfile`: despliegue.

## Instrucción sugerida para una nueva sesión

> Lee completamente `CODEX_HANDOFF.md`, revisa `git status`, compila primero y
> continúa desde el estado actual. No modifiques tablas ajenas a Warframe en
> `cja3651_ACNH`, no expongas secretos y no uses `EnsureCreated`. Antes de
> aplicar migraciones, audita los datos y garantiza que sean reversibles o que
> preserven cualquier fila afectada.
