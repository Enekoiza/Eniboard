using Domain.Enums;

namespace Application.Dtos;

public sealed record CreateCardRequest(
    Guid BoardId,
    Guid ColumnId,
    string Title,
    string? Description,
    CardType CardType,
    Priority Priority,
    string? LinkedBranch);

public sealed record UpdateCardRequest(
    string Title,
    string? Description,
    CardType CardType,
    Priority Priority);

public sealed record MoveCardRequest(Guid TargetColumnId, string? LinkedBranch);

public sealed record CardResponse(
    Guid Id,
    Guid BoardId,
    Guid ColumnId,
    string Title,
    string Description,
    CardType CardType,
    Priority Priority,
    string? LinkedBranch,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);
