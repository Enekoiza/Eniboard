using System.Text.RegularExpressions;
using Application.Dtos;
using Application.Interfaces;
using Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Application.Services;

public interface IGitIntegrationService
{
    Task HandleMergeWebhookAsync(MergeWebhookPayload payload, CancellationToken cancellationToken = default);
}

/// <summary>
/// Thin GitHub integration: reacts to merge webhooks by moving the linked card to the
/// board's Done column.
/// </summary>
public partial class GitIntegrationService(
    IEniboardDbContext db,
    ILogger<GitIntegrationService> logger) : IGitIntegrationService
{
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
            .Include(c => c.Board)
            .ThenInclude(b => b!.Columns)
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
            var doneColumn = card.Board!.Columns.FirstOrDefault(c => c.Name == BoardColumn.DoneName);

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

    private static bool TryParseRepo(string? url, out string owner, out string repo)
    {
        owner = repo = string.Empty;
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var match = GitHubRepoRegex().Match(url);
        if (!match.Success)
        {
            return false;
        }

        owner = match.Groups["owner"].Value;
        repo = match.Groups["repo"].Value;
        return true;
    }

    private static string? NormalizeRepoKey(string? url)
    {
        return TryParseRepo(url, out var owner, out var repo) ? $"{owner}/{repo}".ToLowerInvariant() : null;
    }
}
