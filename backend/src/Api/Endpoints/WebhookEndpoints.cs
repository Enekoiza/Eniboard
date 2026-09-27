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

            MergeWebhookPayload? payload;
            try
            {
                payload = ParseMergedPullRequestPayload(rawBody);
            }
            catch (JsonException)
            {
                return Results.Problem(
                    title: "Malformed webhook payload",
                    detail: "The request body is not valid JSON.",
                    statusCode: StatusCodes.Status400BadRequest);
            }

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
        .Produces(StatusCodes.Status400BadRequest)
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

        if (!TryGetString(root, "action", out var action) ||
            !string.Equals(action, "closed", StringComparison.Ordinal))
        {
            return null;
        }

        if (!TryGetObject(root, "pull_request", out var pullRequestElement))
        {
            return null;
        }

        if (!pullRequestElement.TryGetProperty("merged", out var mergedElement) ||
            mergedElement.ValueKind != JsonValueKind.True)
        {
            return null;
        }

        if (!TryGetObject(pullRequestElement, "head", out var headElement) ||
            !TryGetString(headElement, "ref", out var mergedBranch) ||
            string.IsNullOrEmpty(mergedBranch))
        {
            return null;
        }

        if (!TryGetObject(pullRequestElement, "base", out var baseElement) ||
            !TryGetString(baseElement, "ref", out var baseBranch) ||
            string.IsNullOrEmpty(baseBranch))
        {
            return null;
        }

        var repoUrl = string.Empty;
        var defaultBranch = "main";
        if (TryGetObject(root, "repository", out var repositoryElement))
        {
            if (TryGetString(repositoryElement, "html_url", out var htmlUrl))
            {
                repoUrl = htmlUrl;
            }

            if (TryGetString(repositoryElement, "default_branch", out var branch))
            {
                defaultBranch = branch;
            }
        }

        return new MergeWebhookPayload(repoUrl, mergedBranch, baseBranch, defaultBranch);
    }

    /// <summary>
    /// True only if <paramref name="parent"/> is a JSON object, the property exists, and its
    /// value is itself an object. Guards against malformed/unexpected-shape webhook bodies
    /// (e.g. the property being a string or array) throwing <see cref="InvalidOperationException"/>.
    /// </summary>
    private static bool TryGetObject(JsonElement parent, string propertyName, out JsonElement value)
    {
        if (parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty(propertyName, out var candidate) &&
            candidate.ValueKind == JsonValueKind.Object)
        {
            value = candidate;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// True only if <paramref name="parent"/> is a JSON object, the property exists, and its
    /// value is a JSON string. Guards against malformed/unexpected-shape webhook bodies
    /// throwing <see cref="InvalidOperationException"/> from <c>GetString()</c>.
    /// </summary>
    private static bool TryGetString(JsonElement parent, string propertyName, out string value)
    {
        value = string.Empty;
        if (parent.ValueKind == JsonValueKind.Object &&
            parent.TryGetProperty(propertyName, out var candidate) &&
            candidate.ValueKind == JsonValueKind.String)
        {
            value = candidate.GetString()!;
            return true;
        }

        return false;
    }

    private sealed class GitHubWebhookMarker;
}
