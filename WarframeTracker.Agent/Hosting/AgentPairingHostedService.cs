using System.Diagnostics;
using WarframeTracker.Agent.Security;
using WarframeTracker.Agent.Sync;

namespace WarframeTracker.Agent.Hosting;

public sealed class AgentPairingHostedService : BackgroundService
{
    private readonly AgentApiClient _api;
    private readonly IAgentCredentialStore _credentials;
    private readonly ILogger<AgentPairingHostedService> _logger;
    public AgentPairingHostedService(AgentApiClient api, IAgentCredentialStore credentials,
        ILogger<AgentPairingHostedService> logger)
        => (_api, _credentials, _logger) = (api, credentials, logger);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var token = await _credentials.ReadTokenAsync(stoppingToken);
        if (!string.IsNullOrWhiteSpace(token))
        {
            try
            {
                var status = await _api.StatusAsync(token, stoppingToken);
                if (status is not null)
                {
                    _logger.LogInformation("[Cuenta] Dispositivo conectado a {UserName}.", status.UserName);
                    return;
                }
                await _credentials.DeleteTokenAsync(stoppingToken);
            }
            catch (HttpRequestException exception)
            {
                _logger.LogWarning("[Cuenta] Render no respondió; se conserva el vínculo local: {Message}",
                    exception.Message);
                return;
            }
        }

        try
        {
            var pairing = await _api.StartPairingAsync(stoppingToken);
            _logger.LogInformation("[Cuenta] Código temporal: {Code}", pairing.Code);
            _logger.LogInformation("[Cuenta] Aprueba el equipo en: {ConnectUrl}", pairing.ConnectUrl);
            try { Process.Start(new ProcessStartInfo(pairing.ConnectUrl) { UseShellExecute = true }); }
            catch (Exception exception) { _logger.LogWarning("No se pudo abrir el navegador: {Message}", exception.Message); }

            while (!stoppingToken.IsCancellationRequested && DateTime.UtcNow < pairing.ExpiresUtc)
            {
                await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
                var exchanged = await _api.ExchangeAsync(pairing.PairingId, pairing.Verifier, stoppingToken);
                if (exchanged.Status == "pending") continue;
                if (exchanged.Status == "approved" && !string.IsNullOrWhiteSpace(exchanged.Token))
                {
                    await _credentials.WriteTokenAsync(exchanged.Token, stoppingToken);
                    _logger.LogInformation("[Cuenta] Equipo vinculado a {UserName}; token protegido con DPAPI.",
                        exchanged.UserName);
                    return;
                }
                _logger.LogWarning("[Cuenta] Emparejamiento finalizado con estado {Status}.", exchanged.Status);
                return;
            }
            _logger.LogWarning("[Cuenta] El código temporal venció. Reinicia el Agent para generar otro.");
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            _logger.LogWarning("[Cuenta] No se pudo iniciar el emparejamiento: {Message}", exception.Message);
        }
    }
}
