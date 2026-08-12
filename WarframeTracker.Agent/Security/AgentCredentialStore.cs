using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using WarframeTracker.Agent.Configuration;

namespace WarframeTracker.Agent.Security;

public interface IAgentCredentialStore
{
    Task<string?> ReadTokenAsync(CancellationToken ct);
    Task WriteTokenAsync(string token, CancellationToken ct);
    Task DeleteTokenAsync(CancellationToken ct);
}

public sealed class AgentCredentialStore : IAgentCredentialStore
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("WarframeTracker.Agent.v1");
    private readonly string _path;
    public AgentCredentialStore(IOptions<TrackerAgentOptions> options)
    {
        var root = string.IsNullOrWhiteSpace(options.Value.DataDirectory)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WarframeTracker", "Agent")
            : Path.GetFullPath(options.Value.DataDirectory);
        Directory.CreateDirectory(root);
        _path = Path.Combine(root, "device-token.bin");
    }
    public async Task<string?> ReadTokenAsync(CancellationToken ct)
    {
        if (!File.Exists(_path)) return null;
        try
        {
            var encrypted = await File.ReadAllBytesAsync(_path, ct);
            var clear = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(clear);
        }
        catch (CryptographicException) { return null; }
    }
    public async Task WriteTokenAsync(string token, CancellationToken ct)
    {
        var encrypted = ProtectedData.Protect(Encoding.UTF8.GetBytes(token), Entropy,
            DataProtectionScope.CurrentUser);
        var temporary = _path + ".tmp";
        await File.WriteAllBytesAsync(temporary, encrypted, ct);
        File.Move(temporary, _path, true);
    }
    public Task DeleteTokenAsync(CancellationToken ct)
    {
        if (File.Exists(_path)) File.Delete(_path);
        return Task.CompletedTask;
    }
}
