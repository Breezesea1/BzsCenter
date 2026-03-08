using BzsCenter.Idp.Controllers;
using BzsCenter.Idp.Services.Identity;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace BzsCenter.Idp.UnitTests.Controllers;

public sealed class PermissionScopesControllerTests
{
    [Fact]
    public async Task GetByPermission_WhenPermissionEmpty_ReturnsValidationProblem()
    {
        var service = Substitute.For<IPermissionScopeService>();
        var sut = new PermissionScopesController(service);

        var result = await sut.GetByPermission(" ", CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.IsType<ValidationProblemDetails>(objectResult.Value);
    }

    [Fact]
    public async Task GetByPermission_WhenPermissionNotFound_ReturnsNotFound()
    {
        var service = Substitute.For<IPermissionScopeService>();
        service.GetByPermissionAsync("users.write", Arg.Any<CancellationToken>())
            .Returns((PermissionScopeResponse?)null);
        var sut = new PermissionScopesController(service);

        var result = await sut.GetByPermission("users.write", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Upsert_WhenScopesEmpty_ReturnsValidationProblemWithoutCallingService()
    {
        var service = Substitute.For<IPermissionScopeService>();
        var sut = new PermissionScopesController(service);

        var result = await sut.Upsert("users.write", new PermissionScopeUpsertRequest { Scopes = [] }, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.IsType<ValidationProblemDetails>(objectResult.Value);

        await service.DidNotReceive()
            .UpsertAsync(Arg.Any<string>(), Arg.Any<IEnumerable<string>>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Upsert_WhenValidRequest_ReturnsUpdatedMapping()
    {
        var service = Substitute.For<IPermissionScopeService>();
        var updated = new PermissionScopeResponse
        {
            Permission = "users.write",
            Scopes = ["api"],
        };

        service.GetByPermissionAsync("users.write", Arg.Any<CancellationToken>())
            .Returns(updated);

        var sut = new PermissionScopesController(service);
        var request = new PermissionScopeUpsertRequest { Scopes = ["api"] };

        var result = await sut.Upsert("users.write", request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var payload = Assert.IsType<PermissionScopeResponse>(ok.Value);
        Assert.Equal("users.write", payload.Permission);
        Assert.Equal(["api"], payload.Scopes);

        await service.Received(1)
            .UpsertAsync("users.write", request.Scopes, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_WhenDeleted_ReturnsNoContent()
    {
        var service = Substitute.For<IPermissionScopeService>();
        service.DeleteAsync("users.write", Arg.Any<CancellationToken>())
            .Returns(true);

        var sut = new PermissionScopesController(service);
        var result = await sut.Delete("users.write", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }
}
