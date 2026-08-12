using Microsoft.Extensions.Options;
using WarframeTracker.Agent.Configuration;
using WarframeTracker.Agent.Diagnostics;
using WarframeTracker.Agent.Hosting;
using WarframeTracker.Agent.ProcessDetection;
using WarframeTracker.Agent.Security;
using WarframeTracker.Agent.Sync;
using WarframeTracker.Agent.Providers.EELog;
using WarframeTracker.Agent.Providers.Inventory;
using WarframeTracker.Agent.Inventory;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddJsonFile(
    "trackeragentsettings.json",
    optional: true,
    reloadOnChange: false);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});

builder.Services
    .AddOptions<TrackerAgentOptions>()
    .Bind(builder.Configuration.GetSection(TrackerAgentOptions.SectionName))
    .Validate(
        options => !string.IsNullOrWhiteSpace(options.ProcessName),
        "TrackerAgent:ProcessName es obligatorio.")
    .Validate(
        options => options.ProcessDetectionIntervalSeconds is >= 2 and <= 60,
        "TrackerAgent:ProcessDetectionIntervalSeconds debe estar entre 2 y 60.")
    .Validate(options => Uri.TryCreate(options.ServerBaseUrl, UriKind.Absolute, out var uri)
                         && (uri.Scheme == Uri.UriSchemeHttps
                             || uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback),
        "TrackerAgent:ServerBaseUrl debe usar HTTPS; HTTP solo se admite en loopback para QA.")
    .Validate(options => options.MaximumOfflineBatches is >= 1 and <= 100,
        "TrackerAgent:MaximumOfflineBatches debe estar entre 1 y 100.")
    .ValidateOnStart();

builder.Services.AddSingleton<AgentStatus>();
builder.Services.AddSingleton<IProcessQuery, SystemProcessQuery>();
builder.Services.AddSingleton<WindowsWarframeProcessDetector>();
builder.Services.AddSingleton<IWarframeProcessDetector>(serviceProvider =>
    serviceProvider.GetRequiredService<WindowsWarframeProcessDetector>());
builder.Services.AddSingleton<WarframeSessionCoordinator>();
builder.Services.AddSingleton<EELogEventStream>();
builder.Services.AddSingleton<SnapshotStore>();
builder.Services.AddSingleton<IInventoryProvider, SafeInboxInventoryProvider>();
builder.Services.AddSingleton<IAgentCredentialStore, AgentCredentialStore>();
builder.Services.AddHttpClient<AgentApiClient>((services, client) =>
{
    var options = services.GetRequiredService<IOptions<TrackerAgentOptions>>().Value;
    client.BaseAddress = new Uri(options.ServerBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("WarframeTracker.Agent/0.2");
});
builder.Services.AddHostedService<AgentPairingHostedService>();
builder.Services.AddHostedService<EELogTailService>();
builder.Services.AddHostedService<InventoryCaptureHostedService>();
builder.Services.AddHostedService<InventorySyncHostedService>();

// El coordinador se inicia primero para suscribirse antes de la primera consulta.
builder.Services.AddHostedService(serviceProvider =>
    serviceProvider.GetRequiredService<WarframeSessionCoordinator>());
builder.Services.AddHostedService(serviceProvider =>
    serviceProvider.GetRequiredService<WindowsWarframeProcessDetector>());

using var host = builder.Build();

try
{
    await host.RunAsync();
}
catch (OptionsValidationException exception)
{
    Console.Error.WriteLine($"[Agent] Configuración inválida: {exception.Message}");
    Environment.ExitCode = 2;
}
