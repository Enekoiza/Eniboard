# Eniboard — Backend

.NET 10 minimal API backend for Eniboard: a personal Kanban board where each "app" (one of
your own software projects) gets a board, and cards represent pending changes to that app.

## Stack

- **.NET 10** minimal APIs, nullable reference types enabled everywhere.
- **EF Core 9** + **Pomelo.EntityFrameworkCore.MySql 9.0.0** for MySQL persistence (see
  [Package version note](#package-version-note) below).
- **DbUp** (`dbup-mysql`) for schema migrations — plain SQL scripts applied on startup,
  independent of EF Core's own migration tooling.
- **ASP.NET Core Identity** + **JWT bearer auth** (single seeded user).
- **FluentValidation** for request validation.
- **xUnit** + `WebApplicationFactory<Program>` (SQLite in-memory) for integration tests.
- **HashiCorp Vault** for secrets — this app targets a single real (production) environment,
  always backed by a self-hosted Vault instance; see [Configure secrets](#2-configure-secrets).

## Solution layout

```
backend/
├── Eniboard.slnx              # solution (the .NET 10 SDK's dotnet new sln now emits .slnx)
├── src/
│   ├── Api/                   # minimal API endpoints, Program.cs, appsettings
│   ├── Application/           # services, DTOs, FluentValidation validators, exceptions
│   ├── Domain/                # entities + enums, no external dependencies
│   └── Infrastructure/        # EF Core DbContext, DbUp SQL migrations, Identity, Vault client
└── tests/
    └── Api.Tests/             # xUnit integration tests (WebApplicationFactory + SQLite)
```

## Running locally

### 1. Prerequisites

- .NET 10 SDK.
- A MySQL server (8.0+) reachable from your machine, **or** just run the test suite, which
  needs no external database (see below).

### 2. Configure secrets

This app has a single real environment (`appsettings.json`), always backed by Vault
(`UseVault: true`). Provide:

- `VAULT_ADDR` — base address of your Vault server.
- `VAULT_TOKEN` — a token with read access to the configured KV path.
- `Vault:SecretPath` (optional, defaults to `v1/secret/data/eniboard`) — must contain the
  keys `db-connection-string`, `jwt-signing-key`, `seed-username`, `seed-password`.

Also set:

- `GitHub:WebhookSecret` — shared secret configured on your GitHub webhook (used to verify
  the `X-Hub-Signature-256` header on `POST /webhooks/github`).
- `GitHub:Token` — optional; only needed to list branches for private repos.
- `Cors:FrontendOrigin` — the deployed frontend origin (e.g. your Vercel URL).

For an ad-hoc local run without a real Vault reachable, set `UseVault=false` and provide the
same values directly via user secrets or environment variables instead:

```bash
cd src/Api
dotnet user-secrets init
dotnet user-secrets set "UseVault" "false"
dotnet user-secrets set "Eniboard:DbConnectionString" "server=localhost;port=3306;database=eniboard;user=eniboard;password=<your-password>"
dotnet user-secrets set "Eniboard:JwtSigningKey" "<a long random string, 32+ bytes>"
dotnet user-secrets set "Eniboard:SeedUsername" "admin"
dotnet user-secrets set "Eniboard:SeedPassword" "<a strong password>"
```

### 3. Apply migrations

Schema migrations are plain SQL scripts under `src/Infrastructure/Migrations/Scripts/`
(`NNNN_description.sql`, applied in filename order), run automatically on startup via
[DbUp](https://dbup.readthedocs.io/) (`DatabaseMigrator.Migrate(...)` in `Program.cs`). DbUp
tracks what has already run in its own `SchemaVersions` table in the target database — no EF
Core migration tooling is involved. Simply running the app against a reachable MySQL server
is enough; there is no separate "apply migrations" step to run by hand.

To change the schema, add a new script (e.g. `0002_add_something.sql`) to that folder — never
edit an already-applied script, since DbUp tracks scripts by name and content hash.

### 4. Run

```bash
dotnet build
dotnet run --project src/Api
```

On first run, since no users exist yet, `SeedUserInitializer` creates exactly one user from
the seed username/password. It is idempotent — it never touches an existing user's
password, and there is no endpoint anywhere that changes it.

Log in via `POST /auth/login` with `{ "username": "...", "password": "..." }` to get a JWT,
then send it as `Authorization: Bearer <token>` on the other endpoints.

### 5. Run tests

```bash
dotnet test
```

The test suite needs **no external database** — it boots the real API pipeline through
`WebApplicationFactory<Program>` with an in-memory SQLite database swapped in for MySQL
(Pomelo doesn't support EF Core's InMemory provider well, so SQLite is used instead, per
the project's own testing guidance). The app's `Program.cs` skips the MySQL registration and
the auto-migrate/seed startup step specifically under the `"Testing"` ASP.NET Core
environment, which the test factory sets; the test factory then creates the SQLite schema
and seeds a test user itself.

## API surface

| Endpoint | Auth | Description |
|---|---|---|
| `POST /auth/login` | anonymous | Username + password → JWT access token. |
| `GET /apps` | Bearer | List all apps. |
| `POST /apps` | Bearer | Create an app; also creates its board with the 4 default columns. |
| `GET /apps/{id}/board` | Bearer | Get a board's columns + cards. |
| `POST /cards` | Bearer | Create a card. |
| `GET /cards/{id}` | Bearer | Get a card. |
| `PUT /cards/{id}` | Bearer | Update a card's title/description/type/priority. |
| `DELETE /cards/{id}` | Bearer | Delete a card. |
| `PATCH /cards/{id}/move` | Bearer | Move a card to another column (enforces WIP limits; accepts `linkedBranch` when moving into Doing). |
| `POST /webhooks/github` | HMAC signature | GitHub push webhook; moves the card linked to the merged branch to Done. |

Errors are returned as RFC 7807 Problem Details: validation failures → 400, not-found → 404,
WIP-limit violations → 409.

## Package version note

**Pomelo.EntityFrameworkCore.MySql 9.0.0** is the package used here. At the time this backend
was built, Pomelo had not yet published a stable release targeting EF Core 10 (its latest
stable is `9.0.0`, targeting EF Core 9) — see
https://www.nuget.org/packages/Pomelo.EntityFrameworkCore.MySql for the current state. To
keep the whole stack consistent, all `Microsoft.EntityFrameworkCore.*`,
`Microsoft.AspNetCore.Identity.EntityFrameworkCore` and
`Microsoft.AspNetCore.Authentication.JwtBearer` packages are also pinned to **9.0.0**, even
though every project still targets `net10.0`. This is safe (net10.0 apps can reference
net8.0/net9.0-targeted libraries) and should be revisited once Pomelo ships an EF Core
10-targeted release.

## Deviations from the original spec

- **`.slnx` instead of `.sln`**: `dotnet new sln` on the installed .NET 10 SDK generates the
  newer XML-based `.slnx` format by default. It's a solution file either way; `dotnet build`,
  `dotnet test`, and IDEs that support .NET 10 all work with it directly.
- **Enums location**: `CardType` and `Priority` live under `src/Domain/Enums/` rather than
  an `enums/` subfolder inside `Entities/`, to keep entities and enums as sibling top-level
  folders.
- **`IEniboardDbContext` abstraction**: the Application layer depends on an
  `IEniboardDbContext` interface (in `Application/Interfaces`) rather than the concrete EF
  Core `DbContext`, so Application has no project reference to Infrastructure. It's
  implemented by `EniboardDbContext` in Infrastructure.
- **DbContext registration split**: `Infrastructure.DependencyInjection.AddMySqlDbContext(...)`
  (production MySQL wiring) is kept separate from `AddInfrastructure()` (Identity + app
  services), and `Program.cs` only calls the former outside the `"Testing"` environment, so
  integration tests can register their own SQLite `DbContextOptions<EniboardDbContext>`
  cleanly instead of the two competing to configure the same context.
- **Migrate + seed skipped in `"Testing"` environment**: `Program.cs` skips
  `DatabaseMigrator.Migrate(...)` and `SeedUserInitializer` when
  `IWebHostEnvironment.EnvironmentName == "Testing"`, since DbUp only targets MySQL here. The
  test `WebApplicationFactory` performs the equivalent setup itself (`EnsureCreatedAsync` +
  seeding) against its SQLite connection.
- **DbUp instead of EF Core Migrations**: schema changes are hand-written SQL scripts run by
  DbUp, not `dotnet ef migrations`. This trades away EF's auto-generated migrations for a
  single, explicit source of truth for the schema — chosen because this is a self-hosted,
  single-user, single-environment app going straight to production, where the up-front
  simplicity of one deploy path matters more than migration codegen.
