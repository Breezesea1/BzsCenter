using BzsOIDC.Idp.Infra;
using BzsOIDC.Idp.Services.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BzsOIDC.Idp.UnitTests.Services.Identity;

public sealed class PermissionCatalogServiceTests
{
    [Fact]
    public async Task UpsertResourceAsync_WhenValid_PersistsResource()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var sut = harness.CreateService();

        var result = await sut.UpsertResourceAsync("Orders-API", new ProtectedResourceUpsertRequest
        {
            DisplayName = "Orders API",
            Description = "Order service",
        });

        Assert.Equal(PermissionCatalogCommandStatus.Success, result.Status);
        Assert.Equal("orders-api", result.Value!.Key);
        Assert.Equal("Orders API", result.Value.DisplayName);
    }

    [Fact]
    public async Task UpsertPermissionAsync_WhenResourceMissing_ReturnsNotFound()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var sut = harness.CreateService();

        var result = await sut.UpsertPermissionAsync("orders-api", "orders.read", new PermissionDefinitionUpsertRequest());

        Assert.Equal(PermissionCatalogCommandStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task SyncReleaseScopesAsync_WhenEmpty_ReturnsValidationFailed()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var sut = harness.CreateService();
        await sut.UpsertResourceAsync("orders-api", new ProtectedResourceUpsertRequest());
        await sut.UpsertPermissionAsync("orders-api", "orders.read", new PermissionDefinitionUpsertRequest());

        var result = await sut.SyncReleaseScopesAsync("orders.read", []);

        Assert.Equal(PermissionCatalogCommandStatus.ValidationFailed, result.Status);
    }

    [Fact]
    public async Task ResolveReleaseScopesAsync_WhenPermissionInactive_ExcludesPermission()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var sut = harness.CreateService();
        await sut.UpsertResourceAsync("orders-api", new ProtectedResourceUpsertRequest());
        await sut.UpsertPermissionAsync("orders-api", "orders.read", new PermissionDefinitionUpsertRequest
        {
            IsActive = false,
        });
        await sut.SyncReleaseScopesAsync("orders.read", ["orders-api"]);

        var resolved = await sut.ResolveReleaseScopesAsync(["orders.read"]);

        Assert.Empty(resolved);
    }

    [Fact]
    public async Task ValidateAssignablePermissionsAsync_WhenUnknown_ReturnsValidationError()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var sut = harness.CreateService();

        var invalid = await sut.ValidateAssignablePermissionsAsync(["missing.permission"]);

        Assert.Equal(["missing.permission"], invalid);
    }

    [Fact]
    public async Task InitializeDefaultsAsync_WhenSeeded_PersistsReleaseScopes()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var sut = harness.CreateService();

        await sut.InitializeDefaultsAsync([new PermissionCatalogSeedResource
        {
            ResourceKey = "orders-api",
            DisplayName = "Orders API",
            Permissions =
            [
                new PermissionCatalogSeedPermission
                {
                    Name = "orders.read",
                    ReleaseScopes = ["orders-api"],
                },
            ],
        }]);

        var resource = await sut.GetResourceAsync("orders-api");

        Assert.NotNull(resource);
        Assert.Equal(["orders-api"], resource!.Permissions[0].ReleaseScopes);
    }

    [Fact]
    public async Task InitializeDefaultsAsync_WhenCalledTwice_DoesNotDuplicateData()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var sut = harness.CreateService();
        var seed = new[]
        {
            new PermissionCatalogSeedResource
            {
                ResourceKey = "orders-api",
                DisplayName = "Orders API",
                Permissions =
                [
                    new PermissionCatalogSeedPermission
                    {
                        Name = "orders.read",
                        ReleaseScopes = ["orders-api"],
                    },
                ],
            },
        };

        await sut.InitializeDefaultsAsync(seed);
        await sut.InitializeDefaultsAsync(seed);

        var resources = await sut.GetResourcesAsync();

        Assert.Single(resources);
        Assert.Single(resources[0].Permissions);
        Assert.Equal(["orders-api"], resources[0].Permissions[0].ReleaseScopes);
    }

    [Fact]
    public async Task InitializeDefaultsAsync_WhenObsoleteSeedResourcesExist_RemovesThem()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var sut = harness.CreateService();

        await sut.InitializeDefaultsAsync(
        [
            new PermissionCatalogSeedResource
            {
                ResourceKey = "legacy-api",
                DisplayName = "Legacy API",
                Permissions =
                [
                    new PermissionCatalogSeedPermission
                    {
                        Name = "legacy.read",
                        ReleaseScopes = ["api"],
                    },
                ],
            },
            new PermissionCatalogSeedResource
            {
                ResourceKey = "oidc-clients",
                DisplayName = "OpenIddict Clients",
                Permissions =
                [
                    new PermissionCatalogSeedPermission
                    {
                        Name = "clients.read",
                        ReleaseScopes = ["api"],
                    },
                ],
            },
        ]);

        await sut.InitializeDefaultsAsync(
        [
            new PermissionCatalogSeedResource
            {
                ResourceKey = "api",
                DisplayName = "BzsOIDC Admin API",
                Permissions =
                [
                    new PermissionCatalogSeedPermission
                    {
                        Name = "clients.read",
                        ReleaseScopes = ["api"],
                    },
                ],
            },
        ]);

        var resources = await sut.GetResourcesAsync();

        Assert.Single(resources);
        Assert.Equal("api", resources[0].Key);
        Assert.Equal(["clients.read"], resources[0].Permissions.Select(static permission => permission.Name).ToArray());
    }

    [Fact]
    public async Task InitializeDefaultsAsync_WhenSeedContainsDuplicatePermissionName_KeepsSingleOwner()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var sut = harness.CreateService();

        await sut.InitializeDefaultsAsync(
        [
            new PermissionCatalogSeedResource
            {
                ResourceKey = "api",
                Permissions =
                [
                    new PermissionCatalogSeedPermission
                    {
                        Name = "orders.read",
                        ReleaseScopes = ["api"],
                    },
                ],
            },
            new PermissionCatalogSeedResource
            {
                ResourceKey = "orders-api",
                Permissions =
                [
                    new PermissionCatalogSeedPermission
                    {
                        Name = "orders.read",
                        DisplayName = "Read orders",
                        ReleaseScopes = ["orders-api"],
                    },
                ],
            },
        ]);

        var resources = await sut.GetResourcesAsync();
        var permissions = resources.SelectMany(static resource => resource.Permissions).ToArray();

        Assert.Single(permissions);
        Assert.Equal("orders.read", permissions[0].Name);
        Assert.Equal("api", permissions[0].ResourceKey);
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

        public PermissionCatalogService CreateService()
        {
            return new PermissionCatalogService(
                DbContext,
                null!,
                new MemoryCache(new MemoryCacheOptions()));
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}

