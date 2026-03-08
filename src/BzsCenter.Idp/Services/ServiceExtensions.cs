using BzsCenter.Idp.Infra;
using BzsCenter.Idp.Infra.Oidc;
using BzsCenter.Idp.Services.Authorization;
using BzsCenter.Idp.Services.Identity;
using BzsCenter.Shared.Infrastructure.Cache;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace BzsCenter.Idp.Services;

internal static class ServiceExtensions
{
    /// <summary>
    /// 执行EnrichFromAspire。
    /// </summary>
    /// <param name="builder">参数builder。</param>
    internal static void EnrichFromAspire(this WebApplicationBuilder builder)
    {
        builder.EnrichNpgsqlDbContext<IdpDbContext>();
    }


    /// <summary>
    /// 向服务集合注册 Identity Provider (IDP) 服务。
    /// </summary>
    /// <param name="sc">服务集合。</param>
    /// <param name="configuration">应用程序配置。</param>
    /// <returns>服务集合，用于链式调用。</returns>
    internal static IServiceCollection AddIdpService(this IServiceCollection sc, IConfiguration configuration)
    {
        sc.AddForwardedHeaders();
        sc.AddMemoryCache();
        sc.AddBzsCache(configuration);

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        sc.AddInfraServices(connectionString);

        var registrar = new IdpServiceRegistrar(sc, configuration);
        registrar.AddIdpOptions();
        registrar.AddDataProtection();
        registrar.AddOidc();

        sc.AddScoped<IRoleService, RoleService>();
        sc.AddScoped<IUserService, UserService>();
        sc.AddScoped<IRolePermissionService, RolePermissionService>();
        sc.AddScoped<IPermissionScopeService, PermissionScopeService>();
        sc.AddScoped<IdentitySeeder>();

        sc.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();
        sc.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();

        return sc;
    }
}
