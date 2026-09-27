using Application.Services;
using Infrastructure.Data;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.Tests;

/// <summary>
/// Boots the real API pipeline (auth, validation, exception handling, endpoints) against
/// an in-memory SQLite database instead of MySQL, so the test suite needs no external
/// services. Pomelo doesn't support EF Core's InMemory provider well, so SQLite is used
/// per the project's testing guidance.
/// </summary>
public sealed class EniboardWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            _connection.Open();
            services.AddDbContext<EniboardDbContext>(options => options.UseSqlite(_connection));
        });
    }

    /// <summary>
    /// Creates the schema and seeds the single test user. Must be called once before
    /// tests issue requests (the real Program.cs skips this under the Testing environment).
    /// </summary>
    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<EniboardDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        await SeedUserInitializer.EnsureSeedUserAsync(scope.ServiceProvider);
    }

    public async Task<Guid> CreateAppWithBoardAsync(string name = "Test App")
    {
        using var scope = Services.CreateScope();
        var appService = scope.ServiceProvider.GetRequiredService<IAppService>();
        var created = await appService.CreateAppAsync(new Application.Dtos.CreateAppRequest(name, "#3366ff", null));
        return created.Id;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
