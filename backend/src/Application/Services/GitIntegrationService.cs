using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Application.Dtos;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public interface IGitIntegrationService
{
    Task<ListBranchesResult> ListBranchesAsync(string repoUrl, CancellationToken cancellationToken = default);

    Task HandleMergeWebhookAsync(MergeWebhookPayload payload, CancellationToken cancellationToken = default);
}

/// <summary>
/// Thin GitHub integration: lists branches for a repo (public repos work without a
/// token; a configured token additionally allows private repos) and reacts to merge
/// webhooks by moving the linked card to the board's Done column.
/// </summary>
public partial class GitIntegrationService(
    HttpClient httpClient,
    IEniboardDbContext db,
    IConfiguration configuration,
    ILogger<GitIntegrationService> logger) : IGitIntegrationService
{
    public async Task<ListBranchesResult> ListBranchesAsync(string repoUrl, CancellationToken cancellationToken = default)
    {
        var match = GitHubRepoRegex().Match(repoUrl);
        if (!match.Success)
        {
            return new ListBranchesResult(false, $"'{repoUrl}' is not a recognizable github.com repository URL.", []);
        }

        var owner = match.Groups["owner"].Value;
        var repo = match.Groups["repo"].Value.TrimEnd('/').Replace(".git", string.Empty, StringComparison.OrdinalIgnoreCase);

        var token = configuration["GitHub:Token"];

        using var request = new HttpRequestMessage(HttpMethod.Get, $"https://api.github.com/repos/{owner}/{repo}/branches");
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Eniboard", "1.0"));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                if (string.IsNullOrWhiteSpace(token))
                {
                    return new ListBranchesResult(
                        false,
                        $"GitHub returned {(int)response.StatusCode}. No GitHub token is configured (GitHub:Token) — only public repositories are supported without one.",
                        []);
                }

                return new ListBranchesResult(false, $"GitHub returned {(int)response.StatusCode}.", []);
            }

            var branches = await response.Content.ReadFromJsonAsync<List<GitHubBranchDto>>(cancellationToken: cancellationToken)
                ?? [];

            return new ListBranchesResult(
                true,
                null,
                branches.Select(b => new GitHubBranchResponse(b.Name, b.Protected)).ToList());
        }
        catch (HttpRequestException ex)
        {
            logger.LogWarning(ex, "Failed to reach GitHub while listing branches for {RepoUrl}", repoUrl);
            return new ListBranchesResult(false, "Could not reach GitHub.", []);
        }
    }

    public async Task HandleMergeWebhookAsync(MergeWebhookPayload payload, CancellationToken cancellationToken = default)
    {
        if (string.Equals(payload.MergedBranch, payload.DefaultBranch, StringComparison.Ordinal))
        {
            // The default branch itself was pushed to directly; there is no feature branch to link.
            logger.LogInformation("Ignoring push to the default branch '{Branch}' itself.", payload.DefaultBranch);
            return;
        }

        var card = await db.Cards
            .FirstOrDefaultAsync(c => c.LinkedBranch == payload.MergedBranch, cancellationToken);

        if (card is null)
        {
            logger.LogInformation("Merge webhook for branch {Branch} did not match any linked card.", payload.MergedBranch);
            return;
        }

        var doneColumn = await db.BoardColumns
            .FirstOrDefaultAsync(c => c.BoardId == card.BoardId && c.Name == BoardColumn.DoneName, cancellationToken);

        if (doneColumn is null)
        {
            logger.LogWarning("Board {BoardId} has no '{DoneColumn}' column; cannot auto-move card {CardId}.", card.BoardId, BoardColumn.DoneName, card.Id);
            return;
        }

        card.ColumnId = doneColumn.Id;
        card.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }

    [GeneratedRegex(@"github\.com[:/](?<owner>[^/\s]+)/(?<repo>[^/\s]+?)(?:\.git)?/?$", RegexOptions.IgnoreCase)]
    private static partial Regex GitHubRepoRegex();

    private sealed record GitHubBranchDto(string Name, bool Protected);
}
