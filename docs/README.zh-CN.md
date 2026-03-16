# BzsCenter

[English](./README.en.md) | [仓库根目录 README](../README.md)

BzsCenter 是一个基于 `.NET 10` 的身份平台解决方案，核心应用为 `BzsCenter.Idp`。它使用 ASP.NET Core、Blazor、OpenIddict、EF Core、Redis、PostgreSQL，并通过 .NET Aspire 编排本地开发环境。

## 仓库结构

```text
BzsCenter/
├── src/
│   ├── BzsCenter.AppHost/                 # Aspire 本地编排入口
│   ├── BzsCenter.AppHost.ServiceDefaults/ # 服务默认配置 / OTEL / 健康检查
│   ├── BzsCenter.Idp/                     # 主 Web 应用
│   ├── BzsCenter.Idp.Client/              # 客户端 / 共享 UI 服务
│   ├── BzsCenter.Idp.Migrator/            # 数据库迁移可执行项目
│   └── Shared/
│       └── BzsCenter.Shared.Infrastructure/
└── tests/
    ├── BzsCenter.Idp.UnitTests/
    ├── BzsCenter.Idp.IntegrationTests/
    └── BzsCenter.Idp.E2ETests/
```

## 技术栈

- .NET 10 / C#
- ASP.NET Core + Blazor
- OpenIddict
- EF Core + PostgreSQL
- Redis
- .NET Aspire
- Tailwind CSS + GSAP 前端资源流水线
- xUnit、NSubstitute、bUnit、Playwright

## 本地运行方式

`src/BzsCenter.AppHost/AppHost.cs` 定义了本地开发时的运行拓扑：

- PostgreSQL（带数据卷）
- Redis
- `BzsCenter.Idp`
- `BzsCenter.Idp.Migrator`

AppHost 会把配置注入应用，并在 migrator 完成后再让主 IDP 服务进入就绪状态。

在开发环境下，如果未显式配置管理员账号，AppHost 会提供默认值：

- 用户名：`admin`
- 密码：`Passw0rd!`

## 环境要求

- .NET SDK 10
- Node.js 与 npm
- 已安装并可在 `PATH` 中访问的 Aspire CLI
- Aspire 支持的容器运行时，例如 Docker Desktop

## 快速开始

### 还原与构建

```bash
dotnet restore BzsCenter.sln
dotnet build BzsCenter.sln
```

### 启动完整本地环境

```bash
aspire run
```

### 仅启动 IDP 应用

```bash
dotnet run --project src/BzsCenter.Idp/BzsCenter.Idp.csproj
```

### 前端资源命令

在 `src/BzsCenter.Idp/` 目录中按需执行：

```bash
npm install
npm run css:build
npm run css:watch
npm run gsap:copy
```

`BzsCenter.Idp.csproj` 已经会在 `Build` 和 `Publish` 前自动执行 `css:build` 与 `gsap:copy`。但当 `package-lock.json` 已存在时，`npm install` 不会自动重新执行，因此首次拉取或依赖变更后请手动运行。

### 数据库迁移

在 `src/BzsCenter.Idp/` 目录执行：

```bash
dotnet ef migrations add <MigrationName> --context IdpDbContext
dotnet ef database update --context IdpDbContext
```

## 测试

### 运行全部测试

```bash
dotnet test BzsCenter.sln
```

### 运行单个测试项目

```bash
dotnet test tests/BzsCenter.Idp.UnitTests/BzsCenter.Idp.UnitTests.csproj
dotnet test tests/BzsCenter.Idp.IntegrationTests/BzsCenter.Idp.IntegrationTests.csproj
dotnet test tests/BzsCenter.Idp.E2ETests/BzsCenter.Idp.E2ETests.csproj
```

### 运行单个测试

```bash
dotnet test tests/BzsCenter.Idp.UnitTests/BzsCenter.Idp.UnitTests.csproj --filter "FullyQualifiedName=BzsCenter.Idp.UnitTests.Controllers.PermissionScopesControllerTests.GetByPermission_WhenPermissionEmpty_ReturnsValidationProblem"
dotnet test tests/BzsCenter.Idp.E2ETests/BzsCenter.Idp.E2ETests.csproj --filter "FullyQualifiedName=BzsCenter.Idp.E2ETests.AuthExperienceE2ETests.LoginPage_AllowsThemeAndLanguageSwitching"
```

### 测试分层

- 单元测试：xUnit + NSubstitute + bUnit
- 集成测试：ASP.NET Core TestHost + SQLite
- 端到端测试：Playwright + Aspire

本地运行 E2E 测试依赖 `aspire` CLI 已正确安装并可通过 `PATH` 调用。

## 文档

- 面向代码代理的仓库说明：[AGENTS.md](../AGENTS.md)
- GitHub CI/CD 与 Ubuntu Docker 部署方案：[github-cicd-ubuntu-docker-plan.md](./github-cicd-ubuntu-docker-plan.md)

## 部署说明

当前仓库更偏向使用 Aspire 进行本地开发编排。已提交的 `docs/github-cicd-ubuntu-docker-plan.md` 建议在生产环境中部署 `BzsCenter.Idp` 与 `BzsCenter.Idp.Migrator`，而不是直接把 AppHost 原样搬到生产环境。
