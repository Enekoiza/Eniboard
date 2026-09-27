using System.Net;
using System.Net.Http;
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
        var (_, cardId, _, doneId) = await SeedCardInDoingAsync();

        var payloadJson = """
            {
              "action": "closed",
              "pull_request": {
                "merged": true,
                "head": { "ref": "feature/ship-it" },
                "base": { "ref": "main" }
              },
              "repository": {
                "html_url": "https://github.com/owner/repo",
                "default_branch": "main"
              }
            }
            """;

        var webhookResponse = await SendWebhookAsync("pull_request", payloadJson);
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);

        var client = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory);
        var updatedCard = await GetCardAsync(client, cardId);
        Assert.Equal(doneId, updatedCard!.ColumnId);
    }

    [Fact]
    public async Task PushToLinkedFeatureBranch_DoesNotMoveCard()
    {
        var (_, cardId, doingId, _) = await SeedCardInDoingAsync();

        var payloadJson = """
            {
              "ref": "refs/heads/feature/ship-it",
              "repository": {
                "html_url": "https://github.com/owner/repo",
                "default_branch": "main"
              }
            }
            """;

        var webhookResponse = await SendWebhookAsync("push", payloadJson);
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);

        var client = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory);
        var updatedCard = await GetCardAsync(client, cardId);
        Assert.Equal(doingId, updatedCard!.ColumnId);
    }

    [Fact]
    public async Task ClosedUnmergedPullRequest_DoesNotMoveCard()
    {
        var (_, cardId, doingId, _) = await SeedCardInDoingAsync();

        var payloadJson = """
            {
              "action": "closed",
              "pull_request": {
                "merged": false,
                "head": { "ref": "feature/ship-it" },
                "base": { "ref": "main" }
              },
              "repository": {
                "html_url": "https://github.com/owner/repo",
                "default_branch": "main"
              }
            }
            """;

        var webhookResponse = await SendWebhookAsync("pull_request", payloadJson);
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);

        var client = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory);
        var updatedCard = await GetCardAsync(client, cardId);
        Assert.Equal(doingId, updatedCard!.ColumnId);
    }

    [Fact]
    public async Task MergedIntoNonDefaultBase_DoesNotMoveCard()
    {
        var (_, cardId, doingId, _) = await SeedCardInDoingAsync();

        var payloadJson = """
            {
              "action": "closed",
              "pull_request": {
                "merged": true,
                "head": { "ref": "feature/ship-it" },
                "base": { "ref": "develop" }
              },
              "repository": {
                "html_url": "https://github.com/owner/repo",
                "default_branch": "main"
              }
            }
            """;

        var webhookResponse = await SendWebhookAsync("pull_request", payloadJson);
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);

        var client = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory);
        var updatedCard = await GetCardAsync(client, cardId);
        Assert.Equal(doingId, updatedCard!.ColumnId);
    }

    [Fact]
    public async Task MergeWebhook_OnlyMovesCardsFromMatchingRepository()
    {
        var (clientA, cardIdA, _, doneIdA) = await SeedCardInDoingAsync("https://github.com/owner/repo");
        var (clientB, cardIdB, doingIdB, _) = await SeedCardInDoingAsync("https://github.com/other/repo");

        var payloadJson = """
            {
              "action": "closed",
              "pull_request": {
                "merged": true,
                "head": { "ref": "feature/ship-it" },
                "base": { "ref": "main" }
              },
              "repository": {
                "html_url": "https://github.com/owner/repo",
                "default_branch": "main"
              }
            }
            """;

        var webhookResponse = await SendWebhookAsync("pull_request", payloadJson);
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);

        var updatedCardA = await GetCardAsync(clientA, cardIdA);
        Assert.Equal(doneIdA, updatedCardA!.ColumnId);

        var updatedCardB = await GetCardAsync(clientB, cardIdB);
        Assert.Equal(doingIdB, updatedCardB!.ColumnId);
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
        request.Headers.Add("X-GitHub-Event", "push");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<(HttpClient Client, Guid CardId, Guid DoingId, Guid DoneId)> SeedCardInDoingAsync(string repoUrl = "https://github.com/owner/repo")
    {
        var client = await TestAuthHelper.CreateAuthenticatedClientAsync(_factory);

        var appResponse = await client.PostAsJsonAsync("/apps", new CreateAppRequest("Webhook App", "#123456", repoUrl));
        var app = await appResponse.Content.ReadFromJsonAsync<AppResponse>();

        var boardResponse = await client.GetAsync($"/apps/{app!.Id}/board");
        var board = await boardResponse.Content.ReadFromJsonAsync<BoardResponse>(TestJson.Options);
        var backlog = board!.Columns.Single(c => c.Name == "Backlog");
        var doing = board.Columns.Single(c => c.Name == "Doing");
        var done = board.Columns.Single(c => c.Name == "Done");

        var cardResponse = await client.PostAsJsonAsync("/cards", new CreateCardRequest(
            board.Id, backlog.Id, "Ship feature", "Desc", CardType.Feature, Priority.Medium, null), TestJson.Options);
        var card = await cardResponse.Content.ReadFromJsonAsync<CardResponse>(TestJson.Options);

        var moveResponse = await client.PatchAsJsonAsync($"/cards/{card!.Id}/move", new MoveCardRequest(doing.Id, "feature/ship-it"), TestJson.Options);
        moveResponse.EnsureSuccessStatusCode();

        return (client, card.Id, doing.Id, done.Id);
    }

    private async Task<HttpResponseMessage> SendWebhookAsync(string eventName, string payloadJson)
    {
        using var unauthenticatedClient = _factory.CreateClient();
        var signature = ComputeSignature(payloadJson, WebhookSecret);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/github")
        {
            Content = new StringContent(payloadJson, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Hub-Signature-256", $"sha256={signature}");
        request.Headers.Add("X-GitHub-Event", eventName);

        return await unauthenticatedClient.SendAsync(request);
    }

    private static async Task<CardResponse?> GetCardAsync(HttpClient client, Guid cardId)
    {
        var response = await client.GetAsync($"/cards/{cardId}");
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<CardResponse>(TestJson.Options);
    }

    private static string ComputeSignature(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexStringLower(hash);
    }
}
