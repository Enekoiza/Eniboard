using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.Dtos;
using Domain.Enums;

namespace Api.Tests;

public class CardValidationTests : IAsyncLifetime
{
    private readonly EniboardWebApplicationFactory _factory = new();

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task CreateCard_WithMissingDescription_ReturnsCreatedWithEmptyDescription()
    {
        var client = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory);

        var appResponse = await client.PostAsJsonAsync("/apps", new CreateAppRequest("No Description App", "#abcdef", null));
        var app = await appResponse.Content.ReadFromJsonAsync<AppResponse>();

        var boardResponse = await client.GetAsync($"/apps/{app!.Id}/board");
        var board = await boardResponse.Content.ReadFromJsonAsync<BoardResponse>(TestJson.Options);
        var backlog = board!.Columns.Single(c => c.Name == "Backlog");

        var payload = new
        {
            boardId = board.Id,
            columnId = backlog.Id,
            title = "Card without description",
            cardType = "feature",
            priority = "medium",
        };

        using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/cards", content);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var card = await response.Content.ReadFromJsonAsync<CardResponse>(TestJson.Options);
        Assert.NotNull(card);
        Assert.Equal(string.Empty, card!.Description);
    }
}
