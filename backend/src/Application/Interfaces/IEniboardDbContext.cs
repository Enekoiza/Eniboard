using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Interfaces;

/// <summary>
/// Persistence seam consumed by the Application layer so that services depend on an
/// abstraction rather than the concrete EF Core <c>DbContext</c> implemented in Infrastructure.
/// </summary>
public interface IEniboardDbContext
{
    DbSet<AppEntity> Apps { get; }

    DbSet<Board> Boards { get; }

    DbSet<BoardColumn> BoardColumns { get; }

    DbSet<Card> Cards { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    Task<IEniboardTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Abstraction over an EF Core database transaction so the Application layer never
/// references <c>Microsoft.EntityFrameworkCore.Storage</c> directly.
/// </summary>
public interface IEniboardTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}
