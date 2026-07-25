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
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(
        Path.Combine(builder.Environment.ContentRootPath, "DataProtectionKeys")))
    .SetApplicationName("WarframeInventory");

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(
    builder.Environment.IsDevelopment() ? LogLevel.Information : LogLevel.Warning);

var configuration = builder.Configuration;
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
builder.Services.AddScoped<DataSyncService>();
builder.Services.AddScoped<UserGoalService>();
builder.Services.AddScoped<GoalAnalysisService>();
builder.Services.AddHostedService<CatalogSyncBackgroundService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseForwardedHeaders();
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
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

if (args.Contains("--sync-catalog", StringComparer.OrdinalIgnoreCase))
{
    using var scope = app.Services.CreateScope();
    await scope.ServiceProvider.GetRequiredService<DataSyncService>().SyncAllAsync();
    return;
}

app.Run();
