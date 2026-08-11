using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using MySqlConnector;
using WarframeInventory.Data;
using WarframeInventory.Services;

var platformPort = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(platformPort))
{
    if (!int.TryParse(platformPort, out var parsedPort) || parsedPort is < 1 or > 65535)
        throw new InvalidOperationException("PORT debe ser un puerto TCP válido.");

    // The .NET container image defines HTTP_PORTS=8080. Replacing that value
    // before the host is created avoids conflicting URL sources on Render.
    Environment.SetEnvironmentVariable("ASPNETCORE_HTTP_PORTS", parsedPort.ToString());
}

var builder = WebApplication.CreateBuilder(args);
var designTime = EF.IsDesignTime;
var runningOnRender = string.Equals(
    Environment.GetEnvironmentVariable("RENDER"),
    "true",
    StringComparison.OrdinalIgnoreCase);
var behindReverseProxy = runningOnRender
                         || Environment.GetEnvironmentVariable(
                             "WARFRAME_TRACKER_BEHIND_PROXY") == "1";
var desktopMode = builder.Configuration.GetValue<bool>("DesktopMode")
                  || Environment.GetEnvironmentVariable("WARFRAME_TRACKER_DESKTOP") == "1";
var databaseProvider = Environment.GetEnvironmentVariable("WARFRAME_TRACKER_DATABASE_PROVIDER")
                       ?? builder.Configuration["DatabaseProvider"]
                       ?? (desktopMode ? "Sqlite" : "MySql");
var useSqlite = databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase);
var useMySql = databaseProvider.Equals("MySql", StringComparison.OrdinalIgnoreCase);
if (!useSqlite && !useMySql)
    throw new InvalidOperationException(
        "DatabaseProvider debe ser 'Sqlite' o 'MySql'.");
var localDataRoot = Environment.GetEnvironmentVariable("WARFRAME_TRACKER_DATA_DIR");
if (desktopMode || useSqlite)
{
    localDataRoot = string.IsNullOrWhiteSpace(localDataRoot)
        ? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WarframeTracker")
        : Path.GetFullPath(localDataRoot);
    Directory.CreateDirectory(localDataRoot);
}
if (desktopMode)
{
    builder.WebHost.UseUrls(
        Environment.GetEnvironmentVariable("WARFRAME_TRACKER_URL")
        ?? "http://127.0.0.1:43127");
    // `dotnet run -c Release` does not expose static web assets from referenced
    // packages by default. The local Native/Electron host still needs assets
    // such as MudBlazor.min.js and MudBlazor.min.css from the NuGet package.
    builder.WebHost.UseStaticWebAssets();
}

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(
    builder.Environment.IsDevelopment() ? LogLevel.Information : LogLevel.Warning);
// El token público viaja como parámetro de consulta hacia AlecaFrame. Evitamos que
// el registrador automático de HttpClient escriba esa URL completa en la consola.
builder.Logging.AddFilter(
    "System.Net.Http.HttpClient.AlecaFrameRelicClient",
    LogLevel.Warning);

var configuration = builder.Configuration;
if (useSqlite)
{
    var sqlitePath = Path.Combine(localDataRoot!, "tracker.db");
    builder.Services.AddPooledDbContextFactory<DesktopApplicationDbContext>(options =>
        options.UseSqlite($"Data Source={sqlitePath};Cache=Shared"));
    builder.Services.AddSingleton<IDbContextFactory<ApplicationDbContext>,
        DesktopDbContextFactoryAdapter>();
}
else
{
    var host = Environment.GetEnvironmentVariable("WARFRAME_TRACKER_DB_HOST")
               ?? configuration["ConnectionStrings:DB_HOST"]
               ?? (designTime ? "localhost" :
                   throw new InvalidOperationException("Falta WARFRAME_TRACKER_DB_HOST."));
    var port = Environment.GetEnvironmentVariable("WARFRAME_TRACKER_DB_PORT")
               ?? configuration["ConnectionStrings:DB_PORT"] ?? "3306";
    var user = Environment.GetEnvironmentVariable("WARFRAME_TRACKER_DB_USER")
               ?? configuration["ConnectionStrings:DB_USER"]
               ?? (designTime ? "design" :
                   throw new InvalidOperationException("Falta WARFRAME_TRACKER_DB_USER."));
    var pass = Environment.GetEnvironmentVariable("WARFRAME_TRACKER_DB_PASS")
               ?? configuration["ConnectionStrings:DB_PASS"]
               ?? (designTime ? "design" :
                   throw new InvalidOperationException("Falta WARFRAME_TRACKER_DB_PASS."));
    var dbName = Environment.GetEnvironmentVariable("WARFRAME_TRACKER_DB_NAME")
                 ?? configuration["ConnectionStrings:DB_NAME"]
                 ?? (designTime ? "warframe_design" :
                     throw new InvalidOperationException("Falta WARFRAME_TRACKER_DB_NAME."));
    if (!uint.TryParse(port, out var databasePort) || databasePort > ushort.MaxValue)
        throw new InvalidOperationException("WARFRAME_TRACKER_DB_PORT no es válido.");
    var sslModeValue = Environment.GetEnvironmentVariable("WARFRAME_TRACKER_DB_SSL_MODE")
                       ?? configuration["ConnectionStrings:DB_SSL_MODE"]
                       ?? "Preferred";
    if (!Enum.TryParse<MySqlSslMode>(sslModeValue, true, out var sslMode))
        throw new InvalidOperationException(
            "WARFRAME_TRACKER_DB_SSL_MODE debe ser un modo SSL válido de MySQL.");

    var connectionString = new MySqlConnectionStringBuilder
    {
        Server = host,
        Port = databasePort,
        UserID = user,
        Password = pass,
        Database = dbName,
        SslMode = sslMode,
        AllowPublicKeyRetrieval = true,
        Pooling = true,
        MinimumPoolSize = 0,
        MaximumPoolSize = 50
    }.ConnectionString;
    var serverVersion = designTime
        ? new MySqlServerVersion(new Version(8, 0, 0))
        : ServerVersion.AutoDetect(connectionString);
    builder.Services.AddPooledDbContextFactory<ApplicationDbContext>(options =>
        options.UseMySql(connectionString, serverVersion, mysql =>
            mysql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)));
}
// Compatibilidad temporal con componentes existentes. Cada circuito obtiene un contexto;
// las páginas nuevas deben preferir IDbContextFactory.
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

var dataProtection = builder.Services.AddDataProtection()
    .SetApplicationName("WarframeInventory");
if (builder.Environment.IsProduction() && !useSqlite)
{
    var certificateBase64 = Environment.GetEnvironmentVariable(
        "WARFRAME_TRACKER_DP_CERT_BASE64");
    var certificatePassword = Environment.GetEnvironmentVariable(
        "WARFRAME_TRACKER_DP_CERT_PASSWORD");
    if (string.IsNullOrWhiteSpace(certificateBase64)
        || string.IsNullOrWhiteSpace(certificatePassword))
    {
        throw new InvalidOperationException(
            "Producción requiere WARFRAME_TRACKER_DP_CERT_BASE64 y " +
            "WARFRAME_TRACKER_DP_CERT_PASSWORD para proteger las claves de sesión.");
    }

    X509Certificate2 keyEncryptionCertificate;
    try
    {
        keyEncryptionCertificate = new X509Certificate2(
            Convert.FromBase64String(certificateBase64),
            certificatePassword,
            X509KeyStorageFlags.EphemeralKeySet);
    }
    catch (Exception exception) when (
        exception is FormatException or CryptographicException)
    {
        throw new InvalidOperationException(
            "El certificado de Data Protection no es un PKCS#12 válido.",
            exception);
    }

    if (!keyEncryptionCertificate.HasPrivateKey)
        throw new InvalidOperationException(
            "El certificado de Data Protection debe incluir su clave privada.");

    dataProtection
        .PersistKeysToDbContext<ApplicationDbContext>()
        .ProtectKeysWithCertificate(keyEncryptionCertificate);
}
else
{
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(
            localDataRoot ?? builder.Environment.ContentRootPath,
            "DataProtectionKeys")));
}

if (behindReverseProxy)
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                                   | ForwardedHeaders.XForwardedProto;
        options.ForwardLimit = 1;
        // Render's service port is reachable only through its edge proxy. Its
        // proxy IP ranges aren't stable, so the trusted boundary is the Render
        // network rather than a hard-coded address list.
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();
    });
}

builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequiredLength = 10;
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    options.User.RequireUniqueEmail = true;
    options.User.AllowedUserNameCharacters =
        "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-._";
})
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddScoped<AuthenticationStateProvider,
    RevalidatingIdentityAuthenticationStateProvider<IdentityUser>>();
builder.Services.AddCascadingAuthenticationState();
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    // The production site is embedded by the Overwolf Native shell. Its
    // authentication cookie therefore needs explicit cross-site iframe
    // support and must only travel over HTTPS. Local QA remains HTTP loopback.
    options.Cookie.SameSite = desktopMode ? SameSiteMode.Lax : SameSiteMode.None;
    options.Cookie.SecurePolicy = desktopMode
        ? CookieSecurePolicy.SameAsRequest
        : CookieSecurePolicy.Always;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.LoginPath = "/auth/login";
    options.AccessDeniedPath = "/auth/login";
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        else
            context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api"))
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
        else
            context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, cancellationToken) =>
    {
        context.HttpContext.Response.ContentType = "text/plain; charset=utf-8";
        await context.HttpContext.Response.WriteAsync(
            "Demasiados intentos en poco tiempo. Espera un minuto y vuelve a intentarlo.",
            cancellationToken);
    };
    options.AddPolicy("auth", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 8,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
    options.AddPolicy("native-inventory", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 15,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<BrotliCompressionProviderOptions>(x => x.Level = CompressionLevel.Fastest);
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<CatalogCacheService>();
builder.Services.AddSingleton<DesktopInventorySyncService>();
builder.Services.AddSingleton<NativeInventorySyncService>();
builder.Services.AddMudServices();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor(options =>
{
    options.DetailedErrors = builder.Environment.IsDevelopment();
    options.DisconnectedCircuitRetentionPeriod = TimeSpan.FromMinutes(2);
});
builder.Services.AddControllers();
builder.Services.AddHttpClient<WarframeApiService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(2);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("WarframeInventory/2.0");
});
builder.Services.AddHttpClient<ResourceCatalogService>(client =>
{
    client.BaseAddress = new Uri("https://api.warframestat.us/");
    client.Timeout = TimeSpan.FromMinutes(2);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("WarframeInventory/2.0");
});
builder.Services.AddScoped<DataSyncService>();
builder.Services.AddScoped<UserGoalService>();
builder.Services.AddScoped<GoalAnalysisService>();
builder.Services.AddScoped<FarmPlannerService>();
builder.Services.AddScoped<BuildableAnalysisService>();
builder.Services.AddScoped<CommandCenterService>();
builder.Services.AddScoped<RelicIntelligenceService>();
builder.Services.AddScoped<RelicLinkService>();
builder.Services.AddHttpClient<MarketPriceService>(client =>
{
    client.BaseAddress = new Uri("https://api.warframe.market/v2/");
    client.Timeout = TimeSpan.FromSeconds(8);
    client.DefaultRequestHeaders.Add("Platform", "pc");
    client.DefaultRequestHeaders.Add("Crossplay", "true");
    client.DefaultRequestHeaders.Add("Language", "es");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("WarframeInventory/2.0");
});
builder.Services.AddScoped<UniversalSearchService>();
builder.Services.AddScoped<ComparisonService>();
builder.Services.AddScoped<InventoryToolsService>();
builder.Services.AddScoped<BuildService>();
builder.Services.AddScoped<InventoryAdvancedService>();
builder.Services.AddHttpClient<AlecaFrameRelicClient>(client =>
{
    client.BaseAddress = new Uri("https://stats.alecaframe.com/");
    client.Timeout = TimeSpan.FromSeconds(20);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("WarframeInventory/2.0");
});
builder.Services.AddScoped<RelicSyncService>();
builder.Services.AddHttpClient<WorldstateService>(client =>
{
    client.BaseAddress = new Uri("https://api.warframestat.us/");
    client.Timeout = TimeSpan.FromSeconds(8);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("WarframeInventory/2.0");
});
builder.Services.AddHostedService<CatalogSyncBackgroundService>();

var app = builder.Build();

if (behindReverseProxy)
    app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment() && !desktopMode)
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

if (!desktopMode && !behindReverseProxy)
    app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers.CacheControl =
            "public,max-age=604800,immutable";
    }
});
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .AllowAnonymous();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

var migrationRequested = args.Contains("--migrate-database", StringComparer.OrdinalIgnoreCase)
                         || configuration.GetValue<bool>("ApplyDatabaseMigrations")
                         || Environment.GetEnvironmentVariable(
                             "WARFRAME_TRACKER_APPLY_MIGRATIONS") == "1";
// La base local pertenece exclusivamente a esta instalación y puede actualizarse sola.
// MySQL puede ser compartido: solo se migra mediante una orden administrativa explícita.
if (useSqlite || migrationRequested)
{
    using var migrationScope = app.Services.CreateScope();
    var migrationDatabase = migrationScope.ServiceProvider
        .GetRequiredService<ApplicationDbContext>();
    await migrationDatabase.Database.MigrateAsync();
}

if (args.Contains("--migrate-database", StringComparer.OrdinalIgnoreCase))
{
    Console.WriteLine($"Migraciones aplicadas con el proveedor {databaseProvider}.");
    return;
}

if (desktopMode)
{
    using var desktopScope = app.Services.CreateScope();
    var desktopDatabase = desktopScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (args.Contains("--validate-desktop", StringComparer.OrdinalIgnoreCase))
    {
        Console.WriteLine($"Modo escritorio validado con {desktopDatabase.Database.ProviderName}.");
        return;
    }
    var validateInventoryIndex = Array.FindIndex(
        args,
        value => value.Equals("--validate-desktop-inventory",
            StringComparison.OrdinalIgnoreCase));
    if (validateInventoryIndex >= 0)
    {
        if (validateInventoryIndex + 1 >= args.Length)
            throw new InvalidOperationException(
                "Debes indicar el archivo JSON que quieres validar.");
        var inventoryPath = Path.GetFullPath(args[validateInventoryIndex + 1]);
        var inventoryJson = await File.ReadAllTextAsync(inventoryPath);
        var receipt = desktopScope.ServiceProvider
            .GetRequiredService<DesktopInventorySyncService>()
            .Stage(inventoryJson, "command-line-validation");
        Console.WriteLine(
            $"Inventario válido: {receipt.DistinctItems} objetos, " +
            $"{receipt.TotalQuantity} unidades, cobertura " +
            $"{(receipt.IsAuthoritative ? "completa" : "parcial")}.");
        return;
    }
}

var resetPasswordIndex = Array.FindIndex(
    args,
    value => value.Equals("--reset-password", StringComparison.OrdinalIgnoreCase));
if (resetPasswordIndex >= 0)
{
    if (resetPasswordIndex + 1 >= args.Length)
        throw new InvalidOperationException("Debes indicar el nombre de usuario.");

    var resetUserName = args[resetPasswordIndex + 1];
    var resetPassword = Environment.GetEnvironmentVariable("WARFRAME_RESET_PASSWORD");
    if (string.IsNullOrWhiteSpace(resetPassword))
        throw new InvalidOperationException(
            "Define WARFRAME_RESET_PASSWORD para realizar el restablecimiento.");

    using var scope = app.Services.CreateScope();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
    var identityUser = await userManager.FindByNameAsync(resetUserName)
        ?? throw new InvalidOperationException($"No existe el usuario '{resetUserName}'.");
    var token = await userManager.GeneratePasswordResetTokenAsync(identityUser);
    var resetResult = await userManager.ResetPasswordAsync(identityUser, token, resetPassword);
    if (!resetResult.Succeeded)
    {
        var errors = string.Join("; ", resetResult.Errors.Select(error => error.Description));
        throw new InvalidOperationException($"No se pudo restablecer la contraseña: {errors}");
    }

    await userManager.SetLockoutEndDateAsync(identityUser, null);
    await userManager.ResetAccessFailedCountAsync(identityUser);
    Console.WriteLine($"Contraseña restablecida para '{identityUser.UserName}'.");
    return;
}

if (args.Contains("--sync-catalog", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DataSyncService>().SyncAllAsync();
    return;
}

app.Run();
