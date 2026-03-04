using BzsCenter.Shared.Infrastructure.AspNetCore;

namespace BzsCenter.Idp;

internal static class ServiceExtensions
{
    private const string ForwardedHeadersSectionName = "ForwardedHeaders";
    private const string DataProtectionSectionName = "DataProtection";

    internal static IServiceCollection AddIdpOptions(this IServiceCollection sc, IConfiguration configuration)
    {
        sc.AddOptions<BzsForwardedHeadersOptions>().Bind(configuration.GetSection(ForwardedHeadersSectionName));
        return sc;
    }

    internal static IServiceCollection AddDataProtection(this IServiceCollection sc)
    {
        sc.AddDataProtectionKeyStorage(AppContext.,
            builder.Configuration.GetRequiredSection("DataProtection").Value!);
    }
}