using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using WarframeInventory.Data;
using WarframeInventory.Services;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net;

// =======================================================
// 🔹 CONFIGURACIÓN PRINCIPAL
// =======================================================
var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.SetMinimumLevel(LogLevel.Debug);

Console.ForegroundColor = ConsoleColor.Cyan;
Console.WriteLine("========== INICIO DE CONFIGURACIÓN ==========");
Console.ResetColor();

// =======================================================
// 🔹 VARIABLES DE ENTORNO Y CONEXIÓN A BASE DE DATOS
// =======================================================
var host = Environment.GetEnvironmentVariable("DB_HOST") ?? "localhost";
var user = Environment.GetEnvironmentVariable("DB_USER") ?? "root";
var pass = Environment.GetEnvironmentVariable("DB_PASS") ?? "";
var db   = Environment.GetEnvironmentVariable("DB_NAME") ?? "warframe_inventory";

var connectionString =
    $"server={host};port=3306;database={db};user={user};password={pass};SslMode=None;AllowPublicKeyRetrieval=True;";

Console.WriteLine($"🧩 DB Host: {host}");
Console.WriteLine($"🧩 DB User: {user}");
Console.WriteLine($"🧩 DB Name: {db}");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

try
{
    using var testCtx = new ApplicationDbContext(
        new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
        .Options);
    testCtx.Database.OpenConnection();
    Console.ForegroundColor = ConsoleColor.Green;
    Console.WriteLine("✅ Conexión a MySQL verificada correctamente.");
    testCtx.Database.CloseConnection();
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"❌ Error conectando a la base de datos: {ex.Message}");
}
Console.ResetColor();

// =======================================================
// 🔹 IDENTITY (usuarios y autenticación)
// =======================================================
builder.Services.AddDefaultIdentity<IdentityUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
})
.AddEntityFrameworkStores<ApplicationDbContext>();

// =======================================================
// 🔹 AUTENTICACIÓN EN BLAZOR
// =======================================================
builder.Services.AddScoped<AuthenticationStateProvider,
    RevalidatingIdentityAuthenticationStateProvider<IdentityUser>>();
builder.Services.AddCascadingAuthenticationState();

// =======================================================
// 🔹 CONFIGURACIÓN DE COOKIES
// =======================================================
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.LoginPath = "/auth/login";
    options.AccessDeniedPath = "/";
});

Console.WriteLine("🍪 Cookies configuradas con SameSite=None y Secure=Always");

// =======================================================
// 🔹 SERVICIOS PERSONALIZADOS
// =======================================================
builder.Services.AddMudServices();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddControllers();
builder.Services.AddHttpClient<WarframeApiService>();
builder.Services.AddScoped<DataSyncService>();

Console.WriteLine("⚙️ Servicios registrados correctamente.");

// =======================================================
// 🔹 CONSTRUCCIÓN DE APP
// =======================================================
var app = builder.Build();
Console.WriteLine($"🌍 Entorno actual: {app.Environment.EnvironmentName}");
Console.WriteLine($"📁 Raíz de contenido: {app.Environment.ContentRootPath}");

// =======================================================
// 🔹 MIDDLEWARE PIPELINE
// =======================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    Console.WriteLine("🧱 Modo Producción: HSTS habilitado.");
}
else
{
    Console.WriteLine("💻 Modo Desarrollo activo.");
}

// HTTPS Redirection check
try
{
    app.UseHttpsRedirection();
    Console.WriteLine("🔒 HTTPS redirection habilitado.");
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Yellow;
    Console.WriteLine($"⚠️ No se pudo habilitar HTTPS correctamente: {ex.Message}");
    Console.ResetColor();
}

app.UseStaticFiles();
app.UseRouting();

// =======================================================
// 🔎 DEPURACIÓN DE REQUESTS Y COOKIES
// =======================================================
app.Use(async (context, next) =>
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"\n🌐 [{DateTime.Now:T}] {context.Request.Method} {context.Request.Scheme}://{context.Request.Host}{context.Request.Path}");
    Console.ResetColor();

    if (context.Request.Headers.TryGetValue("Cookie", out var cookies))
        Console.WriteLine($"📥 Cookies recibidas: {cookies}");
    else
        Console.WriteLine("⚠️ No se recibió ninguna cookie en la solicitud.");

    context.Response.OnStarting(() =>
    {
        if (context.Response.Headers.TryGetValue("Set-Cookie", out var setCookie))
            Console.WriteLine($"🍪 Cookies enviadas al cliente: {setCookie}");
        return Task.CompletedTask;
    });

    await next();
});

app.UseAuthentication();
app.UseAuthorization();

// =======================================================
// 🔹 MAPEO DE RUTAS
// =======================================================
app.MapControllers();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

Console.WriteLine("📡 Rutas y controladores listos.");

// =======================================================
// 🔹 SINCRONIZACIÓN INICIAL DE DATOS
// =======================================================
using (var scope = app.Services.CreateScope())
{
    try
    {
        Console.WriteLine("🔄 Sincronizando datos iniciales...");
        var sync = scope.ServiceProvider.GetRequiredService<DataSyncService>();
        await sync.SyncAllAsync();
        Console.WriteLine("✅ Sincronización completa.");
    }
    catch (Exception ex)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"[WARN] Error sincronizando datos: {ex.Message}");
        Console.ResetColor();
    }
}

// =======================================================
// 🔹 AVISO DE CERTIFICADO HTTPS LOCAL
// =======================================================
if (app.Environment.IsDevelopment())
{
    Console.WriteLine("\n🔐 Verifica tu certificado HTTPS local:");
    Console.WriteLine("   Ejecuta: dotnet dev-certs https --trust");
    Console.WriteLine("   Si no está confiado, las cookies Secure serán bloqueadas.\n");
}

// =======================================================
// 🔹 EJECUCIÓN FINAL
// =======================================================
app.Logger.LogInformation("========== SERVIDOR INICIADO ==========");
app.Logger.LogInformation("Modo: {env}", app.Environment.EnvironmentName);
app.Run();
