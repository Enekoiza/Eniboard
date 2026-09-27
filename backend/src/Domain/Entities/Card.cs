using Domain.Enums;

namespace Domain.Entities;

public class Card
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BoardId { get; set; }

    public Board? Board { get; set; }

    public Guid ColumnId { get; set; }

    public BoardColumn? Column { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public CardType CardType { get; set; }

    public Priority Priority { get; set; }

    public string? LinkedBranch { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
