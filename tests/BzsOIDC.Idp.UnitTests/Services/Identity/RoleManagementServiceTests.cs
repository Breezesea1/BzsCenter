using BzsOIDC.Idp.Infra;
using BzsOIDC.Idp.Models;
using BzsOIDC.Idp.Services.Identity;
using BzsOIDC.Shared.Infrastructure.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;

namespace BzsOIDC.Idp.UnitTests.Services.Identity;

public sealed class RoleManagementServiceTests
{
    [Fact]
    public async Task GetAllAsync_WhenRolesExist_ReturnsSortedRoleSummaries()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var sut = harness.CreateService();
        await harness.CreateRoleAsync("operators");
        await harness.CreateRoleAsync("admin");

        var roles = await sut.GetAllAsync();

        Assert.Equal(["admin", "operators"], roles.Select(static role => role.Name).ToArray());
        Assert.All(roles, role => Assert.Empty(role.Permissions));
    }

    [Fact]
    public async Task CreateAsync_WhenRoleNameValid_CreatesRoleAndReturnsDetails()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var sut = harness.CreateService();

        var result = await sut.CreateAsync(new RoleUpsertRequest { Name = "operators" });

        Assert.Equal(RoleManagementCommandStatus.Success, result.Status);
        Assert.Equal("operators", result.Value!.Name);
        Assert.False(result.Value.IsProtected);
    }

    [Fact]
    public async Task CreateAsync_WhenAdminAlreadyExists_ReturnsValidationFailure()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var sut = harness.CreateService();
        await harness.CreateRoleAsync(IdentitySeedConstants.AdminRoleName);

        var result = await sut.CreateAsync(new RoleUpsertRequest { Name = IdentitySeedConstants.AdminRoleName });

        Assert.Equal(RoleManagementCommandStatus.ValidationFailed, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_WhenRoleMissing_ReturnsNotFound()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var sut = harness.CreateService();

        var result = await sut.UpdateAsync(Guid.NewGuid(), new RoleUpsertRequest { Name = "operators" });

        Assert.Equal(RoleManagementCommandStatus.NotFound, result.Status);
    }

    [Fact]
    public async Task UpdateAsync_WhenProtectedAdminRenamedAway_ReturnsProtected()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var sut = harness.CreateService();
        var role = await harness.CreateRoleAsync(IdentitySeedConstants.AdminRoleName);

        var result = await sut.UpdateAsync(role.Id, new RoleUpsertRequest { Name = "operators" });

        Assert.Equal(RoleManagementCommandStatus.Protected, result.Status);
    }

    [Fact]
    public async Task DeleteAsync_WhenNonAdminRoleExists_DeletesRole()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var sut = harness.CreateService();
        var role = await harness.CreateRoleAsync("operators");

        var result = await sut.DeleteAsync(role.Id);

        Assert.Equal(RoleManagementCommandStatus.Success, result.Status);
        Assert.Null(await harness.RoleManager.FindByIdAsync(role.Id.ToString()));
    }

    [Fact]
    public async Task DeleteAsync_WhenAdminRoleProtected_ReturnsProtected()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var sut = harness.CreateService();
        var role = await harness.CreateRoleAsync(IdentitySeedConstants.AdminRoleName);

        var result = await sut.DeleteAsync(role.Id);

        Assert.Equal(RoleManagementCommandStatus.Protected, result.Status);
    }

    [Fact]
    public async Task SyncPermissionsAsync_WhenTargetsValid_AddsAndRemovesClaims()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var sut = harness.CreateService();
        var role = await harness.CreateRoleAsync("operators");
        await harness.PermissionCatalogService.UpsertResourceAsync("api", new ProtectedResourceUpsertRequest
        {
            DisplayName = "API",
        });
        await harness.PermissionCatalogService.UpsertPermissionAsync("api", "roles.read", new PermissionDefinitionUpsertRequest
        {
            DisplayName = "Read roles",
            IsActive = true,
        });
        await harness.PermissionCatalogService.UpsertPermissionAsync("api", "roles.write", new PermissionDefinitionUpsertRequest
        {
            DisplayName = "Write roles",
            IsActive = true,
        });

        var seed = await sut.SyncPermissionsAsync(role.Id, ["roles.read", "roles.write"]);

        Assert.Equal(RoleManagementCommandStatus.Success, seed.Status);
        Assert.Equal(["roles.read", "roles.write"], seed.Value);

        var shrink = await sut.SyncPermissionsAsync(role.Id, ["roles.read"]);

        Assert.Equal(RoleManagementCommandStatus.Success, shrink.Status);
        Assert.Equal(["roles.read"], shrink.Value);
    }

    [Fact]
    public async Task SyncPermissionsAsync_WhenPermissionInvalid_ReturnsValidationFailureWithoutMutatingClaims()
    {
        await using var harness = await SqliteHarness.CreateAsync();
        var sut = harness.CreateService();
        var role = await harness.CreateRoleAsync("operators");

        var result = await sut.SyncPermissionsAsync(role.Id, ["missing.permission"]);

        Assert.Equal(RoleManagementCommandStatus.ValidationFailed, result.Status);
        Assert.Empty(await harness.RoleManager.GetClaimsAsync(role));
    }

    private sealed class SqliteHarness : IAsyncDisposable
    {
        private SqliteHarness(
            ServiceProvider provider,
            IServiceScope scope,
            SqliteConnection connection,
            IdpDbContext dbContext,
            RoleManager<BzsRole> roleManager,
            PermissionCatalogService permissionCatalogService,
            RoleManagementPolicy policy)
        {
            Provider = provider;
            Scope = scope;
            Connection = connection;
            DbContext = dbContext;
            RoleManager = roleManager;
            PermissionCatalogService = permissionCatalogService;
            Policy = policy;
        }

        public ServiceProvider Provider { get; }
        public IServiceScope Scope { get; }
        public SqliteConnection Connection { get; }
        public IdpDbContext DbContext { get; }
        public RoleManager<BzsRole> RoleManager { get; }
        public PermissionCatalogService PermissionCatalogService { get; }
        public RoleManagementPolicy Policy { get; }

        public static async Task<SqliteHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddMemoryCache();
            services.AddDbContext<IdpDbContext>(options => options.UseSqlite(connection));
            services.AddIdentityCore<BzsUser>(options => { })
                .AddRoles<BzsRole>()
                .AddEntityFrameworkStores<IdpDbContext>();
            services.AddScoped<RoleManagementPolicy>();
            services.AddScoped<PermissionCatalogService>();

            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IdpDbContext>();
            await dbContext.Database.EnsureCreatedAsync();

            return new SqliteHarness(
                provider,
                scope,
                connection,
                dbContext,
                scope.ServiceProvider.GetRequiredService<RoleManager<BzsRole>>(),
                scope.ServiceProvider.GetRequiredService<PermissionCatalogService>(),
                scope.ServiceProvider.GetRequiredService<RoleManagementPolicy>());
        }

        public RoleManagementService CreateService()
        {
            return new RoleManagementService(RoleManager, PermissionCatalogService, Policy);
        }

        public async Task<BzsRole> CreateRoleAsync(string name)
        {
            var role = new BzsRole { Name = name };
            var result = await RoleManager.CreateAsync(role);
            Assert.True(result.Succeeded, string.Join("; ", result.Errors.Select(static error => error.Description)));
            return role;
        }

        public async ValueTask DisposeAsync()
        {
            Scope.Dispose();
            await Provider.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }
}
