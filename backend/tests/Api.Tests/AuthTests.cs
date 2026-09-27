using System.Net;
using System.Net.Http.Json;
using Application.Dtos;

namespace Api.Tests;

public class AuthTests : IAsyncLifetime
{
    private readonly EniboardWebApplicationFactory _factory = new();

    public Task InitializeAsync() => _factory.InitializeAsync();

    public Task DisposeAsync()
    {
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Login_WithSeedUserCredentials_ReturnsJwt()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest("testadmin", "TestPassword123!"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest("testadmin", "WrongPassword!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_AfterFiveFailedAttempts_LocksOutEvenWithCorrectPassword()
    {
        var client = _factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            var failedResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequest("testadmin", "WrongPassword!"));
            Assert.Equal(HttpStatusCode.Unauthorized, failedResponse.StatusCode);
        }

        var correctPasswordResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequest("testadmin", "TestPassword123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, correctPasswordResponse.StatusCode);
    }

    [Fact]
    public async Task Login_ExceedingRateLimit_Returns429()
    {
        var client = _factory.CreateClient();

        for (var i = 0; i < 10; i++)
        {
            var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest("testadmin", "WrongPassword!"));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var throttledResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequest("testadmin", "WrongPassword!"));
        Assert.Equal(HttpStatusCode.TooManyRequests, throttledResponse.StatusCode);
    }
}
