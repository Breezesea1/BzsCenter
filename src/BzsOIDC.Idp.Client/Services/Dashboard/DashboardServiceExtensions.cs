using Microsoft.Extensions.DependencyInjection;

namespace BzsOIDC.Idp.Client.Services.Dashboard;

public static class DashboardServiceExtensions
{
    public static IServiceCollection AddAdminDashboardClient(this IServiceCollection services, Func<IServiceProvider, Uri> baseAddressFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseAddressFactory);

        services.AddScoped<IAdminDashboardClient>(serviceProvider =>
        {
            var httpClient = new HttpClient
            {
                BaseAddress = baseAddressFactory(serviceProvider)
            };

            return new AdminDashboardClient(httpClient);
        });

        return services;
    }
}
