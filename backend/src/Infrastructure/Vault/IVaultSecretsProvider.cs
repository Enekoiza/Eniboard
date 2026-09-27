namespace Infrastructure.Vault;

/// <summary>
/// Supplies the small set of secrets Eniboard needs at startup: the database connection
/// string, the JWT signing key, and the credentials for the single seed user.
/// </summary>
public interface IVaultSecretsProvider
{
    Task<EniboardSecrets> GetSecretsAsync(CancellationToken cancellationToken = default);
}

public sealed record EniboardSecrets(
    string DbConnectionString,
    string JwtSigningKey,
    string SeedUsername,
    string SeedPassword);
