# IDP Code Review 跟踪（2026-03-09）

## 背景

本记录用于沉淀本轮针对 `BzsCenter.Idp` 的 code review 结果，并基于以下两类证据校准结论：

- **仓库内代码与测试**：`src/BzsCenter.Idp`、`src/BzsCenter.Idp.Client`、`src/Shared/BzsCenter.Shared.Infrastructure`、`tests/BzsCenter.Idp.*`
- **外部基线**：Blazor 官方安全/项目结构文档、OpenIddict 官方架构与 claim destinations 文档

本轮 review 重点关注：

- Blazor 项目结构与认证页面放置方式
- OIDC / OpenIddict / Identity 实现边界
- 与 DDD 的对齐程度
- 稳定性、模块化、维护性
- 已完成事项与后续整改路径

---

## 本轮 review 校准后的结论摘要

### 1. 架构定位

当前 `BzsCenter.Idp` 更准确地说是：

- **以 ASP.NET Core Identity + OpenIddict 为核心的分层 IDP 宿主**
- **Server Host + Client Assembly 的混合式 Blazor Web App**
- **带有 Models/Services/Infra 命名的模块化应用**

它**不是严格意义上的 DDD 实现**，但也不是完全无边界的“代码堆叠式”应用。

更准确的判断是：

- 有明确的 Controller / Service / Infra 分层；
- 有 `Models/` 目录，但当前实体模型很薄；
- 核心认证/授权流仍以 ASP.NET Core Identity、OpenIddict 的框架对象为主导；
- 更接近**框架驱动的模块化分层应用**，而不是聚合根/值对象/领域事件明确的 DDD 应用。

### 2. 关键校准结论

#### 2.1 DDD 对齐情况

- 当前 `Models/` 仅包含 `BzsUser`、`BzsRole`、`PermissionScopeMapping`：
  - `BzsUser : IdentityUser<Guid>`
  - `BzsRole : IdentityRole<Guid>`
  - `PermissionScopeMapping` 为简单数据映射对象
- 这些类型当前**主要承载 Identity/EF Core 持久化模型职责**，并未体现明显的：
  - 聚合根边界
  - 值对象
  - 领域不变量封装
  - 领域事件

本轮已将 `Domain/` 重命名为 `Models/`，以减少“项目已走 DDD”的误导；代码事实仍更接近**Identity-centric persistence model**。

#### 2.2 Blazor 结构判断

- `Program.cs` 在 `BzsCenter.Idp` 中通过 `AddRazorComponents().AddInteractiveWebAssemblyComponents()` 启动 Razor Components，并通过 `AddAdditionalAssemblies(typeof(BzsCenter.Idp.Client._Imports).Assembly)` 引入 `BzsCenter.Idp.Client`。
- 登录页面 `src/BzsCenter.Idp.Client/Components/Pages/Account/Login.razor` 位于 `.Client` 项目中。
- 但登录表单本身是：
  - `<form method="post" action="/account/login">`
  - 由 `src/BzsCenter.Idp/Controllers/AccountController.cs` 处理密码登录

这意味着：

- **凭据校验发生在服务器端**，不是“WASM 在浏览器里直接处理登录逻辑”；
- 但认证关键页面的 UI 仍依赖 `.Client` 项目与交互式组件装配，**比官方推荐的保守型 auth surface 更激进**。

结合 Microsoft 对 Blazor 安全相关 UI 的建议，当前结构**可运行、也不等于立即不安全**，但对于 IDP 来说，不是最保守、最易长期维护的放置方式。

#### 2.3 OIDC / OpenIddict 边界判断

- `ConnectController` 直接处理 `/connect/authorize`、`/connect/token`、`/connect/userinfo`、`/connect/logout`。
- `IdpServiceRegistrar` 已显式启用：
  - `EnableAuthorizationEndpointPassthrough()`
  - `EnableTokenEndpointPassthrough()`
  - `EnableEndSessionEndpointPassthrough()`
  - `EnableUserInfoEndpointPassthrough()`

这说明当前 `ConnectController` 的实现方式**符合 OpenIddict passthrough 模式的典型用法**。

因此，本轮文档应避免把“controller 直接编排 OIDC”简单定性为设计错误。更准确的说法应该是：

- **协议端点放在 controller 中是合理的**；
- 但部分管理类接口（如 `OidcClientsController`）是否继续直接依赖 `IOpenIddictApplicationManager`，仍有进一步收口到应用服务层的空间。

#### 2.4 稳定性与可维护性判断

当前 IDP 的稳定性基础比文档初稿更好，原因是：

- 核心 OIDC flow 已有宿主级集成测试覆盖；
- `PermissionClaimDestinationsHandler` 已明确控制 claims 发放目的地；
- `ConnectController` 已在签发前显式构建 OIDC principal，并与 destination 规则共用同一套投影逻辑；
- `OidcClientDescriptorFactory` 已改为使用 `RandomNumberGenerator` 生成 confidential client secret；
- `AccountController` 对登录回跳使用 `Url.IsLocalUrl` 做了本地地址约束；
- `IdpServiceRegistrar` 将认证、Identity、OpenIddict 的装配集中在一处。

但仍存在几类真实风险：

- client registration 的部分产品策略被硬编码；
- `Models/` 当前仍以 Identity/EF 实体为主，尚不构成真正 DDD 领域模型；
- 认证 UI 放在 `.Client` 项目，对 IDP 这种高敏感场景不是最保守的结构。

---

## 已确认优点

### 1. 服务装配相对集中

- `src/BzsCenter.Idp/Services/IdpServiceRegistrar.cs` 统一处理：
  - options 绑定
  - Data Protection
  - Identity
  - OpenIddict
  - claims destination handler

这比把认证相关注册散落在 `Program.cs` 中更利于维护。

### 2. OIDC 主链路已有真实集成测试

`tests/BzsCenter.Idp.IntegrationTests/ConnectControllerIntegrationTests.cs` 已覆盖：

- 未登录访问 `/connect/authorize`
- unsupported grant 访问 `/connect/token`
- 未携带 token 访问 `/connect/userinfo`
- client_credentials flow
- authorization code flow
- refresh token flow
- bearer token 访问 `/connect/userinfo`
- `/api/oidc/clients` 的创建与读回

这对 IDP 稳定性非常关键，说明系统已经从“只有代码、没有真实链路验证”进步到“关键协议主路径有宿主级回归保护”。

### 3. claims 发放边界已有集中控制点

`src/BzsCenter.Idp/Services/Oidc/PermissionClaimDestinationsHandler.cs` 已根据 scopes 控制：

- `name`
- `email`
- `role`
- permission claims
- `sub`

进入 access token / identity token 的方式。

此外，`ConnectController` 已在 `SignIn` 前直接复用 `ApplyDestinationsAsync(...)`，避免 controller 签发点与 OpenIddict 事件链路发生规则漂移。

这意味着 role / email / name / permission claims 的投影**不仅已实现**，且已经通过 token / id token / userinfo 集成测试获得回归保护；后续仍可补 handler 级单元测试来覆盖更细粒度边界分支。

### 4. 登录回跳具备基本安全约束

`src/BzsCenter.Idp/Controllers/AccountController.cs` 中的登录/登出跳转已通过 `Url.IsLocalUrl` 限制回跳地址，避免开放重定向问题被轻易引入。

### 5. 初始化职责已从 Web 启动路径中拆出

当前已完成：

- 新增 `src/BzsCenter.Idp.Migrator/` 独立初始化入口；
- `BzsCenter.Idp/Program.cs` 不再直接执行 `MigrateAsync()` / `SeedAsync()`；
- `BzsCenter.Idp.Migrator/Program.cs` 已复用：
  - `AddIdpService(...)`
  - `AddMigration<IdpDbContext>(...)`
  - `IdentitySeeder.SeedAsync()`
- `src/BzsCenter.AppHost/AppHost.cs` 已改为让 IDP `WaitForCompletion(idp-migrator)`。

这意味着 migration / seeding 已经从主站点启动路径中抽离，主宿主只承担 Web 服务职责，而初始化职责由独立资源负责。

注意：

- 单独运行 `BzsCenter.Idp` 时，数据库初始化不再由 Web 启动自动承担；
- 本地与编排环境应优先通过 `BzsCenter.Idp.Migrator` 或 Aspire AppHost 完成初始化，再启动 IDP。

### 6. `/connect/authorize` 的 challenge 语义已按真实浏览器行为校准

本轮通过真实宿主 + 浏览器验证确认：

- 在 `web-client` 有效存在的前提下，未登录访问 `/connect/authorize` 时，原先的 `401 + Location` 不会把真实浏览器带到登录页；
- 由于 `Program.cs` 中启用了 `UseStatusCodePagesWithReExecute("/not-found")`，浏览器最终会看到 not-found 页面，而不是 login 页面；
- 因此当前系统已经针对 `/connect/authorize` 做了定向 cookie challenge override，改为直接 `302` 到 `LoginPath`。

这使该 endpoint 的真实用户体验与 OIDC interactive authorize flow 的预期对齐，同时保留其他 endpoint 的 401 行为不被一刀切改变。

---

## 主要问题与风险分级

### 高优先级

#### 1. `Models/` 命名已更贴近事实，但当前模型仍较薄

当前 `Models/` 中的类型更像：

- Identity 实体扩展
- 权限范围映射表模型

而不是高行为密度的领域模型。

当前这类误导已经明显降低，但仍建议继续保持文档对“非严格 DDD”的明确表述，避免把实体模型误读为领域模型。

#### 2. Controller / Service 边界风格已改善，但仍可继续统一

当前边界大致如下：

- `PermissionScopesController` → 应用服务式调用（`IPermissionScopeService`）
- `ConnectController` → 协议端点 + 框架对象直接编排（合理）
- `OidcClientsController` → 管理端 API 已通过 `IOidcClientService` 收口到应用服务层

因此当前结论更准确地说是：

- **协议控制器** 与 **管理类控制器** 的抽象深度仍不同，但已经更符合各自职责；
- 其中 `ConnectController` 直接使用框架 manager 不应被视为反模式；
- `OidcClientsController` 已不再直接承载 descriptor 构建、存在性检查和 manager orchestration；
- 后续可继续统一的重点，转为：
  - 产品策略是否继续硬编码在 OIDC client 管理流程中
  - 失败分支与审计/日志边界是否还需加强

#### 3. OIDC client 产品策略存在硬编码假设

当前状态已经比 review 初稿更清晰：

- 当前 onboarding 策略已在 `OidcClientDescriptorFactory` / `OidcClientService` 中显式化；
- 当前只支持两类 **first-party** client：
  - `FirstPartyInteractive`：public + `authorization_code` / `refresh_token` + PKCE + redirect URI
  - `FirstPartyMachine`：confidential + `client_credentials`

`OidcClientDescriptorFactory` 当前固定：

- `FirstPartyInteractive` → `ConsentType = Implicit`
- `FirstPartyMachine` → `ConsentType = External`
- interactive profile 强制 PKCE
- unsupported 组合（如 confidential + authorization_code）直接返回 validation error

这意味着当前系统已经明确回答了“当前支持什么”，但仍有一个中长期问题没有完全回答：

- 当前是否会扩展到真正的 third-party / self-service onboarding？
- 如果未来要扩展，是否仍沿用当前 profile 设计，还是引入更细的 client trust model？

如果这些问题不提前澄清，后续实现仍可能在产品、协议、安全之间返工；但相比之前，当前代码至少已经不再把“任何组合都默认支持”当成隐式前提。

### 中长期

#### 4. 测试覆盖已经明显改善，但仍有空白区域

当前已有较好的 integration coverage，但对失败分支和管理面行为的覆盖仍可继续补齐：

- `AccountController` 的错误输入/安全跳转分支
- `OidcClientsController` 的冲突、更新、删除、非法输入分支
- `PermissionClaimDestinationsHandler` 的 handler 级边界断言（当前已有 token / userinfo 链路验证）
- `OidcClientDescriptorFactory` 的校验与 secret 生成规则

重点不再是“有没有 OIDC 集成测试”，而是“claims、管理面、失败分支是否也有回归保护”。

#### 5. 认证与管理职责未来可继续模块化

从长期看，可按职责继续拆分为：

- 协议入口（OIDC endpoints）
- 认证账户管理（login/logout/profile）
- OIDC client 管理 API
- 权限/角色/scope 管理 API

这样会更利于后续迭代时避免“所有东西都堆在一个 IDP 宿主里”。

---

## 本轮已完成事项

- [x] 阅读 `src/BzsCenter.Idp`、`src/BzsCenter.Idp.Client`、`src/Shared/BzsCenter.Shared.Infrastructure` 的边界与职责
- [x] 复核 `Program.cs`、`ConnectController`、`OidcClientsController`、`PermissionScopesController`、`AccountController`、OIDC/Identity 服务层与测试层
- [x] 结合 Blazor / OpenIddict 官方文档重新校准原 review 结论
- [x] 确认当前项目**不是严格 DDD**，但具备基本分层与模块化基础
- [x] 确认当前登录页虽然位于 `.Client` 项目，但凭据提交与校验仍走服务端 `AccountController`
- [x] 确认 `ConnectController` 的 passthrough 模式符合 OpenIddict 的典型使用方式
- [x] 确认 `PermissionClaimDestinationsHandler` 已实现 role/email/name/permission claims 的 destination 控制
- [x] 确认 `tests/BzsCenter.Idp.IntegrationTests/ConnectControllerIntegrationTests.cs` 已覆盖核心 OIDC 主链路
- [x] 将 `OidcClientDescriptorFactory.GenerateClientSecret()` 升级为基于 `RandomNumberGenerator` 的高熵生成实现
- [x] 在 `ConnectController` 签发前显式构建 OIDC principal，并复用 `ApplyDestinationsAsync(...)` 保持签发规则一致
- [x] 新增并通过 claims projection 回归验证：
  - [x] access token claims 投影
  - [x] id token claims 投影
  - [x] `/connect/userinfo` claims 投影
  - [x] openid-only scope 下的负向断言
- [x] 新增 `OidcClientDescriptorFactory` 的 secret 格式与唯一性单元测试
- [x] 新增 `src/BzsCenter.Idp.Migrator/` 独立初始化项目，承接 `IdpDbContext` migration + `IdentitySeeder` seeding
- [x] 从 `BzsCenter.Idp/Program.cs` 移除直接 `MigrateAsync()` / `SeedAsync()` 启动逻辑
- [x] 让 `BzsCenter.AppHost/AppHost.cs` 通过 `WaitForCompletion(idp-migrator)` 编排初始化资源先完成，再启动 IDP
- [x] 通过真实浏览器 + AppHost 运行环境确认 `/connect/authorize` 未登录时的最终用户可见行为
- [x] 对 `/connect/authorize` 增加定向 cookie redirect override，避免真实浏览器落到 not-found 页面
- [x] 将 `ConnectControllerIntegrationTests` 的未登录断言更新为 redirect login 语义
- [x] 将 `/login` 从 `BzsCenter.Idp.Client` 收敛到 `BzsCenter.Idp` server-owned surface
- [x] 将 client 导航中的 `/login` 入口改为 full reload，避免被 client router 拦截
- [x] 新增 `LoginPage_WhenRequestedDirectly_ReturnsServerOwnedLoginForm` 回归验证
- [x] 将 `/account/denied` 补齐为 `BzsCenter.Idp` 的 server-owned 页面，避免 `AccessDeniedPath` 落回 client not-found
- [x] 新增 `AccessDeniedPage_WhenRequestedDirectly_ReturnsServerOwnedDeniedPage` 回归验证
- [x] 将 `/logout` 补齐为 `BzsCenter.Idp` 的 server-owned 页面，并复用现有 `POST /account/logout` 语义
- [x] 新增 `LogoutPage_WhenRequestedDirectly_ReturnsServerOwnedLogoutForm` 回归验证
- [x] 通过真实浏览器确认 `/account/denied`、`/logout` 都落在新的 server-owned 页面，且 logout 表单可以正常提交
- [x] 为 `OidcClientsController` 增加 `IOidcClientService` / `OidcClientService`，将 OpenIddict manager orchestration 收口到应用服务层
- [x] 新增 `OidcClientsControllerTests`，验证 controller 的 HTTP 结果映射与服务委托行为
- [x] 新增 OIDC client 管理面的失败分支回归验证：
  - [x] 重复创建返回 `409 Conflict`
  - [x] 非法请求返回 `400 ValidationProblem`
  - [x] 更新不存在 client 返回 `404 NotFound`
  - [x] 删除不存在 client 返回 `404 NotFound`
- [x] 将 current client onboarding 策略显式化为两类 first-party profile：
  - [x] `FirstPartyInteractive`
  - [x] `FirstPartyMachine`
- [x] 将 unsupported 组合（如 confidential + authorization_code）改为显式 validation failure
- [x] 为 interactive / machine / unsupported onboarding profile 补齐单元与集成测试
- [x] 补齐 OIDC client 管理面的 update/delete 成功路径回归验证
- [x] 补齐更细粒度的 profile strategy 测试（interactive PKCE、machine redirect URI 等）
- [x] 明确当前实现仅支持 first-party client onboarding；third-party / self-service onboarding 在当前仓库中不受支持
- [x] 为 `PermissionClaimDestinationsHandler` 补齐 handler 级单测，直接覆盖 scope → claim destination 规则

---

## 待办事项

### 高优先级

- [x] 在文档中明确当前架构定位，避免 `Domain/` 命名被误读为严格 DDD 实现
- [x] 将 `Domain/` 目录重命名为 `Models/`，减少目录命名与架构事实的偏差

### 中优先级

- [x] 继续评估 consent 等剩余 auth-sensitive UI 是否也应迁回 `.Idp`
- [x] 继续补 OIDC client 管理面的更新/删除成功路径与更细粒度策略测试
- [x] 评估未来是否需要引入真正的 third-party / self-service client trust model
- [x] 补 `PermissionClaimDestinationsHandler` 的 handler 级单测

### 中长期

- [x] 将 `Domain/` 重命名为 `Models/`，消除当前实现与目录命名的明显偏差
- [x] 在当前实现范围内，协议入口 / 认证账户 / client 管理 / 权限管理的模块边界已经显著清晰化
- [x] 在当前 first-party-only 实现范围内，剩余 auth-sensitive UI 已不再留在 `.Client`

---

## 下一步实施方案（推荐）

### 当前状态

本轮之后，当前实现范围内的主要 review backlog 已闭环：

- migration / seeding 已从 Web 启动路径中拆出；
- `/connect/authorize` 的 challenge 语义已按真实浏览器行为校准；
- `/login`、`/account/denied`、`/logout` 已全部收口到 `.Idp` server-owned surface；
- `OidcClientsController` 已收口到应用服务层；
- current client onboarding 策略已显式化为 first-party profiles；
- claims destination 规则已同时拥有端到端测试与 handler 级单测覆盖。

### 后续条件性议题

1. **评估 third-party / self-service onboarding 是否需要真实进入 roadmap**  
   如果 future roadmap 需要扩展，就在现有 first-party profile 之外引入明确 trust model，而不是继续靠隐式默认值扩展。

2. **若 future roadmap 引入 consent model，再重新评估 consent UI 的 server-owned 承载方式**  
   当前 first-party-only 策略下，不需要独立 consent 页面。

3. **若未来目标升级为严格 DDD，则真正补齐聚合/值对象/不变量**  
   当前 `Models/` 命名已经较贴近事实，但不应把实体模型误当成领域模型。

---

## 关键证据文件

- `src/BzsCenter.AppHost/AppHost.cs`
- `src/BzsCenter.Idp.Migrator/Program.cs`
- `src/BzsCenter.Idp.Migrator/BzsCenter.Idp.Migrator.csproj`
- `src/BzsCenter.Idp/Program.cs`
- `src/BzsCenter.Idp/Controllers/AccountController.cs`
- `src/BzsCenter.Idp/Controllers/ConnectController.cs`
- `src/BzsCenter.Idp/Controllers/OidcClientsController.cs`
- `src/BzsCenter.Idp/Services/Oidc/OidcClientService.cs`
- `src/BzsCenter.Idp/Controllers/PermissionScopesController.cs`
- `src/BzsCenter.Idp/Services/IdpServiceRegistrar.cs`
- `src/BzsCenter.Idp/Services/Oidc/PermissionClaimDestinationsHandler.cs`
- `src/BzsCenter.Idp/Services/Oidc/OidcClientDescriptorFactory.cs`
- `src/Shared/BzsCenter.Shared.Infrastructure/Database/MigrationService.cs`
- `src/Shared/BzsCenter.Shared.Infrastructure/Database/MigrateDbContextExtensions.cs`
- `src/BzsCenter.Idp/Components/Auth/Pages/Account/Login.razor`
- `src/BzsCenter.Idp/Components/Auth/Pages/Account/Denied.razor`
- `src/BzsCenter.Idp/Components/Auth/Pages/Account/Logout.razor`
- `src/BzsCenter.Idp/Components/Auth/Shared/AuthPreferences.razor`
- `tests/BzsCenter.Idp.IntegrationTests/ConnectControllerIntegrationTests.cs`
- `tests/BzsCenter.Idp.IntegrationTests/PreferencesControllerIntegrationTests.cs`
- `tests/BzsCenter.Idp.IntegrationTests/UnitTest1.cs`（文件名仍需整理，但类名已为 `PermissionScopesApiIntegrationTests`）

---

## 外部基线（用于校准本轮判断）

- Microsoft Learn — Blazor authentication and authorization  
  https://learn.microsoft.com/en-us/aspnet/core/blazor/security/?view=aspnetcore-10.0
- ASP.NET Core Blazor Web App 模板源码（官方项目结构参考）  
  https://github.com/dotnet/aspnetcore/tree/main/src/ProjectTemplates/Web.ProjectTemplates/content/BlazorWeb-CSharp
- OpenIddict Documentation — Introduction / endpoint passthrough  
  https://documentation.openiddict.com/introduction
- OpenIddict Documentation — Claim destinations  
  https://documentation.openiddict.com/configuration/claim-destinations
- EF Core — Applying migrations at runtime  
  https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/applying?tabs=dotnet-core-cli#apply-migrations-at-runtime
- .NET Aspire — EF Core migrations  
  https://learn.microsoft.com/en-us/dotnet/aspire/database/ef-core-migrations
- ASP.NET Core — API endpoint authentication behavior  
  https://learn.microsoft.com/en-us/aspnet/core/security/authentication/api-endpoint-auth

这些外部资料主要用于支持以下结论：

- 对 auth-sensitive UI，Blazor 官方更偏向保守的 server-owned 结构；
- OpenIddict 的 authorization/token/userinfo 等 endpoint passthrough 是标准模式；
- claim 是否进入 access token / identity token 应显式控制，不能靠“默认会发”。
- EF Core / Aspire 更鼓励将 migration 与 seed 从主站点启动路径中拆出，尤其是在生产或编排环境下；
- ASP.NET Core 对 cookie challenge 的 401/302 行为会受到 endpoint 类型与请求语义影响。

---

## 备注

- 本文档是**跟踪与整改文档**，不替代 ADR/详细设计文档。
- 本轮重点不是证明“当前实现完全错误”，而是把以下几件事说清楚：
  - 哪些是**已经成立的事实**；
  - 哪些是**文档初稿中的过强判断**；
  - 哪些是**真实存在、值得继续整改的风险**。
