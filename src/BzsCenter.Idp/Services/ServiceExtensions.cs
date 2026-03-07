using BzsCenter.Idp.Infra;
using BzsCenter.Idp.Infra.Oidc;
using Microsoft.EntityFrameworkCore;

namespace BzsCenter.Idp.Services;

internal static class ServiceExtensions
{
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

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        ArgumentException.ThrowIfNullOrEmpty(connectionString);

        sc.AddInfraServices(connectionString);

        var registrar = new IdpServiceRegistrar(sc, configuration);
        registrar.AddIdpOptions();
        registrar.AddDataProtection();
        registrar.AddOidc();

        return sc;
    }
}
