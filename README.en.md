# BzsCenter

[简体中文](./README.zh-CN.md)

BzsCenter is a `.NET 10` solution for an identity platform centered on `BzsCenter.Idp`, an ASP.NET Core + Blazor application that uses OpenIddict, EF Core, Redis, PostgreSQL, and .NET Aspire for local orchestration.

## Repository structure

```text
BzsCenter/
├── src/
│   ├── BzsCenter.AppHost/                 # Aspire entrypoint
│   ├── BzsCenter.AppHost.ServiceDefaults/ # service defaults / OTEL / health
│   ├── BzsCenter.Idp/                     # main web app
│   ├── BzsCenter.Idp.Client/              # client-side/shared UI services
│   ├── BzsCenter.Idp.Migrator/            # database migration executable
│   └── Shared/
│       └── BzsCenter.Shared.Infrastructure/
└── tests/
    ├── BzsCenter.Idp.UnitTests/
    ├── BzsCenter.Idp.IntegrationTests/
    └── BzsCenter.Idp.E2ETests/
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

`src/BzsCenter.AppHost/AppHost.cs` defines the local runtime graph:

- PostgreSQL with a persistent data volume
- Redis
- `BzsCenter.Idp`
- `BzsCenter.Idp.Migrator`

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
dotnet restore BzsCenter.sln
dotnet build BzsCenter.sln
```

### Run the full local stack

```bash
aspire run
```

### Run only the IDP app

```bash
dotnet run --project src/BzsCenter.Idp/BzsCenter.Idp.csproj
```

### Frontend assets

Run these from `src/BzsCenter.Idp/` when needed:

```bash
npm install
npm run css:build
npm run css:watch
npm run gsap:copy
```

`BzsCenter.Idp.csproj` already runs `css:build` and `gsap:copy` before `Build` and `Publish`. `npm install` is not automatically re-run when `package-lock.json` already exists, so run it manually after dependency changes or on a fresh machine.

### Database migrations

From `src/BzsCenter.Idp/`:

```bash
dotnet ef migrations add <MigrationName> --context IdpDbContext
dotnet ef database update --context IdpDbContext
```

## Tests

### Run all tests

```bash
dotnet test BzsCenter.sln
```

### Run one test project

```bash
dotnet test tests/BzsCenter.Idp.UnitTests/BzsCenter.Idp.UnitTests.csproj
dotnet test tests/BzsCenter.Idp.IntegrationTests/BzsCenter.Idp.IntegrationTests.csproj
dotnet test tests/BzsCenter.Idp.E2ETests/BzsCenter.Idp.E2ETests.csproj
```

### Run a single test

```bash
dotnet test tests/BzsCenter.Idp.UnitTests/BzsCenter.Idp.UnitTests.csproj --filter "FullyQualifiedName=BzsCenter.Idp.UnitTests.Controllers.PermissionScopesControllerTests.GetByPermission_WhenPermissionEmpty_ReturnsValidationProblem"
dotnet test tests/BzsCenter.Idp.E2ETests/BzsCenter.Idp.E2ETests.csproj --filter "FullyQualifiedName=BzsCenter.Idp.E2ETests.AuthExperienceE2ETests.LoginPage_AllowsThemeAndLanguageSwitching"
```

### Test layers

- Unit tests: xUnit + NSubstitute + bUnit
- Integration tests: ASP.NET Core TestHost + SQLite
- E2E tests: Playwright + Aspire

Local E2E execution depends on the `aspire` CLI being installed and available on `PATH`.

## Documentation

- Agent-focused repo guidance: [AGENTS.md](./AGENTS.md)
- Deployment planning note: [docs/github-cicd-ubuntu-docker-plan.md](./docs/github-cicd-ubuntu-docker-plan.md)

## Notes for deployment

The current repository is optimized for local development with Aspire. The checked-in docs under `docs/github-cicd-ubuntu-docker-plan.md` recommend deploying the web app and migrator as production services rather than moving the AppHost directly into production.
