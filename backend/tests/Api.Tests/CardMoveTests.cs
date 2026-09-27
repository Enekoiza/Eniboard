using System.Net;
using System.Net.Http.Json;
using Application.Dtos;
using Domain.Enums;

namespace Api.Tests;

public class CardMoveTests : IAsyncLifetime
{
    private readonly EniboardWebApplicationFactory _factory = new();

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task MovingSecondCardIntoDoing_WhenOneAlreadyThere_ReturnsConflict()
    {
        var client = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory);

        var appResponse = await client.PostAsJsonAsync("/apps", new CreateAppRequest("WIP App", "#00ff00", null));
        var app = await appResponse.Content.ReadFromJsonAsync<AppResponse>();

        var boardResponse = await client.GetAsync($"/apps/{app!.Id}/board");
        var board = await boardResponse.Content.ReadFromJsonAsync<BoardResponse>(TestJson.Options);
        var backlog = board!.Columns.Single(c => c.Name == "Backlog");
        var doing = board.Columns.Single(c => c.Name == "Doing");

        var card1Response = await client.PostAsJsonAsync("/cards", new CreateCardRequest(
            board.Id, backlog.Id, "Card 1", "First card", CardType.Feature, Priority.Medium, null), TestJson.Options);
        var card1Json = await card1Response.Content.ReadAsStringAsync();
        Assert.Contains("\"cardType\":\"feature\"", card1Json, StringComparison.Ordinal);
        Assert.Contains("\"priority\":\"medium\"", card1Json, StringComparison.Ordinal);
        var card1 = System.Text.Json.JsonSerializer.Deserialize<CardResponse>(card1Json, TestJson.Options);

        var card2Response = await client.PostAsJsonAsync("/cards", new CreateCardRequest(
            board.Id, backlog.Id, "Card 2", "Second card", CardType.Feature, Priority.Medium, null), TestJson.Options);
        var card2 = await card2Response.Content.ReadFromJsonAsync<CardResponse>(TestJson.Options);

        // Move the first card into Doing: succeeds, fills the WIP limit of 1.
        var move1Response = await client.PatchAsJsonAsync($"/cards/{card1!.Id}/move", new MoveCardRequest(doing.Id, "feature/card-1"), TestJson.Options);
        Assert.Equal(HttpStatusCode.OK, move1Response.StatusCode);

        // Moving the second card into Doing should now fail with a 409 and a clear message.
        var move2Response = await client.PatchAsJsonAsync($"/cards/{card2!.Id}/move", new MoveCardRequest(doing.Id, "feature/card-2"), TestJson.Options);

        Assert.Equal(HttpStatusCode.Conflict, move2Response.StatusCode);
        var problem = await move2Response.Content.ReadAsStringAsync();
        Assert.Contains("WIP limit", problem, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("move", problem, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MovingCardIntoDoing_PersistsLinkedBranch()
    {
        var client = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory);

        var appResponse = await client.PostAsJsonAsync("/apps", new CreateAppRequest("Branch App", "#0000ff", null));
        var app = await appResponse.Content.ReadFromJsonAsync<AppResponse>();

        var boardResponse = await client.GetAsync($"/apps/{app!.Id}/board");
        var board = await boardResponse.Content.ReadFromJsonAsync<BoardResponse>(TestJson.Options);
        var backlog = board!.Columns.Single(c => c.Name == "Backlog");
        var doing = board.Columns.Single(c => c.Name == "Doing");

        var cardResponse = await client.PostAsJsonAsync("/cards", new CreateCardRequest(
            board.Id, backlog.Id, "Card", "Desc", CardType.Bug, Priority.High, null), TestJson.Options);
        var card = await cardResponse.Content.ReadFromJsonAsync<CardResponse>(TestJson.Options);

        var moveResponse = await client.PatchAsJsonAsync($"/cards/{card!.Id}/move", new MoveCardRequest(doing.Id, "feature/my-branch"), TestJson.Options);
        moveResponse.EnsureSuccessStatusCode();

        var moved = await moveResponse.Content.ReadFromJsonAsync<CardResponse>(TestJson.Options);
        Assert.Equal("feature/my-branch", moved!.LinkedBranch);
        Assert.Equal(doing.Id, moved.ColumnId);
    }
}
