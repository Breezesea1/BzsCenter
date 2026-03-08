# AGENTS.md

本文件为在 **BzsCenter** 仓库内工作的 agent（含 AI coding agents）提供统一执行规范。
目标：减少试错、保持一致性、优先可验证结果。

---

## 1. 仓库结构与技术栈

```
BzsCenter/
├── src/
│   ├── BzsCenter.AppHost/                   # .NET Aspire 编排宿主
│   ├── BzsCenter.AppHost.ServiceDefaults/    # Aspire 服务默认值（OTEL、健康检查）
│   ├── BzsCenter.Idp/                        # ASP.NET Core Web（Identity Provider）
│   └── Shared/
│       └── BzsCenter.Shared.Infrastructure/  # 共享库：Cache、Auth、DB、Telemetry
├── tests/
│   ├── BzsCenter.Idp.UnitTests/             # 纯单元测试（xUnit + NSubstitute）
│   └── BzsCenter.Idp.IntegrationTests/      # 集成测试（TestHost + EF SQLite）
└── BzsCenter.sln
```

- 目标框架：`net10.0`，C# 14
- C# 特性：`Nullable=enable`、`ImplicitUsings=enable`（全项目统一）
- 核心依赖：OpenIddict 7.x、ASP.NET Core Identity、EF Core 10、OpenTelemetry、.NET Aspire 13.x
- 前端：Tailwind CSS（`npm run css:build`）+ Blazor Server 交互组件

### 1.1 规则文件状态
当前仓库**未发现**以下规则文件：`.cursorrules`、`.cursor/rules/`、`.github/copilot-instructions.md`、`.editorconfig`、`Directory.Build.props`。
> 若后续新增，必须同步更新本文件。

---

## 2. 标准命令（必须在仓库根目录执行）

### 2.1 Aspire 运行（首选开发入口）
```bash
# 在 BzsCenter.sln 同级目录执行，自动启动 Postgres、Redis、IDP
aspire run
```

### 2.2 单独运行 IDP
```bash
dotnet run --project src/BzsCenter.Idp/BzsCenter.Idp.csproj
```

### 2.3 Restore / Build
```bash
dotnet restore BzsCenter.sln
dotnet build BzsCenter.sln
dotnet build BzsCenter.sln -c Release
```

### 2.4 Lint（格式校验）
```bash
# 校验（CI 使用）
dotnet format BzsCenter.sln --verify-no-changes --verbosity minimal

# 自动修复
dotnet format BzsCenter.sln --verbosity minimal
```

### 2.5 数据库迁移
```bash
# 在 src/BzsCenter.Idp/ 目录下
dotnet ef migrations add <MigrationName> --context IdpDbContext
dotnet ef database update --context IdpDbContext
```

---

## 3. 测试命令（重点）

测试工程已存在：`BzsCenter.Idp.UnitTests`（xUnit 2.9 + NSubstitute 5.x）和 `BzsCenter.Idp.IntegrationTests`（TestHost + EF SQLite）。

### 3.1 全量测试
```bash
dotnet test BzsCenter.sln
dotnet test BzsCenter.sln --no-build   # 已构建时使用
```

### 3.2 单个测试（精确匹配）
```bash
dotnet test BzsCenter.sln --filter "FullyQualifiedName=BzsCenter.Idp.UnitTests.Controllers.PermissionScopesControllerTests.GetByPermission_WhenPermissionEmpty_ReturnsValidationProblem"
```

### 3.3 按类名模糊匹配
```bash
dotnet test BzsCenter.sln --filter "FullyQualifiedName~PermissionScopesControllerTests"
dotnet test BzsCenter.sln --filter "FullyQualifiedName~PermissionAuthorizationHandler"
```

### 3.4 按 Trait/Category 过滤
```bash
dotnet test BzsCenter.sln --filter "Category=Integration"
dotnet test BzsCenter.sln --filter "Category=Unit"
```

### 3.5 仅运行单元测试工程
```bash
dotnet test tests/BzsCenter.Idp.UnitTests/BzsCenter.Idp.UnitTests.csproj
```

### 3.6 调试输出
```bash
dotnet test BzsCenter.sln -v normal
```

---

## 4. 代码风格

### 4.1 Imports / Usings
- 文件顶部显式 `using`，每行一个，按命名空间字母排序。
- 优先顺序：`System.*` → `Microsoft.*` → `BzsCenter.*` → 第三方。
- 禁止保留未使用 using；ImplicitUsings 已启用，无需 `using System;` 等基础命名空间。
- 全局 `using Xunit;` 在测试项目 csproj `<Using>` 中声明，测试文件无需重复。

### 4.2 命名规范
| 类别 | 规范 | 示例 |
|------|------|------|
| 类型 / 方法 / 属性 | `PascalCase` | `PermissionScopeService` |
| 参数 / 局部变量 | `camelCase` | `connectionString` |
| 私有字段 | `_camelCase` | `_dbContext` |
| 常量 | `PascalCase` | `ClaimType` |
| 扩展类 | `*Extensions` | `ServiceExtensions` |
| 测试类 | `{Subject}Tests` | `PermissionScopesControllerTests` |
| 测试方法 | `{Method}_{Condition}_{Expected}` | `GetByPermission_WhenPermissionEmpty_ReturnsValidationProblem` |

### 4.3 格式与排版
- **4 空格**缩进，不使用 Tab。
- 大括号 **Allman 风格**（左括号独占一行）。
- 链式调用换行对齐（见 `AppHost.cs`）。
- 提交前必须通过 `dotnet format --verify-no-changes`。

### 4.4 类型与空值
- `Nullable=enable` 不可关闭，全项目生效。
- 避免滥用 null-forgiving (`!`)，只在确认不为 null 时使用。
- 空值处理：优先早返回 + 明确 `ArgumentException.ThrowIfNullOrEmpty` / `ArgumentNullException.ThrowIfNull`。

### 4.5 异步与并发
- I/O 场景全部使用 `async/await`，返回 `Task`/`Task<T>`。
- **禁用 `async void`**（事件处理除外）。
- 可取消操作必须接受并传递 `CancellationToken`。

### 4.6 错误处理与日志
- **禁止空 `catch {}`**；捕获后必须记录上下文。
- 使用结构化日志参数（`logger.LogError("{Key} failed", value)`），不拼接字符串。
- 关键配置缺失时 fail fast（`ArgumentException.ThrowIfNullOrEmpty` 或 `InvalidOperationException`）。
- 可恢复失败（如数据库）可有限重试，EF Core 已配置 `EnableRetryOnFailure`。

### 4.7 DI / 配置 / 数据访问
- 用 `IServiceCollection` 扩展方法统一注册，扩展类标记 `internal static`。
- 配置绑定使用 `AddOptions<T>().Bind(configuration.GetSection(...))`。
- EF Core 配置集中在 `DbContext` 的 `OnModelCreating` 与 `EntityConfig` 类。
- 同时注册 `AddDbContext` 和 `AddDbContextFactory`（见 `InfraServiceExtensions.cs`）。

---

## 5. C# 14 / .NET 10 约定

- **扩展块**（`extension(T x) { ... }`）：仓库已使用，新 DI 扩展优先此方式。
- 使用集合表达式（`[item1, item2]`）代替 `new List<T> { ... }`。
- 时间统一 UTC 语义；前端展示由 UI 层转换。
- 新依赖引入前评估必要性与 Aspire 兼容性（优先使用 `Aspire.*` 集成包）。
- 最小化 API 注册：`minimal API` 或 Controller，不混用。

---

## 6. 测试规范

### 6.1 单元测试模式
- **框架**：xUnit 2.9 + NSubstitute 5.x（Mock）+ 内置 `Assert`（不使用 FluentAssertions）。
- **结构**：Arrange → Act → Assert，无多余注释。
- **Mock**：`Substitute.For<IInterface>()`，验证调用用 `Received(n)` / `DidNotReceive()`。
- 测试类 `sealed`（Controller Tests），普通测试类无修饰符。
- 每个 `[Fact]` 只验证一个行为；参数化用 `[Theory] + [InlineData]`。

### 6.2 集成测试模式
- 使用 `Microsoft.AspNetCore.TestHost` + EF Core SQLite 内存数据库。
- 测试工程已引用 `Microsoft.AspNetCore.TestHost 10.0.3`。

### 6.3 质量门槛
1. 每次 PR 至少通过：`build + format verify + 受影响测试`。
2. 认证/token/迁移 链路必须有集成测试。
3. **禁止删除失败测试**来"过 CI"，必须修复根因。

### 6.4 测试产物
- 测试过程中产生的截图、对比图、录屏封面等图片文件必须放在 `tests/images/` 目录下，不得散落在仓库根目录或业务源码目录。
- 若图片仅用于本地临时排查且无需纳入版本控制，完成验证后应立即删除。

---

## 7. Agent 执行流程（必须）

1. **先读代码再改**，不凭猜测，不假设文件存在。
2. 变更小步、可验证；每步改动后执行：
   ```bash
   dotnet build BzsCenter.sln
   dotnet format BzsCenter.sln --verify-no-changes
   dotnet test BzsCenter.sln  # 若存在相关测试
   ```
3. 认证 / 密钥 / 代理头相关改动必须附带测试或验证脚本。
4. 新增功能前检查 `BzsCenter.Shared.Infrastructure` 是否已有可复用实现。
5. 新增 NuGet 包前运行 `dotnet restore` 验证兼容性。

---

## 8. 文档维护规则

- 新增项目、测试工程、CI、`.editorconfig`、Cursor/Copilot 规则后，必须同步更新 AGENTS.md。
- 本文件面向 agent，要求"可执行、可验证、可复制"。
- 若命令与仓库实际不一致，以仓库文件与 CI 配置为准，及时修订。
