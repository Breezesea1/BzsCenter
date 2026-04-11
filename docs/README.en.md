# BzsOIDC

[简体中文](./README.zh-CN.md) | [Repository README](../README.md)

BzsOIDC is a `.NET 10` solution for an identity platform centered on `BzsOIDC.Idp`, an ASP.NET Core + Blazor application that uses OpenIddict, EF Core, Redis, PostgreSQL, and .NET Aspire for local orchestration.

## Repository structure

```text
BzsOIDC/
├── src/
│   ├── BzsOIDC.AppHost/                 # Aspire entrypoint
│   ├── BzsOIDC.AppHost.ServiceDefaults/ # service defaults / OTEL / health
│   ├── BzsOIDC.Idp/                     # main web app
│   ├── BzsOIDC.Idp.Client/              # client-side/shared UI services
│   ├── BzsOIDC.Idp.Migrator/            # database migration executable
│   └── Shared/
│       └── BzsOIDC.Shared.Infrastructure/
└── tests/
    ├── BzsOIDC.Idp.UnitTests/
    ├── BzsOIDC.Idp.IntegrationTests/
    └── BzsOIDC.Idp.E2ETests/
```

## Tech stack

- .NET 10 / C#
- ASP.NET Core + Blazor
- OpenIddict
- EF Core + PostgreSQL
- Redis
- .NET Aspire
- Tailwind CSS + GSAP asset pipeline
- xUnit, NSubstitute, bUnit, Playwright

## How the app runs locally

`src/BzsOIDC.AppHost/AppHost.cs` defines the local runtime graph:

- PostgreSQL with a persistent data volume
- Redis
- `BzsOIDC.Idp`
- `BzsOIDC.Idp.Migrator`

The AppHost wires configuration into the app and waits for the migrator to complete before the main IDP service is considered ready.

In development, the AppHost provides default admin credentials when they are not configured:

- Username: `admin`
- Password: `Passw0rd!`

## Prerequisites

- .NET SDK 10
- Node.js and npm
- Aspire CLI available on `PATH`
- A container runtime supported by Aspire, such as Docker Desktop

## Quick start

### Restore and build

```bash
dotnet restore BzsOIDC.sln
dotnet build BzsOIDC.sln
```

### Run the full local stack

```bash
aspire run
```

### Run only the IDP app

```bash
dotnet run --project src/BzsOIDC.Idp/BzsOIDC.Idp.csproj
```

### Frontend assets

Run these from `src/BzsOIDC.Idp/` when needed:

```bash
npm install
npm run css:build
npm run css:watch
npm run gsap:copy
```

`BzsOIDC.Idp.csproj` already runs `css:build` and `gsap:copy` before `Build` and `Publish`. `npm install` is not automatically re-run when `package-lock.json` already exists, so run it manually after dependency changes or on a fresh machine.

### Database migrations

From `src/BzsOIDC.Idp/`:

```bash
dotnet ef migrations add <MigrationName> --context IdpDbContext
dotnet ef database update --context IdpDbContext
```

## Tests

### Run all tests

```bash
dotnet test BzsOIDC.sln
```

### Run one test project

```bash
dotnet test tests/BzsOIDC.Idp.UnitTests/BzsOIDC.Idp.UnitTests.csproj
dotnet test tests/BzsOIDC.Idp.IntegrationTests/BzsOIDC.Idp.IntegrationTests.csproj
dotnet test tests/BzsOIDC.Idp.E2ETests/BzsOIDC.Idp.E2ETests.csproj
```

### Run a single test

```bash
dotnet test tests/BzsOIDC.Idp.UnitTests/BzsOIDC.Idp.UnitTests.csproj --filter "FullyQualifiedName=BzsOIDC.Idp.UnitTests.Controllers.PermissionScopesControllerTests.GetByPermission_WhenPermissionEmpty_ReturnsValidationProblem"
dotnet test tests/BzsOIDC.Idp.E2ETests/BzsOIDC.Idp.E2ETests.csproj --filter "FullyQualifiedName=BzsOIDC.Idp.E2ETests.AuthExperienceE2ETests.LoginPage_AllowsThemeAndLanguageSwitching"
```

### Test layers

- Unit tests: xUnit + NSubstitute + bUnit
- Integration tests: ASP.NET Core TestHost + SQLite
- E2E tests: Playwright + Aspire

Local E2E execution depends on the `aspire` CLI being installed and available on `PATH`.

## Documentation

- Agent-focused repo guidance: [AGENTS.md](../AGENTS.md)
- Deployment planning note: [github-cicd-ubuntu-docker-plan.md](./github-cicd-ubuntu-docker-plan.md)

## Notes for deployment

The current repository is optimized for local development with Aspire. The checked-in docs under `docs/github-cicd-ubuntu-docker-plan.md` recommend deploying the web app and migrator as production services rather than moving the AppHost directly into production.
