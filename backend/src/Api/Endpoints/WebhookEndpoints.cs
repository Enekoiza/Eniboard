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

            var payload = ParsePushPayload(rawBody);
            if (payload is null)
            {
                return Results.Ok(new { message = "Payload ignored: not a recognized push event." });
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
    /// Extracts the minimal shape needed from a GitHub "push" webhook payload: the ref
    /// that was pushed, the repository URL, and its default branch.
    /// </summary>
    private static MergeWebhookPayload? ParsePushPayload(string rawBody)
    {
        using var document = JsonDocument.Parse(rawBody);
        var root = document.RootElement;

        if (!root.TryGetProperty("ref", out var refElement))
        {
            return null;
        }

        var pushedRef = refElement.GetString() ?? string.Empty;
        const string branchPrefix = "refs/heads/";
        if (!pushedRef.StartsWith(branchPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var mergedBranch = pushedRef[branchPrefix.Length..];

        var repoUrl = root.TryGetProperty("repository", out var repoElement) &&
                      repoElement.TryGetProperty("html_url", out var htmlUrlElement)
            ? htmlUrlElement.GetString() ?? string.Empty
            : string.Empty;

        var defaultBranch = root.TryGetProperty("repository", out var repoElement2) &&
                             repoElement2.TryGetProperty("default_branch", out var defaultBranchElement)
            ? defaultBranchElement.GetString() ?? "main"
            : "main";

        return new MergeWebhookPayload(repoUrl, mergedBranch, defaultBranch);
    }

    private sealed class GitHubWebhookMarker;
}
