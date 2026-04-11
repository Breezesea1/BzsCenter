using BzsOIDC.Shared.Infrastructure.Database;
using Microsoft.EntityFrameworkCore;

namespace BzsOIDC.Idp.Infra;

internal static class InfraServiceExtensions
{
    internal static void ConfigureIdentityDb(this DbContextOptionsBuilder opt,
        string connectionString)
    {
        if (connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase))
        {
            opt.UseSqlite(connectionString);
        }
        else
        {
            opt.UseNpgsql(connectionString, sql => { sql.EnableRetryOnFailure(); });
        }

        opt.UseOpenIddict();
    }



    internal static IServiceCollection AddInfraServices(this IServiceCollection sc, string connectionString)
    {
        ArgumentNullException.ThrowIfNull(connectionString, nameof(connectionString));

        // 数据库
        sc.AddDbContext<IdpDbContext>(opt => opt.ConfigureIdentityDb(connectionString!),
            contextLifetime: ServiceLifetime.Scoped,
            optionsLifetime: ServiceLifetime.Singleton);
        sc.AddDbContextFactory<IdpDbContext>(opt => opt.ConfigureIdentityDb(connectionString));

        return sc;
    }
}
