using BzsCenter.Idp.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BzsCenter.Idp.Infra;

public sealed class IdpDbContextDesignFactory : IDesignTimeDbContextFactory<IdpDbContext>
{
    public IdpDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<IdpDbContext>();

        string? connStr = null;
        string? dbType = null;

        foreach (var arg in args)
        {
            if (arg.StartsWith("ConnectionString=", StringComparison.OrdinalIgnoreCase))
                connStr = arg.Substring("ConnectionString=".Length);
            else if (arg.StartsWith("DbType=", StringComparison.OrdinalIgnoreCase))
                dbType = arg.Substring("DbType=".Length).ToLowerInvariant();
        }

        dbType ??= "pgsql";
        connStr ??= "Host=localhost;Port=5432;Database=identityDb;Username=postgres;Password=postgres";

        // ✅ 设计时直接用跟运行时一样的配置
        optionsBuilder.ConfigureIdentityDb(connStr);
        return new IdpDbContext(optionsBuilder.Options);
    }
}