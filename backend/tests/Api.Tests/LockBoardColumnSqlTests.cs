using System.Data.Common;
using System.Text.RegularExpressions;
using Domain.Entities;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Api.Tests;

/// <summary>
/// Exercises the MySQL-only <c>SELECT ... FOR UPDATE</c> SQL that
/// <see cref="EniboardDbContext.LockBoardColumnAsync"/> generates. The rest of the suite runs
/// against SQLite, which never builds or runs this SQL (see the early-return in that method), so
/// without this test a typo or GUID-format mismatch in the MySQL path would fail silently in
/// production. No Docker/MySQL server is required: the connection is intercepted and suppressed
/// before it ever opens, and command execution is intercepted and suppressed before it ever runs,
/// so this only proves the generated SQL text and parameter shape, not that InnoDB actually blocks
/// a second transaction.
/// </summary>
public class LockBoardColumnSqlTests
{
    private sealed record CapturedCommand(string CommandText, IReadOnlyList<(string Name, object? Value)> Parameters);

    private sealed class SuppressOpenInterceptor : DbConnectionInterceptor
    {
        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result)
        {
            return InterceptionResult.Suppress();
        }

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(InterceptionResult.Suppress());
        }
    }

    // EF Core's RelationalCommand clears the DbCommand's Parameters and disposes the command in a
    // `finally` block right after the (suppressed) execution interceptor returns, so the command
    // object itself can't be inspected afterwards - the SQL text and parameters are snapshotted here,
    // at interception time, instead of being read back later from the (by-then-cleared) DbCommand.
    private sealed class CaptureCommandInterceptor : DbCommandInterceptor
    {
        public CapturedCommand? Captured { get; private set; }

        public override InterceptionResult<int> NonQueryExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<int> result)
        {
            Snapshot(command);
            return InterceptionResult<int>.SuppressWithResult(0);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Snapshot(command);
            return ValueTask.FromResult(InterceptionResult<int>.SuppressWithResult(0));
        }

        private void Snapshot(DbCommand command)
        {
            Captured = new CapturedCommand(
                command.CommandText,
                command.Parameters.Cast<DbParameter>().Select(p => (p.ParameterName, p.Value)).ToList());
        }
    }

    private static EniboardDbContext CreateMySqlContext(CaptureCommandInterceptor capture)
    {
        return new EniboardDbContext(new DbContextOptionsBuilder<EniboardDbContext>()
            .UseMySql(
                "Server=localhost;Database=eniboard_unit;User=unit;Password=unit",
                new MySqlServerVersion(new Version(8, 0, 36)))
            .AddInterceptors(new SuppressOpenInterceptor(), capture)
            .Options);
    }

    [Fact]
    public async Task LockBoardColumnAsync_OnMySql_IssuesSelectForUpdateOnColumnId()
    {
        var capture = new CaptureCommandInterceptor();
        await using var context = CreateMySqlContext(capture);
        var columnId = Guid.NewGuid();

        await context.LockBoardColumnAsync(columnId);

        Assert.NotNull(capture.Captured);
        var normalizedSql = Regex.Replace(capture.Captured!.CommandText, @"\s+", " ").Trim();
        Assert.Matches(@"^SELECT `Id` FROM `BoardColumns` WHERE `Id` = @\w+ FOR UPDATE$", normalizedSql);

        Assert.Single(capture.Captured.Parameters);
        var (_, value) = capture.Captured.Parameters[0];
        if (value is Guid guidValue)
        {
            Assert.Equal(columnId, guidValue);
        }
        else
        {
            Assert.Equal(columnId.ToString(), value?.ToString());
        }
    }

    [Fact]
    public void BoardColumnId_OnMySql_MapsToChar36MatchingSchemaScript()
    {
        var capture = new CaptureCommandInterceptor();
        using var context = CreateMySqlContext(capture);

        var property = context.Model.FindEntityType(typeof(BoardColumn))!.FindProperty(nameof(BoardColumn.Id))!;
        var columnType = property.GetColumnType() ?? property.GetRelationalTypeMapping().StoreType;

        Assert.Equal("char(36)", columnType, ignoreCase: true);
    }
}
