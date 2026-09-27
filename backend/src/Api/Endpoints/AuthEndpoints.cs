using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Application.Dtos;
using Infrastructure.Identity;
using Infrastructure.Vault;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;

namespace Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/login", async (
            LoginRequest request,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            EniboardSecrets secrets,
            CancellationToken cancellationToken) =>
        {
            var user = await userManager.FindByNameAsync(request.Username);
            if (user is null)
            {
                return Results.Problem(
                    title: "Invalid credentials",
                    detail: "Username or password is incorrect.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var result = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
            if (!result.Succeeded)
            {
                return Results.Problem(
                    title: "Invalid credentials",
                    detail: "Username or password is incorrect.",
                    statusCode: StatusCodes.Status401Unauthorized);
            }

            var expiresAt = DateTimeOffset.UtcNow.AddHours(12);

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secrets.JwtSigningKey));
            var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName ?? request.Username),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            };

            var token = new JwtSecurityToken(
                issuer: "Eniboard",
                audience: "Eniboard",
                claims: claims,
                expires: expiresAt.UtcDateTime,
                signingCredentials: credentials);

            var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
            return Results.Ok(new LoginResponse(accessToken, expiresAt));
        })
        .AllowAnonymous()
        .RequireRateLimiting("login")
        .WithName("Login")
        .Produces<LoginResponse>()
        .Produces(StatusCodes.Status401Unauthorized);
    }
}
