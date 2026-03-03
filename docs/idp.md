# BzsCenter IDP 开发指南

## 1. 项目概述

**项目名称**: BzsCenter Identity Provider (IDP)  
**项目类型**: 身份认证提供商 / 授权服务器  
**技术栈**: ASP.NET Core 8 + Blazor Server + OpenIddict  

### 1.1 项目目标

BzsCenter IDP 是一个集中式的身份认证和授权服务，为 BzsCenter 生态系统中的所有应用程序提供单点登录 (SSO) 和OAuth 2.0 / OpenID Connect 认证服务。

### 1.2 系统架构

```
┌─────────────────────────────────────────────────────────────────┐
│                        BzsCenter IDP                            │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐ │
│  │   Blazor    │  │  OpenIddict │  │     User Management     │ │
│  │   Web UI    │  │   Server    │  │     (EF Core)          │ │
│  └─────────────┘  └─────────────┘  └─────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                              │
         ┌────────────────────┼────────────────────┐
         ▼                    ▼                    ▼
┌─────────────┐      ┌─────────────────┐    ┌─────────────┐
│  BzsCenter │      │   BzsCenter     │    │  Future     │
│    WEB     │      │    API(s)       │    │  Clients    │
└─────────────┘      └─────────────────┘    └─────────────┘
```

## 2. 技术架构

### 2.1 核心组件

| 组件 | 技术 | 说明 |
|------|------|------|
| Web Framework | ASP.NET Core 8 | 现代 .NET 运行时 |
| UI Framework | Blazor Server | 交互式 Web UI |
| 身份认证 | OpenIddict | OAuth 2.0 / OIDC 协议实现 |
| 数据存储 | EF Core | 用户、应用、Scope 数据持久化 |
| 数据库 | SQLite (开发) / PostgreSQL (生产) |  |

### 2.2 OpenIddict 核心概念

OpenIddict 是一个用于 ASP.NET Core 的 OpenID Connect 服务器框架，核心概念包括：

- **Applications**: 注册的客户端应用 (ClientId, ClientSecret, GrantTypes, Scopes)
- **Scopes**: 定义可以访问的资源范围 (openid, profile, email, custom scopes)
- **Tokens**: Access Token, Refresh Token, Identity Token
- **Authorization Codes**: 用于 Authorization Code Flow 的临时码
- **User Claims**: 用户身份信息

### 2.3 支持的 OAuth 2.0 授权模式

| 授权模式 | 适用场景 | 实现难度 |
|----------|----------|----------|
| Authorization Code | Web 应用、移动应用 | ⭐⭐⭐ |
| Client Credentials | 机器到机器通信 | ⭐⭐ |
| Refresh Token | 令牌刷新 | ⭐⭐ |

## 3. 功能清单

### 3.1 核心功能 (Must Have)

- [ ] **OpenIddict 服务器配置**
  - [ ] 安装 OpenIddict 核心包
  - [ ] 配置 OpenIddict 服务器
  - [ ] 设置默认 Scopes (openid, profile, email)

- [ ] **用户管理**
  - [ ] 用户实体模型 (User, Role)
  - [ ] EF Core 数据库上下文
  - [ ] 用户注册功能
  - [ ] 用户登录功能 (密码验证)
  - [ ] 用户登出功能

- [ ] **客户端应用管理**
  - [ ] 客户端注册页面
  - [ ] 客户端编辑页面
  - [ ] 客户端删除功能
  - [ ] ClientId / ClientSecret 生成

- [ ] **Scope 管理**
  - [ ] 默认 Scope 配置
  - [ ] 自定义 Scope 定义
  - [ ] Scope 管理页面

- [ ] **令牌管理**
  - [ ] Access Token 配置 (过期时间、签发者)
  - [ ] Refresh Token 配置
  - [ ] 令牌吊销功能

### 3.2 增强功能 (Should Have)

- [ ] **用户角色管理**
  - [ ] 角色实体定义
  - [ ] 角色分配页面
  - [ ] 基于角色的访问控制 (RBAC)

- [ ] **用户个人中心**
  - [ ] 修改密码
  - [ ] 查看个人信息
  - [ ] 查看已授权的应用

- [ ] **审计日志**
  - [ ] 登录日志
  - [ ] 令牌发放日志
  - [ ] 管理操作日志

### 3.3 安全特性 (Must Have)

- [ ] **密码安全**
  - [ ] 密码哈希存储 (bcrypt / Argon2)
  - [ ] 密码复杂度验证
  - [ ] 登录失败锁定

- [ ] **HTTPS 配置**
  - [ ] 生产环境强制 HTTPS
  - [ ] HSTS 配置

- [ ] **CORS 配置**
  - [ ] 限制允许的跨域来源

## 4. 实现步骤

### 4.1 第一阶段：基础配置

#### 步骤 1.1: 安装 NuGet 包

```bash
cd src/BzsCenter.Idp
dotnet add package OpenIddict.AspNetCore
dotnet add package OpenIddict.EntityFrameworkCore
dotnet add package OpenIddict.EntityFrameworkCore.Models
dotnet add package Microsoft.AspNetCore.Identity.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.Sqlite
dotnet add package Microsoft.EntityFrameworkCore.Tools
```

#### 步骤 1.2: 配置 Program.cs

```csharp
// Program.cs
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection"));
    options.UseOpenIddict();
});

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<ApplicationDbContext>();
    })
    .AddServer(options =>
    {
        options.SetAuthorizationEndpointUris("/connect/authorize")
               .SetTokenEndpointUris("/connect/token")
               .SetUserinfoEndpointUris("/connect/userinfo");
        
        options.RegisterScopes(OpenIddictConstants.Scopes.OpenId,
                              OpenIddictConstants.Scopes.Profile,
                              OpenIddictConstants.Scopes.Email);
        
        options.AcceptAnonymousClients();
    });
```

### 4.2 第二阶段：数据模型

#### 步骤 2.1: 创建应用DbContext

```csharp
public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        
        // OpenIddict 配置
        builder.UseOpenIddict();
    }
}
```

#### 步骤 2.2: 创建应用实体 (可选自定义)

如果需要扩展默认的 OpenIddict 实体，可以自定义 Application、Scope、Authorization、Token 实体。

### 4.3 第三阶段：UI 界面

#### 步骤 3.1: 创建管理页面

- `/Admin/Clients` - 客户端应用管理
- `/Admin/Scopes` - Scope 管理
- `/Account/Register` - 用户注册
- `/Account/Login` - 用户登录
- `/Account/Logout` - 用户登出
- `/Account/Manage` - 用户个人中心

#### 步骤 3.2: 实现授权页面

用户授权页面，用于用户确认授权给客户端应用。

### 4.4 第四阶段：API 端点

#### 步骤 4.1: 实现 Token 端点

OpenIddict 默认提供 `/connect/token` 端点，支持：
- `grant_type=authorization_code`
- `grant_type=client_credentials`
- `grant_type=refresh_token`
- `grant_type=password` (仅开发环境)

#### 步骤 4.2: 实现 UserInfo 端点

```csharp
[HttpGet("connect/userinfo")]
[Authorize(OpenIddictConstants.Scopes.OpenId)]
public async Task<IActionResult> Userinfo()
{
    var user = await _userManager.GetUserAsync(User);
    var claims = new Dictionary<string, object>();
    
    if (User.HasClaim(OpenIddictConstants.Scopes.Profile))
    {
        claims[OpenIddictConstantsClaims.Name] = user.UserName;
        claims[OpenIddictConstantsClaims.PreferredUsername] = user.UserName;
    }
    
    if (User.HasClaim(OpenIddictConstants.Scopes.Email))
    {
        claims[OpenIddictConstantsClaims.Email] = user.Email;
        claims[OpenIddictConstantsClaims.EmailVerified] = user.EmailConfirmed;
    }
    
    return Ok(claims);
}
```

## 5. 待办事项清单

### 5.1 环境配置

- [ ] 确认 .NET 8 SDK 已安装
- [ ] 配置开发数据库连接字符串
- [ ] 配置生产数据库 (PostgreSQL)

### 5.2 核心功能开发

- [ ] 添加 OpenIddict 相关 NuGet 包
- [ ] 配置 OpenIddict Server
- [ ] 实现 ApplicationDbContext
- [ ] 配置 Identity
- [ ] 实现数据库迁移
- [ ] 创建用户管理 UI
- [ ] 创建客户端管理 UI
- [ ] 创建 Scope 管理 UI

### 5.3 测试验证

- [ ] 使用 Postman 测试 OAuth 2.0 流程
- [ ] 测试 Authorization Code Flow
- [ ] 测试 Client Credentials Flow
- [ ] 测试 Refresh Token Flow
- [ ] 测试用户注册/登录流程

### 5.4 集成测试

- [ ] 与 BzsCenter Web 集成
- [ ] 与 BzsCenter API 集成
- [ ] 配置服务间通信

## 6. 配置参考

### 6.1 appsettings.json

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=bzscenter-idp.db"
  },
  "OpenIddict": {
    "AccessTokenLifetime": "00:60:00",
    "RefreshTokenLifetime": "07:00:00",
    "IdentityTokenLifetime": "00:60:00"
  }
}
```

### 6.2 安全建议

1. **生产环境必须使用 HTTPS**
2. **ClientSecret 应该安全存储** (环境变量或密钥库)
3. **Access Token 建议短期有效** (15-60 分钟)
4. **Refresh Token 建议长期有效但可吊销**
5. **实现令牌吊销机制**

## 7. 参考资源

### 7.1 官方文档

- [OpenIddict Official Documentation](https://documentation.openiddict.com/)
- [OpenIddict GitHub](https://github.com/openiddict/openiddict-core)
- [ASP.NET Core Identity](https://learn.microsoft.com/aspnet/core/security/authentication/identity)

### 7.2 学习资源

- [OAuth 2.0 协议详解](https://oauth.net/2/)
- [OpenID Connect 协议详解](https://openid.net/connect/)
- [OpenIddict Samples](https://github.com/openiddict/openiddict-samples)

### 7.3 推荐的客户端库

| 客户端类型 | 推荐库 |
|-----------|--------|
| .NET | IdentityModel |
| JavaScript/TypeScript | oidc-client-ts |
| Blazor WebAssembly | Microsoft.AspNetCore.Authentication.JwtBearer |

## 8. 常见问题

### Q1: 如何添加自定义 Scope?

```csharp
builder.Services.AddOpenIddict()
    .AddServer(options =>
    {
        // 注册自定义 Scope
        options.RegisterScopes(
            "api",
            "custom_scope"
        );
    });
```

### Q2: 如何限制客户端的授权类型?

```csharp
// 在创建客户端时设置
options.GrantTypes.Add(GrantTypes.AuthorizationCode);
options.GrantTypes.Add(GrantTypes.ClientCredentials);
```

### Q3: 如何实现令牌吊销?

```csharp
await _openIddictTokenManager.RevokeAsync(token);
```

### Q4: 如何自定义用户 Claims?

```csharp
// 在 token 发放时添加自定义 claims
options.SetAccessTokenClaims(subject => 
{
    subject["custom_claim"] = "value";
});
```

## 9. 后续规划

- [ ] 实现多租户支持
- [ ] 添加外部登录 (Google, GitHub, etc.)
- [ ] 实现 MFA (多因素认证)
- [ ] 添加 API 文档
- [ ] 性能优化和压力测试

---

**文档版本**: 1.0  
**最后更新**: 2025-03-03  
**维护者**: BzsCenter Team
