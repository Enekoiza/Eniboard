using System.Net.Http.Json;
using Application.Dtos;

namespace Api.Tests;

internal static class TestAuthHelper
{
    public static async Task<string> LoginAndGetTokenAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/auth/login", new LoginRequest("testadmin", "TestPassword123!"));
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return body!.AccessToken;
    }

    public static async Task<HttpClient> CreateAuthenticatedClientAsync(EniboardWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        var token = await LoginAndGetTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
