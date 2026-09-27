using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Infrastructure.Data;

/// <summary>
/// Design-time factory used by <c>dotnet ef migrations add</c> / <c>dotnet ef database update</c>.
/// Only needs a syntactically valid MySQL connection string (no real server required) since
/// EF Core only inspects it to pick a server version for SQL generation at design time.
/// </summary>
public sealed class EniboardDbContextFactory : IDesignTimeDbContextFactory<EniboardDbContext>
{
    public EniboardDbContext CreateDbContext(string[] args)
    {
        const string placeholderConnectionString = "server=localhost;port=3306;database=eniboard;user=eniboard;password=placeholder";

        // A fixed server version avoids AutoDetect, which would otherwise try to open a
        // real connection at design time (e.g. while running `dotnet ef migrations add`).
        var serverVersion = new MySqlServerVersion(new Version(8, 0, 35));

        var optionsBuilder = new DbContextOptionsBuilder<EniboardDbContext>();
        optionsBuilder.UseMySql(placeholderConnectionString, serverVersion);

        return new EniboardDbContext(optionsBuilder.Options);
    }
}
