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
        services.AddDbContext<EniboardDbContext>(options =>
            options.UseMySql(dbConnectionString, ServerVersion.AutoDetect(dbConnectionString)));

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
            })
            .AddEntityFrameworkStores<EniboardDbContext>()
            .AddDefaultTokenProviders();

        services.AddScoped<IAppService, AppService>();
        services.AddScoped<ICardService, CardService>();
        services.AddHttpClient<IGitIntegrationService, GitIntegrationService>();

        return services;
    }
}
