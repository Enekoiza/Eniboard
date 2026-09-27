using Application.Interfaces;
using Application.Services;
using Infrastructure.Data;
using Infrastructure.Identity;
using Infrastructure.Vault;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the Vault secrets provider only. Split out from <see cref="AddInfrastructure"/>
    /// because the DbContext and Identity registrations need the resolved secrets (connection
    /// string, JWT key) before the rest of the container is built.
    /// </summary>
    public static IServiceCollection AddVaultSecrets(this IServiceCollection services, IConfiguration configuration)
    {
        var useVault = configuration.GetValue("UseVault", false);

        if (useVault)
        {
            services.AddHttpClient<IVaultSecretsProvider, VaultSecretsProvider>();
        }
        else
        {
            services.AddSingleton<IVaultSecretsProvider, NullVaultSecretsProvider>();
        }

        return services;
    }

    /// <summary>
    /// Registers the production MySQL-backed <see cref="EniboardDbContext"/>. Kept separate
    /// from <see cref="AddInfrastructure"/> so integration tests can skip this entirely and
    /// register their own (e.g. SQLite) <c>DbContextOptions&lt;EniboardDbContext&gt;</c>
    /// instead, without fighting over the same service registration.
    /// </summary>
    public static IServiceCollection AddMySqlDbContext(this IServiceCollection services, string dbConnectionString)
    {
        // Detected once here at startup, not inside the options lambda below: AddDbContext options
        // are scoped by default, so a lambda would re-run ServerVersion.AutoDetect (and open a new
        // MySQL connection) on every request.
        var serverVersion = ServerVersion.AutoDetect(dbConnectionString);

        services.AddDbContext<EniboardDbContext>(options =>
            options.UseMySql(dbConnectionString, serverVersion));

        return services;
    }

    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IEniboardDbContext>(sp => sp.GetRequiredService<EniboardDbContext>());

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                // Single-user personal app: keep the seeded password's own strength rules,
                // don't force additional runtime policy beyond the defaults.
                options.User.RequireUniqueEmail = false;

                // The per-client login rate limiter (per IPv4 address / per IPv6 /64) (5 attempts / 15 min, see Program.cs) is the
                // main defence against brute-forcing the single seeded account. This lockout
                // threshold is only a backstop against distributed guessing (many source IPs)
                // and is intentionally set well above the rate limiter's budget so a single
                // legitimate user retrying a mistyped password from one IP never gets locked
                // out of the only account in the system.
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 50;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<EniboardDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IAppService, AppService>();
        services.AddScoped<ICardService, CardService>();
        services.AddHttpClient<IGitIntegrationService, GitIntegrationService>();

        return services;
    }
}
