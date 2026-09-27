namespace Application.Dtos;

public sealed record GitHubBranchResponse(string Name, bool Protected);

public sealed record ListBranchesResult(bool Configured, string? Message, IReadOnlyList<GitHubBranchResponse> Branches);

/// <summary>
/// Minimal shape extracted from a GitHub "push" webhook payload, after the caller has
/// already verified the branch was merged/pushed to the repository's default branch.
/// </summary>
public sealed record MergeWebhookPayload(string RepoUrl, string MergedBranch, string DefaultBranch);
