using BzsOIDC.Idp.Controllers;
using BzsOIDC.Idp.Services.Identity;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace BzsOIDC.Idp.UnitTests.Controllers;

public sealed class PermissionCatalogControllerTests
{
    [Fact]
    public async Task GetResource_WhenResourceKeyEmpty_ReturnsValidationProblem()
    {
        var service = Substitute.For<IPermissionCatalogService>();
        var sut = new PermissionCatalogController(service, new TestServiceProvider());

        var result = await sut.GetResource(" ", CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.IsType<ValidationProblemDetails>(objectResult.Value);
    }

    [Fact]
    public async Task GetResource_WhenResourceNotFound_ReturnsNotFound()
    {
        var service = Substitute.For<IPermissionCatalogService>();
        service.GetResourceAsync("orders-api", Arg.Any<CancellationToken>())
            .Returns((ProtectedResourceResponse?)null);
        var sut = new PermissionCatalogController(service, new TestServiceProvider());

        var result = await sut.GetResource("orders-api", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task SyncReleaseScopes_WhenScopesEmpty_ReturnsValidationProblem()
    {
        var service = Substitute.For<IPermissionCatalogService>();
        service.SyncReleaseScopesAsync("orders.read", Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>())
            .Returns(PermissionCatalogCommandResult<PermissionDefinitionResponse>.Failure(
                PermissionCatalogCommandStatus.ValidationFailed,
                "At least one release scope is required."));
        var sut = new PermissionCatalogController(service, new TestServiceProvider());

        var result = await sut.SyncReleaseScopes(
            "orders.read",
            new PermissionReleaseScopesUpsertRequest { Scopes = [] },
            CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result.Result);
        Assert.IsType<ValidationProblemDetails>(objectResult.Value);
    }

    [Fact]
    public async Task UpsertPermission_WhenValidRequest_ReturnsUpdatedPermission()
    {
        var service = Substitute.For<IPermissionCatalogService>();
        var updated = new PermissionDefinitionResponse
        {
            ResourceKey = "orders-api",
            Name = "orders.read",
            ReleaseScopes = ["orders-api"],
        };

        service.UpsertPermissionAsync("orders-api", "orders.read", Arg.Any<PermissionDefinitionUpsertRequest>(), Arg.Any<CancellationToken>())
            .Returns(PermissionCatalogCommandResult<PermissionDefinitionResponse>.Success(updated));

        var sut = new PermissionCatalogController(service, new TestServiceProvider());
        var request = new PermissionDefinitionUpsertRequest { DisplayName = "Read orders" };

        var result = await sut.UpsertPermission("orders-api", "orders.read", request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<PermissionDefinitionResponse>(ok.Value);
        Assert.Equal("orders.read", payload.Name);
    }

    [Fact]
    public async Task SyncRolePermissions_WhenServiceFails_ReturnsValidationProblem()
    {
        var service = Substitute.For<IPermissionCatalogService>();
        var sut = new PermissionCatalogController(service, new TestServiceProvider());

        var result = await sut.SyncRolePermissions(Guid.NewGuid(), new RolePermissionSyncRequest
        {
            Permissions = ["missing.permission"],
        }, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Role permission service is unavailable.", problem.Detail);
    }

    private sealed class TestServiceProvider : IServiceProvider
    {
        public object? GetService(Type serviceType)
        {
            return null;
        }
    }
}
