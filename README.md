# BzsCenter

[English](#english) | [简体中文](#简体中文)

## English

BzsCenter is a `.NET 10` identity platform solution centered on `BzsCenter.Idp`, built with ASP.NET Core, Blazor, OpenIddict, EF Core, and .NET Aspire.

### Highlights

- Aspire AppHost orchestrates the local development stack.
- `BzsCenter.Idp` is the main web application.
- `BzsCenter.Idp.Migrator` handles database migration and seeding.
- The repository includes unit, integration, and end-to-end tests.

### Quick start

```bash
dotnet restore BzsCenter.sln
dotnet build BzsCenter.sln
aspire run
```

### Documentation

For full setup and operational details, use the guides under `docs/`.

- Full English guide: [docs/README.en.md](./docs/README.en.md)
- Deployment planning note: [docs/github-cicd-ubuntu-docker-plan.md](./docs/github-cicd-ubuntu-docker-plan.md)
- Agent guidance: [AGENTS.md](./AGENTS.md)

## 简体中文

BzsCenter 是一个基于 `.NET 10` 的身份平台解决方案，核心应用为 `BzsCenter.Idp`，使用 ASP.NET Core、Blazor、OpenIddict、EF Core 和 .NET Aspire 构建。

### 仓库亮点

- 使用 Aspire AppHost 编排本地开发环境。
- `BzsCenter.Idp` 是主 Web 应用。
- `BzsCenter.Idp.Migrator` 负责数据库迁移与种子初始化。
- 仓库包含单元测试、集成测试与端到端测试。

### 快速开始

```bash
dotnet restore BzsCenter.sln
dotnet build BzsCenter.sln
aspire run
```

### 文档

更完整的环境说明与运行细节请查看 `docs/` 下的文档。

- 完整中文说明： [docs/README.zh-CN.md](./docs/README.zh-CN.md)
- GitHub CI/CD 与 Ubuntu Docker 部署方案： [docs/github-cicd-ubuntu-docker-plan.md](./docs/github-cicd-ubuntu-docker-plan.md)
- 面向代理的仓库说明： [AGENTS.md](./AGENTS.md)
