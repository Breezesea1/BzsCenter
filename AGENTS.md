# AGENTS.md

本文件为在 **BzsCenter** 仓库内工作的 agent（含 AI coding agents）提供统一执行规范。
目标：减少试错、保持一致性、优先可验证结果。

## 1. 仓库与技术栈
- Solution: `BzsCenter.sln`
- 项目：
  - `src/BzsCenter.Idp/BzsCenter.Idp.csproj`（ASP.NET Core Web）
  - `src/Shared/BzsCenter.Shared.Infrastructure/BzsCenter.Shared.Infrastructure.csproj`（共享基础设施库）
- 目标框架：`net10.0`
- C# 特性：`Nullable=enable`、`ImplicitUsings=enable`
- 核心依赖：OpenIddict、ASP.NET Core Identity、EF Core、OpenTelemetry

### 1.1 规则文件状态（已核对）
当前仓库未发现以下规则文件：
- `.cursorrules`
- `.cursor/rules/`
- `.github/copilot-instructions.md`
- `.editorconfig`
- `Directory.Build.props` / `Directory.Build.targets`
> 若后续新增以上规则，必须同步更新本文件。

## 2. 标准命令（Build / Lint / Test / Run）
以下命令均在仓库根目录执行。

### 2.1 Restore
```bash
dotnet restore /home/bzs/coding/BzsCenter/BzsCenter.sln
```

### 2.2 Build
```bash
dotnet build /home/bzs/coding/BzsCenter/BzsCenter.sln
dotnet build /home/bzs/coding/BzsCenter/BzsCenter.sln -c Release
```

### 2.3 Lint（格式校验）
```bash
dotnet format /home/bzs/coding/BzsCenter/BzsCenter.sln --verify-no-changes --verbosity minimal
```
自动修复：
```bash
dotnet format /home/bzs/coding/BzsCenter/BzsCenter.sln --verbosity minimal
```

### 2.4 Test
当前仓库暂无测试工程。新增测试后统一使用：
```bash
dotnet test /home/bzs/coding/BzsCenter/BzsCenter.sln --no-build
```

### 2.5 Run（IDP）
```bash
dotnet run --project /home/bzs/coding/BzsCenter/src/BzsCenter.Idp/BzsCenter.Idp.csproj
```

## 3. 单测“单例执行”模板（重点）
新增测试项目后，优先使用过滤执行，避免全量测试。

### 3.1 FullyQualifiedName 精确执行
```bash
dotnet test /home/bzs/coding/BzsCenter/BzsCenter.sln --filter "FullyQualifiedName=Namespace.ClassName.TestMethod"
```

### 3.2 名称片段模糊匹配
```bash
dotnet test /home/bzs/coding/BzsCenter/BzsCenter.sln --filter "FullyQualifiedName~ClassName"
```

### 3.3 Trait/Category 过滤（xUnit 推荐）
```bash
dotnet test /home/bzs/coding/BzsCenter/BzsCenter.sln --filter "Category=Integration"
```

### 3.4 调试输出
```bash
dotnet test /home/bzs/coding/BzsCenter/BzsCenter.sln -v normal
```

## 4. 建立良好测试体系（.NET 10 / C# 14）
仓库当前无测试工程，建议补齐：
- `tests/BzsCenter.Idp.UnitTests/`：纯单元测试（快）
- `tests/BzsCenter.Idp.IntegrationTests/`：集成测试（HTTP/数据库/中间件）

推荐包：
- `xunit`
- `xunit.runner.visualstudio`
- `Microsoft.NET.Test.Sdk`
- `coverlet.collector`
- `FluentAssertions`（推荐）

质量门槛建议：
1. PR 至少通过：`build + format verify + 受影响测试`
2. 认证链路（登录、token、迁移）必须有集成测试覆盖
3. 禁止删除失败测试来“过 CI”，必须修复根因

## 5. 代码风格（以仓库现状为准）
### 5.1 Imports / Usings
- 文件顶部 using，每行一个。
- 优先项目命名空间（`BzsCenter.*`），再 `Microsoft.*`。
- 禁止保留未使用 using。

### 5.2 命名规范
- 类型/方法/属性：`PascalCase`
- 参数/局部变量：`camelCase`
- 私有字段：`_camelCase`
- 私有静态只读字段（`private static readonly`）：`PascalCase`
- 常量：`PascalCase`
- 扩展类：`*Extensions`

### 5.3 格式与排版
- 4 空格缩进，不使用 Tab。
- 大括号采用 Allman 风格。
- 链式调用可换行并保持对齐。
- 提交前必须通过 `dotnet format --verify-no-changes`。

### 5.4 类型与空值
- 保持 Nullable 开启，不可关闭。
- 避免滥用 null-forgiving (`!`)。
- 明确 null 处理：参数校验、早返回、清晰异常。

### 5.5 异步与并发
- I/O 场景使用 `async/await`。
- 返回 `Task/Task<T>`，禁用 `async void`。
- 可取消操作必须传递 `CancellationToken`。

### 5.6 错误处理与日志
- 禁止空 `catch`。
- 捕获异常后记录上下文并保留异常链。
- 可恢复异常可有限重试（参考迁移逻辑）。
- 使用结构化日志参数，避免字符串拼接。

### 5.7 DI / 配置 / 数据访问
- 使用 `IServiceCollection` 扩展统一注册依赖。
- 配置绑定使用 `AddOptions<T>().Bind(configuration.GetSection(...))`。
- 关键配置缺失时 fail fast（抛 `InvalidOperationException`）。
- EF Core 配置集中在 `DbContext` 与扩展方法。

## 6. .NET 10 / C# 14 约定
- 允许并鼓励使用 C# 14 扩展块（仓库已有 `extension(IServiceCollection sc)`）。
- 新增 API 优先清晰 DI 组合与现代最小化写法。
- 时间处理统一 UTC 语义。
- 新依赖引入前评估必要性与兼容性。

## 7. Agent 执行流程（必须）
1. 先读代码再改，不凭猜测。
2. 变更尽量小步、可验证。
3. 每次改动后至少执行：
   - `dotnet build`
   - `dotnet format --verify-no-changes`
   - `dotnet test`（如存在测试）
4. 若无测试工程：至少提交“最小测试补齐计划”。
5. 认证/密钥/代理头相关改动必须附带测试或验证脚本。

## 8. 文档维护规则
- 新增测试工程、CI、`.editorconfig`、Cursor/Copilot 规则后，必须同步更新 AGENTS.md。
- 本文件面向 agent，要求“可执行、可验证、可复制”。
- 若命令与仓库实际不一致，以仓库文件与 CI 配置为准，并及时修订。
