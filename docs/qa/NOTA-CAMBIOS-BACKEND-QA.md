# Nota técnica — cambios requeridos en el backend de Warframe Tracker

Fecha: 5 de agosto de 2026

## Contexto

El paquete portable QA originalmente iniciaba el backend ASP.NET estableciendo:

```text
WARFRAME_TRACKER_DESKTOP=1
```

En el backend compilado, esa misma bandera controlaba dos comportamientos distintos:

1. El alojamiento local requerido por Electron: URL y puerto dinámico en `127.0.0.1`.
2. La selección de SQLite (`tracker.db`) como proveedor de base de datos.

Por ello, aunque `appsettings.json` tuviera credenciales MySQL correctas, el portable ignoraba MySQL y utilizaba SQLite.

## Cambios temporales realizados en el portable QA

### 1. Uso de MySQL desde Electron

Se modificó `desktop-electron/dist/main.js` para:

- Iniciar el backend con `WARFRAME_TRACKER_DESKTOP=0`.
- Conservar el puerto local dinámico pasando explícitamente:

```text
--urls http://127.0.0.1:<puerto-dinámico>
```

- Mantener las variables necesarias para la integración de escritorio:

```text
WARFRAME_TRACKER_DATA_DIR
WARFRAME_DESKTOP_BRIDGE_KEY
```

El controlador `api/desktop-bridge` no depende directamente de `DesktopMode`; utiliza `WARFRAME_DESKTOP_BRIDGE_KEY`, por lo que la captura GEP continuó funcionando.

### 2. Migraciones faltantes en MySQL

La base remota estaba dos migraciones por detrás del binario:

```text
20260728004747_AddUserResources
20260805023308_AddEquipmentMastery
```

Se aplicaron los siguientes cambios:

- Creación de la tabla `UserResources`.
- Índice único por `UserId` y `ResourceUnique`.
- Clave foránea hacia `AspNetUsers` con borrado en cascada.
- Columnas nuevas en `UserWarframes`:
  - `Mastered`, `tinyint(1)`, no nula, valor inicial `0`.
  - `MasteryXp`, `bigint`, no nula, valor inicial `0`.
- Las mismas columnas en `UserWeapons`.
- Registro de ambas migraciones en `__EFMigrationsHistory`.

No se sobrescribieron filas existentes. Los conteos antes y después fueron:

```text
UserWarframes: 36 -> 36
UserWeapons: 118 -> 118
```

Se dejaron estas copias de seguridad en MySQL:

```text
QA_Backup_UserWarframes_before_mastery_20260805
QA_Backup_UserWeapons_before_mastery_20260805
QA_Backup_EFMigrationsHistory_20260805
```

### 3. Incompatibilidad entre reintentos y transacción manual

La configuración MySQL del backend contenía:

```csharp
mysql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
```

Pero `DesktopInventorySyncService.ApplyAsync` abre una transacción manual directamente:

```csharp
await db.Database.BeginTransactionAsync(cancellationToken);
```

Entity Framework no admite una transacción iniciada por el usuario fuera de la estrategia configurada por `EnableRetryOnFailure`. El análisis funcionaba porque era de solo lectura; al aplicar cambios, la transacción fallaba y la página ocultaba la excepción real mediante un `catch` genérico.

Para desbloquear exclusivamente la prueba QA, se extrajo el ensamblado del single-file y se creó una copia en:

```text
backend/qa-mysql/WarframeInventory.dll
```

En esa copia se eliminó únicamente la llamada IL a `EnableRetryOnFailure`. El ejecutable backend original quedó intacto.

### 4. Runtime .NET incluido

La primera variante QA ejecutaba el ensamblado mediante `dotnet`. En equipos sin .NET instalado aparecía:

```text
Error: spawn dotnet ENOENT
```

Para conservar el funcionamiento portable, se agregó un runtime privado .NET 8 x64 en:

```text
backend/qa-runtime/
```

El lanzador ahora utiliza:

```text
backend/qa-runtime/dotnet.exe
```

Así no depende de una instalación global de .NET ni del `PATH` del tester.

## Solución definitiva recomendada en el código fuente

### Separar modo de alojamiento y proveedor de base de datos

No se debe utilizar `DesktopMode` para seleccionar también SQLite. Se recomienda una configuración independiente:

```json
{
  "DesktopMode": true,
  "DatabaseProvider": "MySql"
}
```

Ejemplo:

```csharp
var desktopMode =
    builder.Configuration.GetValue<bool>("DesktopMode") ||
    Environment.GetEnvironmentVariable("WARFRAME_TRACKER_DESKTOP") == "1";

var databaseProvider =
    builder.Configuration["DatabaseProvider"] ??
    (desktopMode ? "Sqlite" : "MySql");

if (desktopMode)
{
    builder.WebHost.UseUrls(
        Environment.GetEnvironmentVariable("WARFRAME_TRACKER_URL") ??
        "http://127.0.0.1:43127");
}

if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
{
    // Registrar DesktopApplicationDbContext con SQLite.
}
else if (databaseProvider.Equals("MySql", StringComparison.OrdinalIgnoreCase))
{
    // Registrar ApplicationDbContext con MySQL.
}
else
{
    throw new InvalidOperationException(
        $"Proveedor de base de datos desconocido: {databaseProvider}");
}
```

Electron podría enviar:

```text
WARFRAME_TRACKER_DESKTOP=1
DatabaseProvider=MySql
```

De esta forma se conserva el alojamiento para Electron sin forzar SQLite.

### Corregir correctamente la transacción con reintentos

En producción es preferible conservar `EnableRetryOnFailure` y ejecutar toda la unidad transaccional mediante la estrategia de EF Core:

```csharp
public async Task<DesktopApplyResult> ApplyAsync(
    string userId,
    DesktopInventoryPreview preview,
    CancellationToken cancellationToken = default)
{
    await using var db = await _dbFactory.CreateDbContextAsync(cancellationToken);
    var strategy = db.Database.CreateExecutionStrategy();

    return await strategy.ExecuteAsync(async () =>
    {
        await using var transaction =
            await db.Database.BeginTransactionAsync(cancellationToken);

        var changed = 0;
        changed += await ApplyWarframesAsync(db, userId, preview, cancellationToken);
        changed += await ApplyWeaponsAsync(db, userId, preview, cancellationToken);
        changed += await ApplyModsAsync(db, userId, preview, cancellationToken);
        changed += await ApplyRelicsAsync(db, userId, preview, cancellationToken);
        changed += await ApplyComponentsAsync(db, userId, preview, cancellationToken);
        changed += await ApplyResourcesAsync(db, userId, preview, cancellationToken);

        ApplyAccount(db, userId, preview.Account);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new DesktopApplyResult(changed, DateTime.UtcNow);
    });
}
```

Importante: cualquier estado en memoria que se modifique después del `Commit`, como limpiar `_capture`, debe hacerse una sola vez después de que `ExecuteAsync` termine correctamente, no dentro de una sección que pueda repetirse.

Para una compilación QA excepcional también puede omitirse `EnableRetryOnFailure`, que fue el parche utilizado, pero no es la solución recomendada para producción.

### Aplicar migraciones de forma controlada

El backend MySQL no ejecutaba `Database.MigrateAsync()` al iniciar, mientras que la rama SQLite sí lo hacía. Debe definirse una política explícita:

- Aplicar migraciones durante despliegue con `dotnet ef database update`; o
- Ejecutarlas en un proceso administrativo controlado; o
- Ejecutar `MigrateAsync()` al arrancar solo si el entorno y los permisos lo permiten.

No conviene que cada portable QA intente migrar automáticamente una base compartida.

### Mejorar el registro de excepciones

La página `DesktopSync` captura cualquier excepción no controlada y muestra solamente:

```text
Ocurrió un error inesperado. El inventario no fue modificado.
```

Debe conservarse un mensaje seguro para el usuario, pero registrar la excepción completa en backend:

```csharp
catch (Exception exception)
{
    logger.LogError(exception, "Falló la aplicación del inventario GEP para {UserId}", userId);
    message = "Ocurrió un error inesperado. El inventario no fue modificado.";
    severity = Severity.Error;
}
```

No registrar la captura JSON original, credenciales, claves GEP ni contraseñas.

## Pruebas mínimas para la nueva compilación

1. Ejecutar el portable en un Windows 11 x64 sin .NET instalado.
2. Confirmar que el backend inicia y `/api/desktop-bridge/health` devuelve HTTP 200.
3. Confirmar que se utiliza MySQL cuando `DatabaseProvider=MySql`.
4. Iniciar sesión con un usuario QA.
5. Recibir una captura GEP.
6. Ejecutar “Analizar inventario”.
7. Ejecutar “Aplicar cambios”.
8. Confirmar que la transacción se completa o se revierte íntegramente.
9. Verificar Warframes, armas, maestría, mods, reliquias, componentes, recursos y `AlecaAccountSnapshots`.
10. Simular un fallo durante `SaveChangesAsync` y confirmar que no queden escrituras parciales.
11. Comprobar que un reintento no aplique dos veces cambios ni limpie prematuramente la captura.

## Seguridad

El paquete QA contiene credenciales MySQL en texto plano dentro de `appsettings.json`. Deben considerarse expuestas a cualquier tester que reciba el ZIP. Para futuras versiones se recomienda:

- Usar un usuario MySQL exclusivo de QA con permisos mínimos.
- Restringirlo por red/IP cuando sea posible.
- Rotar la contraseña al terminar la prueba.
- No reutilizar credenciales de producción.

## Resultado verificado del parche QA

- Conexión MySQL funcional.
- Endpoint local HTTP 200.
- Captura y análisis GEP funcionales después de actualizar el esquema.
- `EnableRetryOnFailure` eliminado únicamente en el ensamblado QA.
- Runtime .NET 8 privado incluido para equipos sin instalación global.
- Backend original y respaldos conservados.
