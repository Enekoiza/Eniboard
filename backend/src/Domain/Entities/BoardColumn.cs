namespace Domain.Entities;

public class BoardColumn
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid BoardId { get; set; }

    public Board? Board { get; set; }

    public string Name { get; set; } = string.Empty;

    public int Order { get; set; }

    public int? WipLimit { get; set; }

    public bool IsDefault { get; set; }

    public List<Card> Cards { get; set; } = [];

    public const string BacklogName = "Backlog";
    public const string ToDoName = "To Do";
    public const string DoingName = "Doing";
    public const string DoneName = "Done";
}
