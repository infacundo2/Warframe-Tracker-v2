using Microsoft.EntityFrameworkCore;
using WarframeInventory.Data;
using WarframeInventory.Services;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// =======================================================
// CONFIGURACIÓN DE BASE DE DATOS
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
// SERVICIOS Y DEPENDENCIAS
// =======================================================
builder.Services.AddMudServices();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Servicios propios
builder.Services.AddHttpClient<WarframeApiService>();
builder.Services.AddScoped<DataSyncService>();

// =======================================================
// CONSTRUCCIÓN DE LA APLICACIÓN
// =======================================================
var app = builder.Build();

// =======================================================
// CONFIGURACIÓN DEL PIPELINE
// =======================================================
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// =======================================================
// SINCRONIZACIÓN INICIAL CON LA API (ETAPA 3)
// =======================================================
using (var scope = app.Services.CreateScope())
{
    var sync = scope.ServiceProvider.GetRequiredService<DataSyncService>();
    await sync.SyncWarframesAsync();
}

// =======================================================
// MAPEO DE PÁGINAS Y EJECUCIÓN
// =======================================================
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.Run();
