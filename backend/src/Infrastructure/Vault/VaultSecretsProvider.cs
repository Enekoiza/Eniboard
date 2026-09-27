using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Vault;

/// <summary>
/// Reads Eniboard's secrets from a HashiCorp Vault KV (v2) endpoint. The Vault base
/// address and token come from the <c>VAULT_ADDR</c> / <c>VAULT_TOKEN</c> environment
/// variables (falling back to configuration for local overrides). The secret path
/// itself is configurable via <c>Vault:SecretPath</c> (defaults to <c>v1/secret/data/Eniboard</c>)
/// and is expected to contain the keys <c>connection_string</c>, <c>jwt_key</c>,
/// <c>seed_login</c> and <c>seed_password</c>.
/// </summary>
public sealed class VaultSecretsProvider : IVaultSecretsProvider
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<VaultSecretsProvider> _logger;
    private EniboardSecrets? _cached;

    public VaultSecretsProvider(HttpClient httpClient, IConfiguration configuration, ILogger<VaultSecretsProvider> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;

        var vaultAddr = Environment.GetEnvironmentVariable("VAULT_ADDR") ?? _configuration["Vault:Address"];
        var vaultToken = Environment.GetEnvironmentVariable("VAULT_TOKEN") ?? _configuration["Vault:Token"];

        if (string.IsNullOrWhiteSpace(vaultAddr))
        {
            throw new InvalidOperationException(
                "Vault is enabled (UseVault=true) but VAULT_ADDR is not set. Set the VAULT_ADDR environment variable or Vault:Address in configuration.");
        }

        _httpClient.BaseAddress = new Uri(vaultAddr, UriKind.Absolute);

        if (!string.IsNullOrWhiteSpace(vaultToken))
        {
            _httpClient.DefaultRequestHeaders.Remove("X-Vault-Token");
            _httpClient.DefaultRequestHeaders.Add("X-Vault-Token", vaultToken);
        }
        else
        {
            _logger.LogWarning("VAULT_TOKEN is not set; requests to Vault will likely be rejected.");
        }
    }

    public async Task<EniboardSecrets> GetSecretsAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
        {
            return _cached;
        }

        var secretPath = _configuration["Vault:SecretPath"] ?? "v1/secret/data/Eniboard";
        using var response = await _httpClient.GetAsync(secretPath, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        // KV v2 responses nest the actual key/value pairs under data.data.
        var data = document.RootElement.GetProperty("data").GetProperty("data");

        var secrets = new EniboardSecrets(
            DbConnectionString: RequireString(data, "connection_string"),
            JwtSigningKey: RequireString(data, "jwt_key"),
            SeedUsername: RequireString(data, "seed_login"),
            SeedPassword: RequireString(data, "seed_password"));

        _cached = secrets;
        return secrets;
    }

    private static string RequireString(JsonElement data, string key)
    {
        if (!data.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException($"Vault secret '{key}' was not found at the configured secret path.");
        }

        return value.GetString()!;
    }
}
