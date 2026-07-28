using System.IO.Compression;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using WarframeInventory.Data;
using WarframeInventory.Services;

var builder = WebApplication.CreateBuilder(args);
var desktopMode = builder.Configuration.GetValue<bool>("DesktopMode")
                  || Environment.GetEnvironmentVariable("WARFRAME_TRACKER_DESKTOP") == "1";
var localDataRoot = Environment.GetEnvironmentVariable("WARFRAME_TRACKER_DATA_DIR");
if (desktopMode)
{
    localDataRoot = string.IsNullOrWhiteSpace(localDataRoot)
        ? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WarframeTracker")
        : Path.GetFullPath(localDataRoot);
    Directory.CreateDirectory(localDataRoot);
    builder.WebHost.UseUrls(
        Environment.GetEnvironmentVariable("WARFRAME_TRACKER_URL")
        ?? "http://127.0.0.1:43127");
}

builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(localDataRoot ?? builder.Environment.ContentRootPath, "DataProtectionKeys")))
    .SetApplicationName("WarframeInventory");

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
if (desktopMode)
{
    var sqlitePath = Path.Combine(localDataRoot!, "tracker.db");
    builder.Services.AddPooledDbContextFactory<DesktopApplicationDbContext>(options =>
        options.UseSqlite($"Data Source={sqlitePath};Cache=Shared"));
    builder.Services.AddSingleton<IDbContextFactory<ApplicationDbContext>,
        DesktopDbContextFactoryAdapter>();
}
else
{
    var host = configuration["ConnectionStrings:DB_HOST"] ?? "localhost";
    var user = configuration["ConnectionStrings:DB_USER"]
               ?? throw new InvalidOperationException("Falta ConnectionStrings:DB_USER");
    var pass = configuration["ConnectionStrings:DB_PASS"]
               ?? throw new InvalidOperationException("Falta ConnectionStrings:DB_PASS");
    var dbName = configuration["ConnectionStrings:DB_NAME"]
                 ?? throw new InvalidOperationException("Falta ConnectionStrings:DB_NAME");
    var connectionString =
        $"server={host};port=3306;database={dbName};user={user};password={pass};" +
        "SslMode=None;AllowPublicKeyRetrieval=True;Pooling=True;MinimumPoolSize=0;MaximumPoolSize=50;";
    var serverVersion = ServerVersion.AutoDetect(connectionString);
    builder.Services.AddPooledDbContextFactory<ApplicationDbContext>(options =>
        options.UseMySql(connectionString, serverVersion, mysql =>
            mysql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null)));
}
// Compatibilidad temporal con componentes existentes. Cada circuito obtiene un contexto;
// las páginas nuevas deben preferir IDbContextFactory.
builder.Services.AddScoped(sp =>
    sp.GetRequiredService<IDbContextFactory<ApplicationDbContext>>().CreateDbContext());

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
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    options.ExpireTimeSpan = TimeSpan.FromDays(14);
    options.SlidingExpiration = true;
    options.LoginPath = "/auth/login";
    options.AccessDeniedPath = "/auth/login";
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

if (!app.Environment.IsDevelopment() && !desktopMode)
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

if (!desktopMode)
{
    app.UseForwardedHeaders();
    app.UseHttpsRedirection();
}
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
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

if (desktopMode)
{
    using var desktopScope = app.Services.CreateScope();
    var desktopDatabase = desktopScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await desktopDatabase.Database.MigrateAsync();
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
