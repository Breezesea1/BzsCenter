using BzsOIDC.Idp.Controllers;
using BzsOIDC.Idp.Services.Oidc;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace BzsOIDC.Idp.UnitTests.Controllers;

public sealed class OidcScopesControllerTests
{
    [Fact]
    public async Task Create_WhenServiceReturnsValidationErrors_ReturnsValidationProblem()
    {
        var service = Substitute.For<IOidcScopeService>();
        service.RegisterAsync(Arg.Any<OidcScopeUpsertRequest>(), Arg.Any<CancellationToken>())
            .Returns(new OidcScopeCommandResult<OidcScopeResponse>
            {
                Status = OidcScopeCommandStatus.ValidationFailed,
                Errors = ["Scope name is required."],
            });

        var sut = new OidcScopesController(service);

        var result = await sut.Create(new OidcScopeUpsertRequest(), CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.IsType<ValidationProblemDetails>(objectResult.Value);
    }

    [Fact]
    public async Task GetByName_WhenNotFound_ReturnsNotFound()
    {
        var service = Substitute.For<IOidcScopeService>();
        service.GetByNameAsync("api.read", Arg.Any<CancellationToken>())
            .Returns((OidcScopeResponse?)null);

        var sut = new OidcScopesController(service);

        var result = await sut.GetByName("api.read", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Delete_WhenDeleted_ReturnsNoContent()
    {
        var service = Substitute.For<IOidcScopeService>();
        service.DeleteAsync("api.read", Arg.Any<CancellationToken>())
            .Returns(true);

        var sut = new OidcScopesController(service);

        var result = await sut.Delete("api.read", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }
}
