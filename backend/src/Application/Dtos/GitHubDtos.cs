namespace Application.Dtos;

/// <summary>
/// Minimal shape extracted from a GitHub `pull_request` webhook whose PR was merged.
/// `MergedBranch` is `pull_request.head.ref` and `BaseBranch` is `pull_request.base.ref`.
/// </summary>
public sealed record MergeWebhookPayload(string RepoUrl, string MergedBranch, string BaseBranch, string DefaultBranch);
