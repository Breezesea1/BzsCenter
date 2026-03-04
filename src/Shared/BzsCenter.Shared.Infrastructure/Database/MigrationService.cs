using Microsoft.EntityFrameworkCore;

namespace BzsCenter.Shared.Infrastructure.Database;

/// <summary>
///   Hosted service for migrating the database. 
/// </summary>
internal class MigrationService<TContext> : IMigrated where TContext : DbContext
{
    private readonly IServiceProvider _sp;
    private readonly Func<TContext, IServiceProvider, Task>? _seeder;

    public MigrationService(IServiceProvider sp)
    {
        _sp = sp;
    }

    public MigrationService(IServiceProvider sp, Func<TContext, IServiceProvider, Task> seeder) : this(sp)
    {
        _seeder = seeder;
    }

    public Task MigrateAsync(CancellationToken cancellationToken)
    {
        return _sp.MigrateDbContextAsync(_seeder, cancellationToken);
    }
}