using Microsoft.EntityFrameworkCore;

namespace BzsCenter.Idp.Infra;

internal static class InfraServiceExtensions
{
    internal static void ConfigureIdentityDb(this DbContextOptionsBuilder opt,
        string connectionString)
    {
        opt.UseNpgsql(connectionString, sql => { sql.EnableRetryOnFailure(); });
        opt.UseOpenIddict();
    }
}