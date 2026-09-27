namespace Application.Dtos;

public sealed record CreateAppRequest(string Name, string Color, string? RepoUrl);

public sealed record AppResponse(Guid Id, string Name, string Color, string? RepoUrl, DateTimeOffset CreatedAt, Guid BoardId);
