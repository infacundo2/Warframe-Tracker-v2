using Microsoft.Extensions.Options;
using WarframeTracker.Agent.Configuration;
using WarframeTracker.Agent.Diagnostics;
using WarframeTracker.Agent.Hosting;
using WarframeTracker.Agent.ProcessDetection;

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
    .ValidateOnStart();

builder.Services.AddSingleton<AgentStatus>();
builder.Services.AddSingleton<IProcessQuery, SystemProcessQuery>();
builder.Services.AddSingleton<WindowsWarframeProcessDetector>();
builder.Services.AddSingleton<IWarframeProcessDetector>(serviceProvider =>
    serviceProvider.GetRequiredService<WindowsWarframeProcessDetector>());
builder.Services.AddSingleton<WarframeSessionCoordinator>();

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
