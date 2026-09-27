using Application.Dtos;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public interface IAppService
{
    Task<AppResponse> CreateAppAsync(CreateAppRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<BoardResponse> GetBoardAsync(Guid appId, CancellationToken cancellationToken = default);
}

public class AppService(IEniboardDbContext db) : IAppService
{
    public async Task<AppResponse> CreateAppAsync(CreateAppRequest request, CancellationToken cancellationToken = default)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var app = new AppEntity
        {
            Name = request.Name,
            Color = request.Color,
            RepoUrl = request.RepoUrl,
        };
        db.Apps.Add(app);

        var board = new Board { Id = Guid.NewGuid(), AppId = app.Id };
        db.Boards.Add(board);

        var defaultColumns = new[]
        {
            new BoardColumn { BoardId = board.Id, Name = BoardColumn.BacklogName, Order = 0, WipLimit = null, IsDefault = true },
            new BoardColumn { BoardId = board.Id, Name = BoardColumn.ToDoName, Order = 1, WipLimit = null, IsDefault = true },
            new BoardColumn { BoardId = board.Id, Name = BoardColumn.DoingName, Order = 2, WipLimit = 1, IsDefault = true },
            new BoardColumn { BoardId = board.Id, Name = BoardColumn.DoneName, Order = 3, WipLimit = null, IsDefault = true },
        };
        db.BoardColumns.AddRange(defaultColumns);

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new AppResponse(app.Id, app.Name, app.Color, app.RepoUrl, app.CreatedAt, board.Id);
    }

    public async Task<IReadOnlyList<AppResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.Apps
            .AsNoTracking()
            .OrderBy(a => a.CreatedAt)
            .Select(a => new AppResponse(a.Id, a.Name, a.Color, a.RepoUrl, a.CreatedAt, a.Board!.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task<BoardResponse> GetBoardAsync(Guid appId, CancellationToken cancellationToken = default)
    {
        var board = await db.Boards
            .AsNoTracking()
            .Include(b => b.Columns)
                .ThenInclude(c => c.Cards)
            .FirstOrDefaultAsync(b => b.AppId == appId, cancellationToken);

        if (board is null)
        {
            throw new NotFoundException($"No board found for app '{appId}'.");
        }

        var columns = board.Columns
            .OrderBy(c => c.Order)
            .Select(c => new BoardColumnResponse(
                c.Id,
                c.Name,
                c.Order,
                c.WipLimit,
                c.IsDefault,
                c.Cards
                    .Select(card => new CardResponse(
                        card.Id,
                        card.BoardId,
                        card.ColumnId,
                        card.Title,
                        card.Description,
                        card.CardType,
                        card.Priority,
                        card.LinkedBranch,
                        card.CreatedAt,
                        card.UpdatedAt))
                    .ToList()))
            .ToList();

        return new BoardResponse(board.Id, board.AppId, columns);
    }
}
