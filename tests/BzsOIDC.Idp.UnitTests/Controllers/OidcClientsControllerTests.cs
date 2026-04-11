using BzsOIDC.Idp.Controllers;
using BzsOIDC.Idp.Services.Oidc;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;

namespace BzsOIDC.Idp.UnitTests.Controllers;

public sealed class OidcClientsControllerTests
{
    [Fact]
    public async Task Register_WhenServiceReturnsValidationErrors_ReturnsValidationProblem()
    {
        var service = Substitute.For<IOidcClientService>();
        service.RegisterAsync(Arg.Any<OidcClientUpsertRequest>(), Arg.Any<CancellationToken>())
            .Returns(new OidcClientCommandResult<OidcClientRegistrationResponse>
            {
                Status = OidcClientCommandStatus.ValidationFailed,
                Errors = ["DisplayName is required."],
            });

        var sut = new OidcClientsController(service);

        var result = await sut.Register(new OidcClientUpsertRequest(), CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(result.Result);
        Assert.IsType<ValidationProblemDetails>(objectResult.Value);
    }

    [Fact]
    public async Task Register_WhenClientExists_ReturnsConflict()
    {
        var service = Substitute.For<IOidcClientService>();
        service.RegisterAsync(Arg.Any<OidcClientUpsertRequest>(), Arg.Any<CancellationToken>())
            .Returns(new OidcClientCommandResult<OidcClientRegistrationResponse>
            {
                Status = OidcClientCommandStatus.Conflict,
                Errors = ["Client 'interactive-client' already exists."],
            });

        var sut = new OidcClientsController(service);

        var result = await sut.Register(new OidcClientUpsertRequest(), CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
        Assert.Equal("Client 'interactive-client' already exists.", conflict.Value);
    }

    [Fact]
    public async Task GetByClientId_WhenNotFound_ReturnsNotFound()
    {
        var service = Substitute.For<IOidcClientService>();
        service.GetByClientIdAsync("missing-client", Arg.Any<CancellationToken>())
            .Returns((OidcClientResponse?)null);

        var sut = new OidcClientsController(service);

        var result = await sut.GetByClientId("missing-client", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Update_WhenNotFound_ReturnsNotFound()
    {
        var service = Substitute.For<IOidcClientService>();
        service.UpdateAsync("missing-client", Arg.Any<OidcClientUpsertRequest>(), Arg.Any<CancellationToken>())
            .Returns(new OidcClientCommandResult<OidcClientResponse>
            {
                Status = OidcClientCommandStatus.NotFound,
            });

        var sut = new OidcClientsController(service);

        var result = await sut.Update("missing-client", new OidcClientUpsertRequest(), CancellationToken.None);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    [Fact]
    public async Task Delete_WhenServiceReturnsFalse_ReturnsNotFound()
    {
        var service = Substitute.For<IOidcClientService>();
        service.DeleteAsync("missing-client", Arg.Any<CancellationToken>())
            .Returns(false);

        var sut = new OidcClientsController(service);

        var result = await sut.Delete("missing-client", CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }
}
