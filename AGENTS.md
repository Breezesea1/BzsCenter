# Repository Guidelines

## Project Structure & Module Organization
The solution file is `BzsCenter.sln`. Main code lives under `src/`:
- `src/BzsCenter.Idp/`: ASP.NET Core + Blazor Server identity provider app (entry point: `Program.cs`, UI in `Components/`, static assets in `wwwroot/`, config in `appsettings*.json`).
- `src/Shared/BzsCenter.Shared.Infrastructure/`: shared infrastructure utilities (database migration helpers, telemetry, ASP.NET Core extensions).
- `docs/`: project documentation and design notes (see `docs/idp.md`).

## Build, Test, and Development Commands
Run from repository root unless noted:
- `dotnet restore BzsCenter.sln`: restore NuGet packages.
- `dotnet build BzsCenter.sln -c Debug`: compile all projects.
- `dotnet run --project src/BzsCenter.Idp`: start the IDP app locally.
- `dotnet watch --project src/BzsCenter.Idp run`: run with hot reload for UI/backend changes.
- `dotnet test BzsCenter.sln`: run tests (currently no test project is checked in, so add tests before relying on CI).

## Coding Style & Naming Conventions
Use standard C# conventions already present in the codebase:
- 4-space indentation, `nullable` and `implicit usings` enabled.
- `PascalCase` for types/methods/properties, `camelCase` for locals/parameters.
- Keep extension classes in `*Extensions.cs` (for example `ServiceExtensions.cs`).
- Keep Blazor components in `Components/` with `PascalCase.razor` names.
Before opening a PR, run `dotnet format` (if installed) and ensure `dotnet build` succeeds.

## Testing Guidelines
No dedicated test project exists yet. When adding tests:
- Create `tests/` at repo root (for example `tests/BzsCenter.Idp.Tests`).
- Prefer xUnit for .NET projects.
- Name test files as `<ClassName>Tests.cs` and methods as `MethodName_ShouldExpectedBehavior`.
- Run tests locally with `dotnet test` and include results in PR notes.

## Commit & Pull Request Guidelines
Current history uses placeholder messages (`first commit`), so use a consistent convention going forward:
- Commit format: `type(scope): short summary` (for example `feat(idp): add OpenIddict token endpoint config`).
- Keep commits focused and atomic.
- PRs should include: purpose, key changes, verification steps (commands run), linked issue/ticket, and screenshots for UI changes in `Components/`.

## Security & Configuration Tips
- Do not commit secrets; use environment variables or user secrets for sensitive settings.
- Keep production settings outside `appsettings.Development.json`.
- Validate HTTPS/reverse-proxy behavior when touching forwarded headers or auth flows.

## Agent-Specific Instructions
1. 使用 `dotnet-10-csharp-14` 的 skill。
