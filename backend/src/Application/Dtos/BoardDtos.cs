namespace Application.Dtos;

public sealed record BoardColumnResponse(
    Guid Id,
    string Name,
    int Order,
    int? WipLimit,
    bool IsDefault,
    IReadOnlyList<CardResponse> Cards);

public sealed record BoardResponse(Guid Id, Guid AppId, IReadOnlyList<BoardColumnResponse> Columns);
