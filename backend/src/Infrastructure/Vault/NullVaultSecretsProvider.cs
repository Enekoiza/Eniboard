using Microsoft.Extensions.Configuration;

namespace Infrastructure.Vault;

/// <summary>
/// Local-development fallback used when <c>UseVault</c> is false. Reads the same four
/// secrets directly from configuration (appsettings / environment variables / user
/// secrets) instead of calling out to a real Vault instance.
/// </summary>
public sealed class NullVaultSecretsProvider(IConfiguration configuration) : IVaultSecretsProvider
{
    public Task<EniboardSecrets> GetSecretsAsync(CancellationToken cancellationToken = default)
    {
        var secrets = new EniboardSecrets(
            DbConnectionString: configuration["Eniboard:DbConnectionString"]
                ?? configuration.GetConnectionString("Default")
                ?? throw new InvalidOperationException("No DB connection string configured (Eniboard:DbConnectionString / ConnectionStrings:Default)."),
            JwtSigningKey: configuration["Eniboard:JwtSigningKey"]
                ?? throw new InvalidOperationException("No JWT signing key configured (Eniboard:JwtSigningKey)."),
            SeedUsername: configuration["Eniboard:SeedUsername"] ?? "admin",
            SeedPassword: configuration["Eniboard:SeedPassword"]
                ?? throw new InvalidOperationException("No seed password configured (Eniboard:SeedPassword)."));

        return Task.FromResult(secrets);
    }
}
