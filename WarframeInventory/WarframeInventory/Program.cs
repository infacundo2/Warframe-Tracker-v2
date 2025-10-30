using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MudBlazor.Services;
using WarframeInventory.Data;
using WarframeInventory.Services;
using Microsoft.AspNetCore.Components.Authorization;


var builder = WebApplication.CreateBuilder(args);

// =======================================================
// 🔹 CONEXIÓN A BASE DE DATOS
// =======================================================
var host = Environment.GetEnvironmentVariable("DB_HOST");
var user = Environment.GetEnvironmentVariable("DB_USER");
var pass = Environment.GetEnvironmentVariable("DB_PASS");
var db   = Environment.GetEnvironmentVariable("DB_NAME");

var connectionString =
    $"server={host};port=3306;database={db};user={user};password={pass};SslMode=None;AllowPublicKeyRetrieval=True;";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// =======================================================
// 🔹 IDENTITY (autenticación y usuarios)
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
// 🔹 SERVICIOS PERSONALIZADOS
// =======================================================
builder.Services.AddMudServices();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddControllers();

builder.Services.AddHttpClient<WarframeApiService>();
builder.Services.AddScoped<DataSyncService>();

// =======================================================
// 🔹 CONSTRUIR APLICACIÓN
// =======================================================
var app = builder.Build();

// =======================================================
// 🔹 MIDDLEWARE PIPELINE
// =======================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// =======================================================
// 🔹 RUTAS
// =======================================================
app.MapControllers();       // <-- Necesario para AuthController
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// =======================================================
// 🔹 SINCRONIZAR DATOS DE WARFRAME AL INICIAR
// =======================================================
using (var scope = app.Services.CreateScope())
{
    try
    {
        var sync = scope.ServiceProvider.GetRequiredService<DataSyncService>();
        await sync.SyncAllAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[WARN] Error sincronizando datos iniciales: {ex.Message}");
    }
}

app.Run();
