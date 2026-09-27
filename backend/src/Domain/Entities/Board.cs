namespace Domain.Entities;

public class Board
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid AppId { get; set; }

    public AppEntity? App { get; set; }

    public List<BoardColumn> Columns { get; set; } = [];
}
