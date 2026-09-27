using Infrastructure.Vault;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Identity;

/// <summary>
/// Ensures exactly one user exists on startup, created from the seed username/password
/// read via <see cref="IVaultSecretsProvider"/>. Idempotent: if any user already exists
/// (in particular, the seed user itself), nothing happens — this never touches an
/// existing user's password.
/// </summary>
public static class SeedUserInitializer
{
    public static async Task EnsureSeedUserAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var vault = services.GetRequiredService<IVaultSecretsProvider>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("SeedUserInitializer");

        var anyUserExists = await userManager.Users.AnyAsync(cancellationToken);
        if (anyUserExists)
        {
            logger.LogInformation("At least one user already exists; skipping seed user creation.");
            return;
        }

        var secrets = await vault.GetSecretsAsync(cancellationToken);

        var user = new ApplicationUser
        {
            UserName = secrets.SeedUsername,
            Email = secrets.SeedUsername.Contains('@') ? secrets.SeedUsername : null,
            EmailConfirmed = true,
        };

        var result = await userManager.CreateAsync(user, secrets.SeedPassword);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to create seed user: {errors}");
        }

        logger.LogInformation("Created seed user '{Username}'.", secrets.SeedUsername);
    }
}
