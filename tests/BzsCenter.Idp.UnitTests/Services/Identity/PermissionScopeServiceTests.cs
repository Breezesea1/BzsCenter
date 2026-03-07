using BzsCenter.Idp.Infra;
using BzsCenter.Idp.Services.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BzsCenter.Idp.UnitTests.Services.Identity;

public sealed class PermissionScopeServiceTests
{
    [Fact]
    public async Task InitializeDefaultsIfEmptyAsync_SeedsOnceAndDoesNotOverwriteExisting()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var service = new PermissionScopeService(harness.DbContext, new MemoryCache(new MemoryCacheOptions()));

        await service.InitializeDefaultsIfEmptyAsync(new Dictionary<string, string[]>
        {
            ["users.write"] = ["api"],
        });

        await service.UpsertAsync("users.write", ["internal"]);

        await service.InitializeDefaultsIfEmptyAsync(new Dictionary<string, string[]>
        {
            ["users.write"] = ["api"],
        });

        var mapping = await service.GetByPermissionAsync("users.write");

        Assert.NotNull(mapping);
        Assert.Equal("users.write", mapping.Permission);
        Assert.Equal(["internal"], mapping.Scopes);
    }

    [Fact]
    public async Task UpsertAsync_NormalizesAndResolvesScopes()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var service = new PermissionScopeService(harness.DbContext, new MemoryCache(new MemoryCacheOptions()));

        await service.UpsertAsync("Users.Write", ["API", "internal", "api"]);

        var resolved = await service.ResolveScopesAsync(["users.write"]);

        Assert.True(resolved.TryGetValue("users.write", out var scopes));
        Assert.NotNull(scopes);
        Assert.Equal(["api", "internal"], scopes.OrderBy(static x => x, StringComparer.OrdinalIgnoreCase).ToArray());
    }


    [Fact]
    public async Task GetByPermissionAsync_ReturnedScopesMutation_DoesNotPolluteCache()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var service = new PermissionScopeService(harness.DbContext, new MemoryCache(new MemoryCacheOptions()));

        await service.UpsertAsync("users.write", ["api", "internal"]);

        var firstRead = await service.GetByPermissionAsync("users.write");
        Assert.NotNull(firstRead);
        firstRead!.Scopes[0] = "tampered";

        var secondRead = await service.GetByPermissionAsync("users.write");
        Assert.NotNull(secondRead);
        Assert.DoesNotContain("tampered", secondRead!.Scopes, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task UpsertAsync_InvalidatesCache_ForSubsequentReads()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var service = new PermissionScopeService(harness.DbContext, new MemoryCache(new MemoryCacheOptions()));

        await service.UpsertAsync("users.write", ["api"]);
        _ = await service.GetByPermissionAsync("users.write");

        await service.UpsertAsync("users.write", ["internal"]);

        var read = await service.GetByPermissionAsync("users.write");

        Assert.NotNull(read);
        Assert.Equal(["internal"], read!.Scopes);
    }

    [Fact]
    public async Task DeleteAsync_RemovesPermissionMapping()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var service = new PermissionScopeService(harness.DbContext, new MemoryCache(new MemoryCacheOptions()));

        await service.UpsertAsync("users.read.self", ["api"]);

        var deleted = await service.DeleteAsync("users.read.self");
        var mapping = await service.GetByPermissionAsync("users.read.self");

        Assert.True(deleted);
        Assert.Null(mapping);
    }

    private sealed class SqliteHarness : IAsyncDisposable
    {
        private SqliteHarness(SqliteConnection connection, IdpDbContext dbContext)
        {
            Connection = connection;
            DbContext = dbContext;
        }

        public SqliteConnection Connection { get; }
        public IdpDbContext DbContext { get; }

        public static async Task<SqliteHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var options = new DbContextOptionsBuilder<IdpDbContext>()
                .UseSqlite(connection)
                .Options;

            var dbContext = new IdpDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            return new SqliteHarness(connection, dbContext);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
