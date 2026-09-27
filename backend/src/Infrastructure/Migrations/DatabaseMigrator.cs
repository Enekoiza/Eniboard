using System.Reflection;
using DbUp;

namespace Infrastructure.Migrations;

/// <summary>
/// Applies pending schema changes from the embedded SQL scripts under
/// <c>Migrations/Scripts/</c> (naming convention: <c>NNNN_description.sql</c>, applied in
/// order). Tracked in DbUp's own <c>SchemaVersions</c> journal table, independent of EF Core.
/// </summary>
public static class DatabaseMigrator
{
    public static void Migrate(string connectionString)
    {
        var upgrader = DeployChanges.To
            .MySqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(Assembly.GetExecutingAssembly())
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
        {
            throw new InvalidOperationException(
                $"Database migration failed while applying '{result.ErrorScript?.Name}'.",
                result.Error);
        }
    }
}
