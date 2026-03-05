using BzsCenter.Shared.Infrastructure.AspNetCore;
using Microsoft.EntityFrameworkCore;

namespace BzsCenter.Idp.Services;

internal static class ServiceExtensions
{
    private const string ForwardedHeadersSectionName = "ForwardedHeaders";
    private const string DataProtectionSectionName = "DataProtection";
    private const string OidcSectionName = "Oidc";



    internal static IServiceCollection AddIdpService(this IServiceCollection sc, IConfiguration configuration)
    {
        _ = new IdpInternalService(sc, configuration);

        return sc;
    }

    private class IdpInternalService(IServiceCollection sc, IConfiguration cfg)
    {
        internal IServiceCollection AddIdpOptions()
        {
            sc.AddOptions<BzsForwardedHeadersOptions>().Bind(cfg.GetSection(ForwardedHeadersSectionName));
            sc.AddOptions<DataProtectionOptions>().Bind(cfg.GetSection(DataProtectionSectionName));
            sc.AddOptions<OidcOptions>().Bind(cfg.GetSection(OidcSectionName));
            return sc;
        }

        internal IServiceCollection AddDataProtection()
        {
#if !DEBUG
            var options = cfg.GetSection(DataProtectionSectionName).Get<DataProtectionOptions>();
            if (options is null)
            {
                throw new InvalidOperationException("DataProtection options are not configured.");
            }
#else
            var options = new DataProtectionOptions()
            {
                ApplicationName = "BzsCenter.Idp.Test",
                KeyLifetimeDays = 365,
                StorageDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DevDataProtectionKeys"),
            };
#endif

            sc.AddDataProtectionKeyStorage(options);
            return sc;
        }
    }
}