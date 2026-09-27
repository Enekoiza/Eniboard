using Application.Interfaces;
using Application.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Api.Tests;

public class GitIntegrationServiceTests
{
    [Theory]
    [InlineData("https://github.com/owner/owner.github.io", "https://api.github.com/repos/owner/owner.github.io/branches")]
    [InlineData("https://github.com/owner/repo.git", "https://api.github.com/repos/owner/repo/branches")]
    [InlineData("https://github.com/owner/repo/", "https://api.github.com/repos/owner/repo/branches")]
    [InlineData("git@github.com:owner/repo.git", "https://api.github.com/repos/owner/repo/branches")]
    public async Task ListBranchesAsync_BuildsExpectedGitHubApiUrl(string repoUrl, string expectedUri)
    {
        var handler = new RecordingHandler();
        using var httpClient = new HttpClient(handler);
        var configuration = new ConfigurationBuilder().Build();

        var service = new GitIntegrationService(
            httpClient,
            db: null!,
            configuration,
            NullLogger<GitIntegrationService>.Instance);

        await service.ListBranchesAsync(repoUrl);

        Assert.NotNull(handler.RequestedUri);
        Assert.Equal(expectedUri, handler.RequestedUri!.ToString());
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        public Uri? RequestedUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedUri = request.RequestUri;
            var response = new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("[]", System.Text.Encoding.UTF8, "application/json"),
            };
            return Task.FromResult(response);
        }
    }
}
