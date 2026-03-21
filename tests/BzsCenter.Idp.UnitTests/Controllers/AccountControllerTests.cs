using BzsCenter.Idp.Controllers;
using BzsCenter.Idp.Models;
using BzsCenter.Idp.UnitTests.TestDoubles;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace BzsCenter.Idp.UnitTests.Controllers;

public sealed class AccountControllerTests
{
    [Fact]
    public async Task Login_WhenCredentialsMissing_RedirectsToLoginWithValidationError()
    {
        var signInManager = CreateSignInManager();
        var sut = CreateSut(signInManager, static url => url == "/admin/users");

        var result = await sut.Login(new AccountController.LoginForm(), "/admin/users");

        var redirect = Assert.IsType<RedirectResult>(result);
        var uri = new Uri($"https://localhost{redirect.Url}");
        var query = QueryHelpers.ParseQuery(uri.Query);

        Assert.Equal("/login", uri.AbsolutePath);
        Assert.Equal("validation", query["error"].ToString());
        Assert.Equal("/admin/users", query["returnUrl"].ToString());
        await signInManager.DidNotReceive()
            .PasswordSignInAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<bool>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task Login_WhenCredentialsValid_LocalRedirectsToSafeReturnUrl()
    {
        var signInManager = CreateSignInManager();
        signInManager.PasswordSignInAsync("admin", "Passw0rd!", true, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Success);

        var sut = CreateSut(signInManager, static url => url == "/admin/users");

        var result = await sut.Login(new AccountController.LoginForm
        {
            UserName = "  admin  ",
            Password = "Passw0rd!",
            RememberMe = true,
        }, "/admin/users");

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/admin/users", redirect.Url);
        await signInManager.Received(1)
            .PasswordSignInAsync("admin", "Passw0rd!", true, true);
    }

    [Fact]
    public async Task Login_WhenCredentialsInvalid_RedirectsToLoginWithoutUnsafeReturnUrl()
    {
        var signInManager = CreateSignInManager();
        signInManager.PasswordSignInAsync("admin", "wrong-password", false, true)
            .Returns(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        var sut = CreateSut(signInManager, static _ => false);

        var result = await sut.Login(new AccountController.LoginForm
        {
            UserName = "admin",
            Password = "wrong-password",
        }, "https://evil.example");

        var redirect = Assert.IsType<RedirectResult>(result);
        var uri = new Uri($"https://localhost{redirect.Url}");
        var query = QueryHelpers.ParseQuery(uri.Query);

        Assert.Equal("/login", uri.AbsolutePath);
        Assert.Equal("invalid_credentials", query["error"].ToString());
        Assert.False(query.ContainsKey("returnUrl"));
    }

    [Fact]
    public void Logout_WhenReturnUrlSafe_SignsOutToLocalTarget()
    {
        var sut = CreateSut(CreateSignInManager(), static url => url == "/admin/users");

        var result = sut.Logout("/admin/users");

        var signOut = Assert.IsType<SignOutResult>(result);
        Assert.Equal([IdentityConstants.ApplicationScheme], signOut.AuthenticationSchemes);
        Assert.Equal("/admin/users", signOut.Properties?.RedirectUri);
    }

    [Fact]
    public void Logout_WhenReturnUrlUnsafe_SignsOutToRoot()
    {
        var sut = CreateSut(CreateSignInManager(), static _ => false);

        var result = sut.Logout("https://evil.example");

        var signOut = Assert.IsType<SignOutResult>(result);
        Assert.Equal([IdentityConstants.ApplicationScheme], signOut.AuthenticationSchemes);
        Assert.Equal("/", signOut.Properties?.RedirectUri);
    }

    private static AccountController CreateSut(SignInManager<BzsUser> signInManager, Func<string, bool> isLocalUrl)
    {
        var urlHelper = Substitute.For<IUrlHelper>();
        urlHelper.IsLocalUrl(Arg.Any<string>())
            .Returns(callInfo => isLocalUrl(callInfo.Arg<string>()));

        return new AccountController(signInManager)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext(),
            },
            Url = urlHelper,
        };
    }

    private static SignInManager<BzsUser> CreateSignInManager()
    {
        return Substitute.For<SignInManager<BzsUser>>(
            new TestUserManager(),
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<BzsUser>>(),
            Options.Create(new IdentityOptions()),
            Substitute.For<ILogger<SignInManager<BzsUser>>>(),
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<BzsUser>>());
    }
}
