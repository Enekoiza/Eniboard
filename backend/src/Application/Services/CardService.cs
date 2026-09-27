using Application.Dtos;
using Application.Exceptions;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Application.Services;

public interface ICardService
{
    Task<CardResponse> CreateAsync(CreateCardRequest request, CancellationToken cancellationToken = default);

    Task<CardResponse> GetAsync(Guid cardId, CancellationToken cancellationToken = default);

    Task<CardResponse> UpdateAsync(Guid cardId, UpdateCardRequest request, CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid cardId, CancellationToken cancellationToken = default);

    Task<CardResponse> MoveCardAsync(Guid cardId, Guid targetColumnId, string? linkedBranch, CancellationToken cancellationToken = default);
}

public class CardService(IEniboardDbContext db) : ICardService
{
    public async Task<CardResponse> CreateAsync(CreateCardRequest request, CancellationToken cancellationToken = default)
    {
        var column = await db.BoardColumns
            .FirstOrDefaultAsync(c => c.Id == request.ColumnId && c.BoardId == request.BoardId, cancellationToken)
            ?? throw new NotFoundException($"Column '{request.ColumnId}' was not found on board '{request.BoardId}'.");

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        if (column.WipLimit is not null)
        {
            // The lock must be the first statement in the transaction, before the count: under
            // REPEATABLE READ the count would otherwise read a snapshot fixed before the lock.
            await db.LockBoardColumnAsync(column.Id, cancellationToken);

            var currentCount = await db.Cards.CountAsync(c => c.ColumnId == column.Id, cancellationToken);
            if (currentCount >= column.WipLimit)
            {
                throw new WipLimitExceededException(
                    $"Column '{column.Name}' has reached its WIP limit of {column.WipLimit}. Move an existing card out first.");
            }
        }

        var card = new Card
        {
            BoardId = request.BoardId,
            ColumnId = request.ColumnId,
            Title = request.Title,
            Description = request.Description ?? string.Empty,
            CardType = request.CardType,
            Priority = request.Priority,
            LinkedBranch = string.Equals(column.Name, BoardColumn.DoingName, StringComparison.OrdinalIgnoreCase)
                ? request.LinkedBranch
                : null,
        };

        db.Cards.Add(card);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ToResponse(card);
    }

    public async Task<CardResponse> GetAsync(Guid cardId, CancellationToken cancellationToken = default)
    {
        var card = await FindCardAsync(cardId, cancellationToken);
        return ToResponse(card);
    }

    public async Task<CardResponse> UpdateAsync(Guid cardId, UpdateCardRequest request, CancellationToken cancellationToken = default)
    {
        var card = await FindCardAsync(cardId, cancellationToken);

        card.Title = request.Title;
        card.Description = request.Description ?? string.Empty;
        card.CardType = request.CardType;
        card.Priority = request.Priority;
        card.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
        return ToResponse(card);
    }

    public async Task DeleteAsync(Guid cardId, CancellationToken cancellationToken = default)
    {
        var card = await FindCardAsync(cardId, cancellationToken);
        db.Cards.Remove(card);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<CardResponse> MoveCardAsync(Guid cardId, Guid targetColumnId, string? linkedBranch, CancellationToken cancellationToken = default)
    {
        var card = await FindCardAsync(cardId, cancellationToken);

        var targetColumn = await db.BoardColumns
            .FirstOrDefaultAsync(c => c.Id == targetColumnId && c.BoardId == card.BoardId, cancellationToken)
            ?? throw new NotFoundException($"Column '{targetColumnId}' was not found on board '{card.BoardId}'.");

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        if (targetColumn.WipLimit is not null)
        {
            // The lock must be the first statement in the transaction, before the count: under
            // REPEATABLE READ the count would otherwise read a snapshot fixed before the lock.
            await db.LockBoardColumnAsync(targetColumn.Id, cancellationToken);

            var occupantCount = await db.Cards.CountAsync(
                c => c.ColumnId == targetColumn.Id && c.Id != cardId,
                cancellationToken);

            if (occupantCount >= targetColumn.WipLimit)
            {
                throw new WipLimitExceededException(
                    $"Column '{targetColumn.Name}' has reached its WIP limit of {targetColumn.WipLimit}. " +
                    "Move the existing card out first before moving this one in.");
            }
        }

        card.ColumnId = targetColumn.Id;
        card.UpdatedAt = DateTimeOffset.UtcNow;

        if (string.Equals(targetColumn.Name, BoardColumn.DoingName, StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(linkedBranch))
            {
                card.LinkedBranch = linkedBranch;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(card);
    }

    private async Task<Card> FindCardAsync(Guid cardId, CancellationToken cancellationToken)
    {
        return await db.Cards.FirstOrDefaultAsync(c => c.Id == cardId, cancellationToken)
            ?? throw new NotFoundException($"Card '{cardId}' was not found.");
    }

    private static CardResponse ToResponse(Card card) => new(
        card.Id,
        card.BoardId,
        card.ColumnId,
        card.Title,
        card.Description,
        card.CardType,
        card.Priority,
        card.LinkedBranch,
        card.CreatedAt,
        card.UpdatedAt);
}
