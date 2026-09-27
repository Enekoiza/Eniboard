using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Application.Dtos;
using Domain.Enums;

namespace Api.Tests;

public class WebhookTests : IAsyncLifetime
{
    private const string WebhookSecret = "test-webhook-secret";

    private readonly EniboardWebApplicationFactory _factory = new();

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task MergeWebhook_MovesLinkedCardToDone()
    {
        var client = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory);

        var appResponse = await client.PostAsJsonAsync("/apps", new CreateAppRequest("Webhook App", "#123456", "https://github.com/owner/repo"));
        var app = await appResponse.Content.ReadFromJsonAsync<AppResponse>();

        var boardResponse = await client.GetAsync($"/apps/{app!.Id}/board");
        var board = await boardResponse.Content.ReadFromJsonAsync<BoardResponse>();
        var backlog = board!.Columns.Single(c => c.Name == "Backlog");
        var doing = board.Columns.Single(c => c.Name == "Doing");

        var cardResponse = await client.PostAsJsonAsync("/cards", new CreateCardRequest(
            board.Id, backlog.Id, "Ship feature", "Desc", CardType.Feature, Priority.Medium, null));
        var card = await cardResponse.Content.ReadFromJsonAsync<CardResponse>();

        var moveResponse = await client.PatchAsJsonAsync($"/cards/{card!.Id}/move", new MoveCardRequest(doing.Id, "feature/ship-it"));
        moveResponse.EnsureSuccessStatusCode();

        var payloadJson = """
            {
              "ref": "refs/heads/main",
              "repository": {
                "html_url": "https://github.com/owner/repo",
                "default_branch": "main"
              }
            }
            """;

        // The webhook payload here represents "feature/ship-it" having been merged: the
        // endpoint itself only inspects the pushed ref, so exercise HandleMergeWebhookAsync's
        // matching behavior against the branch our card was linked to.
        payloadJson = payloadJson.Replace("refs/heads/main", "refs/heads/feature/ship-it");

        using var unauthenticatedClient = _factory.CreateClient();
        var signature = ComputeSignature(payloadJson, WebhookSecret);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/github")
        {
            Content = new StringContent(payloadJson, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Hub-Signature-256", $"sha256={signature}");

        var webhookResponse = await unauthenticatedClient.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);

        var authedCardAfter = await client.GetAsync($"/cards/{card.Id}");
        authedCardAfter.EnsureSuccessStatusCode();
        var updatedCard = await authedCardAfter.Content.ReadFromJsonAsync<CardResponse>();

        var doneColumn = board.Columns.Single(c => c.Name == "Done");
        Assert.Equal(doneColumn.Id, updatedCard!.ColumnId);
    }

    [Fact]
    public async Task MergeWebhook_WithInvalidSignature_ReturnsUnauthorized()
    {
        using var client = _factory.CreateClient();

        var payloadJson = """{"ref":"refs/heads/some-branch","repository":{"html_url":"https://github.com/owner/repo","default_branch":"main"}}""";

        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/github")
        {
            Content = new StringContent(payloadJson, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Hub-Signature-256", "sha256=deadbeef");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static string ComputeSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(hash);
    }
}
