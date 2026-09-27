using System.Net.Http.Json;
using Application.Dtos;

namespace Api.Tests;

public class AppBoardTests : IAsyncLifetime
{
    private readonly EniboardWebApplicationFactory _factory = new();

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreatingAnApp_CreatesItsBoardWithFourDefaultColumns()
    {
        var client = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory);

        var createResponse = await client.PostAsJsonAsync("/apps", new CreateAppRequest("My App", "#ff0000", "https://github.com/owner/repo"));
        createResponse.EnsureSuccessStatusCode();
        var created = await createResponse.Content.ReadFromJsonAsync<AppResponse>();
        Assert.NotNull(created);

        var boardResponse = await client.GetAsync($"/apps/{created!.Id}/board");
        boardResponse.EnsureSuccessStatusCode();
        var board = await boardResponse.Content.ReadFromJsonAsync<BoardResponse>();

        Assert.NotNull(board);
        Assert.Equal(4, board!.Columns.Count);

        var orderedNames = board.Columns.OrderBy(c => c.Order).Select(c => c.Name).ToArray();
        Assert.Equal(["Backlog", "To Do", "Doing", "Done"], orderedNames);

        var doingColumn = board.Columns.Single(c => c.Name == "Doing");
        Assert.Equal(1, doingColumn.WipLimit);
    }
}
