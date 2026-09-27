using System.Net;
using System.Net.Http.Json;
using Application.Dtos;
using Infrastructure.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
    public async Task Login_WithMissingPassword_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new { username = "testadmin" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithEmptyBody_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithWhitespaceUsername_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest("   ", "x"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest("testadmin", "WrongPassword!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithUnknownUsername_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest("no-such-user", "WrongPassword!"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public void Startup_WithShortJwtSigningKey_Throws()
    {
        using var factory = new EniboardWebApplicationFactory();
        using var shortKey = factory.WithWebHostBuilder(b => b.UseSetting("Eniboard:JwtSigningKey", "too-short"));

        var ex = Assert.Throws<InvalidOperationException>(() => shortKey.CreateClient());
        Assert.Contains("32 bytes", ex.Message);
    }

    [Fact]
    public async Task Login_AfterMaxFailedAttempts_LocksOutEvenWithCorrectPassword()
    {
        using var scope = _factory.Services.CreateScope();
        var maxFailedAccessAttempts = scope.ServiceProvider.GetRequiredService<IOptions<IdentityOptions>>().Value.Lockout.MaxFailedAccessAttempts;
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync("testadmin");
        Assert.NotNull(user);

        for (var i = 0; i < maxFailedAccessAttempts - 1; i++)
        {
            await userManager.AccessFailedAsync(user!);
        }

        var client = _factory.CreateClient();

        var wrongPasswordResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequest("testadmin", "WrongPassword!"));
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPasswordResponse.StatusCode);

        var correctPasswordResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequest("testadmin", "TestPassword123!"));
        Assert.Equal(HttpStatusCode.Unauthorized, correctPasswordResponse.StatusCode);
    }

    [Fact]
    public async Task Login_AfterFiveFailedAttempts_DoesNotLockOutAccount()
    {
        var client = _factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            var failedResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequest("testadmin", "WrongPassword!"));
            Assert.Equal(HttpStatusCode.Unauthorized, failedResponse.StatusCode);
        }

        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByNameAsync("testadmin");
        Assert.NotNull(user);
        Assert.False(await userManager.IsLockedOutAsync(user!));
    }

    [Fact]
    public async Task Login_ExceedingRateLimit_Returns429()
    {
        var client = _factory.CreateClient();

        for (var i = 0; i < 5; i++)
        {
            var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest("testadmin", "WrongPassword!"));
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var throttledResponse = await client.PostAsJsonAsync("/auth/login", new LoginRequest("testadmin", "WrongPassword!"));
        Assert.Equal(HttpStatusCode.TooManyRequests, throttledResponse.StatusCode);
    }

    [Fact]
    public async Task Login_RateLimit_IsPerForwardedClientIp()
    {
        for (var i = 0; i < 5; i++)
        {
            var response = await SendLoginWithForwardedForAsync("203.0.113.1");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var throttledResponse = await SendLoginWithForwardedForAsync("203.0.113.1");
        Assert.Equal(HttpStatusCode.TooManyRequests, throttledResponse.StatusCode);

        var otherClientResponse = await SendLoginWithForwardedForAsync("203.0.113.2");
        Assert.Equal(HttpStatusCode.Unauthorized, otherClientResponse.StatusCode);
    }

    [Fact]
    public async Task Login_RateLimit_GroupsIpv6ClientsBy64Prefix()
    {
        for (var i = 0; i < 5; i++)
        {
            var response = await SendLoginWithForwardedForAsync("2001:db8:1:1::1");
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        var sameSubnetResponse = await SendLoginWithForwardedForAsync("2001:db8:1:1::2");
        Assert.Equal(HttpStatusCode.TooManyRequests, sameSubnetResponse.StatusCode);

        var differentSubnetResponse = await SendLoginWithForwardedForAsync("2001:db8:1:2::1");
        Assert.Equal(HttpStatusCode.Unauthorized, differentSubnetResponse.StatusCode);
    }

    private async Task<HttpResponseMessage> SendLoginWithForwardedForAsync(string forwardedFor)
    {
        var payload = JsonContent.Create(new LoginRequest("testadmin", "WrongPassword!"));
        var body = await payload.ReadAsStringAsync();

        var context = await _factory.Server.SendAsync(ctx =>
        {
            // The forwarded-headers middleware ignores requests with a null remote IP, which
            // is otherwise always the case on the in-process TestServer.
            ctx.Connection.RemoteIpAddress = IPAddress.Loopback;
            ctx.Request.Scheme = "http";
            ctx.Request.Host = new HostString("localhost");
            ctx.Request.Method = "POST";
            ctx.Request.Path = "/auth/login";
            ctx.Request.Headers["X-Forwarded-For"] = forwardedFor;
            ctx.Request.ContentType = "application/json";

            var bytes = System.Text.Encoding.UTF8.GetBytes(body);
            ctx.Request.Body = new MemoryStream(bytes);
            ctx.Request.ContentLength = bytes.Length;

            // TestServer's HttpContextBuilder computes body-detection up front, before this
            // callback runs, so it doesn't see the body assigned above; without this the JSON
            // body binder treats the request as bodyless and short-circuits with a bare 400.
            ctx.Features.Set<IHttpRequestBodyDetectionFeature>(new AlwaysHasBodyFeature());
        });

        var responseBody = await new StreamReader(context.Response.Body).ReadToEndAsync();
        return new HttpResponseMessage((HttpStatusCode)context.Response.StatusCode)
        {
            Content = new StringContent(responseBody),
        };
    }

    private sealed class AlwaysHasBodyFeature : IHttpRequestBodyDetectionFeature
    {
        public bool CanHaveBody => true;
    }
}
