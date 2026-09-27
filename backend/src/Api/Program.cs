using System.Text;
using Api.Endpoints;
using Api.Middleware;
using Application.Validators;
using FluentValidation;
using Infrastructure;
using Infrastructure.Identity;
using Infrastructure.Migrations;
using Infrastructure.Vault;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// --- Secrets (Vault or local fallback) -------------------------------------------------
builder.Services.AddVaultSecrets(builder.Configuration);

using var bootstrapProvider = builder.Services.BuildServiceProvider();
var secretsProvider = bootstrapProvider.GetRequiredService<IVaultSecretsProvider>();
var secrets = await secretsProvider.GetSecretsAsync();

// --- Persistence + Identity --------------------------------------------------------------
// The MySQL DbContext registration is skipped under the "Testing" environment: integration
// tests register their own (SQLite) DbContextOptions<EniboardDbContext> via
// WebApplicationFactory.ConfigureTestServices instead.
if (!builder.Environment.IsEnvironment("Testing"))
{
    builder.Services.AddMySqlDbContext(secrets.DbConnectionString);
}

builder.Services.AddInfrastructure();

// --- FluentValidation ----------------------------------------------------------------------
builder.Services.AddValidatorsFromAssembly(typeof(CreateAppRequestValidator).Assembly);

// --- JWT auth --------------------------------------------------------------------------
builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "Eniboard",
            ValidateAudience = true,
            ValidAudience = "Eniboard",
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secrets.JwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });

builder.Services.AddAuthorization();

// --- CORS (configurable frontend origin, e.g. the Vercel deployment) --------------------
var frontendOrigin = builder.Configuration["Cors:FrontendOrigin"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        if (!string.IsNullOrWhiteSpace(frontendOrigin))
        {
            policy.WithOrigins(frontendOrigin)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

// --- Exception handling / Problem Details -----------------------------------------------
builder.Services.AddExceptionHandler<ProblemDetailsExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddOpenApi();

var app = builder.Build();

// --- Startup: apply migrations (DbUp) + seed user ---------------------------------------
// Skipped under the "Testing" environment: WebApplicationFactory-based integration tests
// swap in a SQLite DbContext via ConfigureTestServices and drive schema creation + seeding
// themselves (DbUp only targets MySQL here).
if (!app.Environment.IsEnvironment("Testing"))
{
    DatabaseMigrator.Migrate(secrets.DbConnectionString);

    using var scope = app.Services.CreateScope();
    await SeedUserInitializer.EnsureSeedUserAsync(scope.ServiceProvider);
}

app.UseExceptionHandler(_ => { });

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseCors("Frontend");

app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapAppEndpoints();
app.MapCardEndpoints();
app.MapWebhookEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in integration tests.
public partial class Program;
