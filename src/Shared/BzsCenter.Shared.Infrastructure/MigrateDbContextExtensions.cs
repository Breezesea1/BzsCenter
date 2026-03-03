using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BzsCenter.Shared.Infrastructure;

public static class MigrateDbContextExtensions
{
    private static readonly string _activitySourceName = "DbMigrations";
    private static readonly ActivitySource _activitySource = new(_activitySourceName);


    extension(IServiceCollection sc)
    {
        /// <summary>
        /// Adds migration services to the service collection.
        /// </summary>
        public IServiceCollection AddMigration<TContext>(Func<TContext, IServiceProvider, Task>? seeder = null)
            where TContext : DbContext
        {
            // Enable migration tracing
            sc.AddOpenTelemetry().WithTracing(tracing => tracing.AddSource(_activitySourceName));
            return sc.AddScoped<IMigrated>(sp =>
                seeder is null
                    ? new MigrationService<TContext>(sp)
                    : new MigrationService<TContext>(sp, seeder));
        }

        /// <summary>
        /// Adds migration services to the service collection.
        /// </summary>
        public IServiceCollection AddMigration<TContext>(string key,
            Func<TContext, IServiceProvider, Task>? seeder = null)
            where TContext : DbContext
        {
            // Enable migration tracing
            sc.AddOpenTelemetry().WithTracing(tracing => tracing.AddSource(_activitySourceName));
            return sc.AddKeyedScoped<IMigrated>(key, (sp, _) =>
                seeder is null
                    ? new MigrationService<TContext>(sp)
                    : new MigrationService<TContext>(sp, seeder));
        }
    }


    /// <summary>
    ///   Migrates the database.
    /// </summary>
    /// <param name="sp">IServiceProvider</param>
    /// <param name="seeder">Seed function</param>
    /// <param name="cancellationToken">cancellationToken</param>
    /// <typeparam name="TContext">Type of db context</typeparam>
    internal static async Task MigrateDbContextAsync<TContext>(this IServiceProvider sp,
        Func<TContext, IServiceProvider, Task>? seeder, CancellationToken cancellationToken) where TContext : DbContext
    {
        using var scope = sp.CreateScope();
        var scopeServices = scope.ServiceProvider;
        var logger = scopeServices.GetRequiredService<ILogger<TContext>>();
        var context = scopeServices.GetRequiredService<TContext>();


        using var activity = _activitySource.StartActivity($"Migration operation {typeof(TContext).Name}");

        var retryCount = 10;
        var currentRetry = 0;
        // ReSharper disable once ConditionIsAlwaysTrueOrFalse
        while (currentRetry < retryCount)
        {
            try
            {
                var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
                if (pending.Length == 0)
                {
                    logger.LogInformation("No pending migrations found on context {DbContextName}",
                        typeof(TContext).Name);
                    return;
                }

                logger.LogInformation("Applying {Count} migrations for {DbContextName}: {Migrations}",
                    pending.Length, typeof(TContext).Name, string.Join(", ", pending));

                var strategy = context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    await context.Database.MigrateAsync(cancellationToken);
                    if (seeder is not null)
                        await seeder(context, sp);
                });

                return;
            }
            catch (Exception ex)
            {
                currentRetry++;
                activity?.SetExceptionTags(ex);

                if (currentRetry >= retryCount)
                {
                    logger.LogError(ex,
                        "An error occurred while migrating the database used on context {DbContextName}",
                        typeof(TContext).Name);
                    throw;
                }

                logger.LogError(ex,
                    "An error occurred while migrating the database used on context {DbContextName}, current retry count: {RetryCount}",
                    typeof(TContext).Name, currentRetry);
                await Task.Delay(TimeSpan.FromSeconds(2 + currentRetry * 2), cancellationToken);
            }
        }
    }
}

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