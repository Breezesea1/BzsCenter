using BzsOIDC.Shared.Infrastructure.Database;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BzsOIDC.Idp.UnitTests.Shared.Infrastructure.Database;

public sealed class MigrateDbContextExtensionsTests
{
    [Fact]
    public async Task MigrateAsync_WhenNoPendingMigrations_StillRunsSeeder()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TestDbContext>(options => options.UseSqlite(connection));

        var seederInvoked = false;
        services.AddMigration<TestDbContext>((_, _) =>
        {
            seederInvoked = true;
            return Task.CompletedTask;
        });

        await using var serviceProvider = services.BuildServiceProvider();

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<TestDbContext>();
            await dbContext.Database.EnsureCreatedAsync();
        }

        await using (var scope = serviceProvider.CreateAsyncScope())
        {
            var migrator = scope.ServiceProvider.GetRequiredService<IMigrated>();
            await migrator.MigrateAsync(CancellationToken.None);
        }

        Assert.True(seederInvoked);
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
    }
}
