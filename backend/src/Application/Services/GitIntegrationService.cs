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
        if (!string.Equals(payload.BaseBranch, payload.DefaultBranch, StringComparison.Ordinal))
        {
            logger.LogInformation(
                "Ignoring pull request merged into non-default branch '{Base}' (default is '{Default}')",
                payload.BaseBranch,
                payload.DefaultBranch);
            return;
        }

        var repoKey = NormalizeRepoKey(payload.RepoUrl);
        if (repoKey is null)
        {
            logger.LogInformation("Merge webhook has no recognizable repository URL; ignoring.");
            return;
        }

        var candidates = await db.Cards
            .Include(c => c.Column)
            .Include(c => c.Board)
            .ThenInclude(b => b!.App)
            .Where(c => c.LinkedBranch == payload.MergedBranch && c.Column!.Name != BoardColumn.DoneName)
            .ToListAsync(cancellationToken);

        var matchingCards = candidates
            .Where(c => NormalizeRepoKey(c.Board?.App?.RepoUrl) == repoKey)
            .ToList();

        if (matchingCards.Count == 0)
        {
            logger.LogInformation(
                "Merge webhook for branch {Branch} in repository {Repo} did not match any linked card.",
                payload.MergedBranch,
                repoKey);
            return;
        }

        foreach (var card in matchingCards)
        {
            var doneColumn = await db.BoardColumns
                .FirstOrDefaultAsync(c => c.BoardId == card.BoardId && c.Name == BoardColumn.DoneName, cancellationToken);

            if (doneColumn is null)
            {
                logger.LogWarning("Board {BoardId} has no '{DoneColumn}' column; cannot auto-move card {CardId}.", card.BoardId, BoardColumn.DoneName, card.Id);
                continue;
            }

            card.ColumnId = doneColumn.Id;
            card.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    [GeneratedRegex(@"github\.com[:/](?<owner>[^/\s]+)/(?<repo>[^/\s]+?)(?:\.git)?/?$", RegexOptions.IgnoreCase)]
    private static partial Regex GitHubRepoRegex();

    private static string? NormalizeRepoKey(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var match = GitHubRepoRegex().Match(url);
        if (!match.Success)
        {
            return null;
        }

        var owner = match.Groups["owner"].Value;
        var repo = match.Groups["repo"].Value.TrimEnd('/').Replace(".git", string.Empty, StringComparison.OrdinalIgnoreCase);

        return $"{owner}/{repo}".ToLowerInvariant();
    }

    private sealed record GitHubBranchDto(string Name, bool Protected);
}
