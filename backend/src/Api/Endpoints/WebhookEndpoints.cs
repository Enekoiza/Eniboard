using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Application.Dtos;
using Application.Services;

namespace Api.Endpoints;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/webhooks").WithTags("Webhooks");

        group.MapPost("/github", async (
            HttpRequest request,
            IGitIntegrationService gitIntegrationService,
            IConfiguration configuration,
            ILogger<GitHubWebhookMarker> logger,
            CancellationToken cancellationToken) =>
        {
            request.EnableBuffering();
            using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync(cancellationToken);
            request.Body.Position = 0;

            var secret = configuration["GitHub:WebhookSecret"];
            if (string.IsNullOrWhiteSpace(secret))
            {
                logger.LogError("GitHub:WebhookSecret is not configured; rejecting webhook.");
                return Results.Problem("Webhook secret is not configured.", statusCode: StatusCodes.Status500InternalServerError);
            }

            if (!request.Headers.TryGetValue("X-Hub-Signature-256", out var signatureHeader) ||
                !IsSignatureValid(rawBody, secret, signatureHeader.ToString()))
            {
                return Results.Unauthorized();
            }

            var eventName = request.Headers["X-GitHub-Event"].ToString();
            if (string.Equals(eventName, "ping", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Ok(new { message = "pong" });
            }

            if (!string.Equals(eventName, "pull_request", StringComparison.OrdinalIgnoreCase))
            {
                return Results.Ok(new { message = "Payload ignored: not a pull_request event." });
            }

            var payload = ParseMergedPullRequestPayload(rawBody);
            if (payload is null)
            {
                return Results.Ok(new { message = "Payload ignored: pull request was not merged." });
            }

            await gitIntegrationService.HandleMergeWebhookAsync(payload, cancellationToken);
            return Results.Ok(new { message = "Processed." });
        })
        .AllowAnonymous()
        .WithName("GitHubWebhook")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized);
    }

    private static bool IsSignatureValid(string payload, string secret, string signatureHeader)
    {
        if (!signatureHeader.StartsWith("sha256=", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var providedHex = signatureHeader["sha256=".Length..];

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var computedHex = Convert.ToHexStringLower(computedHash);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedHex),
            Encoding.UTF8.GetBytes(providedHex));
    }

    /// <summary>
    /// Extracts the minimal shape needed from a GitHub `pull_request` webhook payload, but
    /// only when the payload represents a merged pull request: the head (merged) branch, the
    /// base branch it was merged into, the repository URL, and its default branch.
    /// </summary>
    private static MergeWebhookPayload? ParseMergedPullRequestPayload(string rawBody)
    {
        using var document = JsonDocument.Parse(rawBody);
        var root = document.RootElement;

        if (!root.TryGetProperty("action", out var actionElement) ||
            !string.Equals(actionElement.GetString(), "closed", StringComparison.Ordinal))
        {
            return null;
        }

        if (!root.TryGetProperty("pull_request", out var pullRequestElement))
        {
            return null;
        }

        if (!pullRequestElement.TryGetProperty("merged", out var mergedElement) ||
            mergedElement.ValueKind != JsonValueKind.True)
        {
            return null;
        }

        if (!pullRequestElement.TryGetProperty("head", out var headElement) ||
            !headElement.TryGetProperty("ref", out var headRefElement))
        {
            return null;
        }

        var mergedBranch = headRefElement.GetString();
        if (string.IsNullOrEmpty(mergedBranch))
        {
            return null;
        }

        if (!pullRequestElement.TryGetProperty("base", out var baseElement) ||
            !baseElement.TryGetProperty("ref", out var baseRefElement))
        {
            return null;
        }

        var baseBranch = baseRefElement.GetString();
        if (string.IsNullOrEmpty(baseBranch))
        {
            return null;
        }

        var repoUrl = root.TryGetProperty("repository", out var repoElement) &&
                      repoElement.TryGetProperty("html_url", out var htmlUrlElement)
            ? htmlUrlElement.GetString() ?? string.Empty
            : string.Empty;

        var defaultBranch = root.TryGetProperty("repository", out var repoElement2) &&
                             repoElement2.TryGetProperty("default_branch", out var defaultBranchElement)
            ? defaultBranchElement.GetString() ?? "main"
            : "main";

        return new MergeWebhookPayload(repoUrl, mergedBranch, baseBranch, defaultBranch);
    }

    private sealed class GitHubWebhookMarker;
}
