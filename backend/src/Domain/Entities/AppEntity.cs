namespace Domain.Entities;

public class AppEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string Color { get; set; } = string.Empty;

    public string? RepoUrl { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public Board? Board { get; set; }
}
