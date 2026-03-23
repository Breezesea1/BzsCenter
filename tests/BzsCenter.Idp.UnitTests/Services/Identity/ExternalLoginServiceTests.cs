using System.Security.Claims;
using BzsCenter.Idp.Models;
using BzsCenter.Idp.Services.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace BzsCenter.Idp.UnitTests.Services.Identity;

public sealed class ExternalLoginServiceTests
{
    [Fact]
    public async Task SignInAsync_WhenExternalLoginAlreadyLinked_SignsInExistingUser()
    {
        var userManager = (TestExternalUserManager)CreateUserManager();
        var signInManager = CreateSignInManager(userManager);
        var loginInfo = CreateExternalLoginInfo();
        var createCalled = false;

        signInManager.GetExternalLoginInfoAsync().Returns(loginInfo);
        signInManager.ExternalLoginSignInAsync(loginInfo.LoginProvider, loginInfo.ProviderKey, false, true)
            .Returns(SignInResult.Success);
        userManager.CreateAsyncHandler = _ =>
        {
            createCalled = true;
            return Task.FromResult(IdentityResult.Success);
        };

        var sut = new ExternalLoginService(userManager, signInManager, NullLogger<ExternalLoginService>.Instance);

        var result = await sut.SignInAsync();

        Assert.True(result.Succeeded);
        Assert.Null(result.ErrorCode);
        await signInManager.Received(1)
            .ExternalLoginSignInAsync(loginInfo.LoginProvider, loginInfo.ProviderKey, false, true);
        Assert.False(createCalled);
    }

    [Fact]
    public async Task SignInAsync_WhenExternalLoginUnknown_CreatesUserLinksLoginAndSignsIn()
    {
        var userManager = (TestExternalUserManager)CreateUserManager();
        var signInManager = CreateSignInManager(userManager);
        var loginInfo = CreateExternalLoginInfo();
        BzsUser? createdUser = null;
        BzsUser? linkedUser = null;

        signInManager.GetExternalLoginInfoAsync().Returns(loginInfo);
        signInManager.ExternalLoginSignInAsync(loginInfo.LoginProvider, loginInfo.ProviderKey, false, true)
            .Returns(SignInResult.Failed);
        userManager.FindByEmailAsyncHandler = static _ => Task.FromResult<BzsUser?>(null);
        userManager.FindByNameAsyncHandler = static _ => Task.FromResult<BzsUser?>(null);
        userManager.CreateAsyncHandler = user =>
        {
            createdUser = user;
            return Task.FromResult(IdentityResult.Success);
        };
        userManager.AddLoginAsyncHandler = (user, _) =>
        {
            linkedUser = user;
            return Task.FromResult(IdentityResult.Success);
        };

        var sut = new ExternalLoginService(userManager, signInManager, NullLogger<ExternalLoginService>.Instance);

        var result = await sut.SignInAsync();

        Assert.True(result.Succeeded);
        Assert.NotNull(createdUser);
        Assert.Equal("octocat", createdUser.UserName);
        Assert.Equal("octocat@users.noreply.github.com", createdUser.Email);
        Assert.Equal("The Octocat", createdUser.DisplayName);
        Assert.Same(createdUser, linkedUser);
        await signInManager.Received(1).SignInAsync(Arg.Any<BzsUser>(), false, loginInfo.LoginProvider);
    }

    [Fact]
    public async Task SignInAsync_WhenExternalInfoMissing_ReturnsFailure()
    {
        var userManager = CreateUserManager();
        var signInManager = CreateSignInManager(userManager);
        signInManager.GetExternalLoginInfoAsync().Returns((ExternalLoginInfo?)null);

        var sut = new ExternalLoginService(userManager, signInManager, NullLogger<ExternalLoginService>.Instance);

        var result = await sut.SignInAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("external_login_failed", result.ErrorCode);
    }

    [Fact]
    public async Task SignInAsync_WhenMatchedEmailUserLockedOut_ReturnsFailureWithoutSigningIn()
    {
        var userManager = (TestExternalUserManager)CreateUserManager();
        var signInManager = CreateSignInManager(userManager);
        var loginInfo = CreateExternalLoginInfo();
        var existingUser = new BzsUser
        {
            UserName = "existing-user",
            Email = "octocat@users.noreply.github.com",
        };
        var signInCalled = false;

        signInManager.GetExternalLoginInfoAsync().Returns(loginInfo);
        signInManager.ExternalLoginSignInAsync(loginInfo.LoginProvider, loginInfo.ProviderKey, false, true)
            .Returns(SignInResult.Failed);
        signInManager.When(manager => manager.SignInAsync(Arg.Any<BzsUser>(), false, loginInfo.LoginProvider))
            .Do(_ => signInCalled = true);
        userManager.FindByEmailAsyncHandler = _ => Task.FromResult<BzsUser?>(existingUser);
        userManager.IsLockedOutAsyncHandler = _ => Task.FromResult(true);

        var sut = new ExternalLoginService(userManager, signInManager, NullLogger<ExternalLoginService>.Instance);

        var result = await sut.SignInAsync();

        Assert.False(result.Succeeded);
        Assert.Equal("external_login_failed", result.ErrorCode);
        Assert.False(signInCalled);
    }

    private static ExternalLoginInfo CreateExternalLoginInfo()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "12345"),
            new Claim(ClaimTypes.Name, "octocat"),
            new Claim(ClaimTypes.Email, "octocat@users.noreply.github.com"),
            new Claim("urn:github:name", "The Octocat"),
        ], ExternalLoginProvider.GitHubScheme));

        return new ExternalLoginInfo(principal, ExternalLoginProvider.GitHubScheme, "12345", "GitHub");
    }

    private static UserManager<BzsUser> CreateUserManager()
    {
        return new TestExternalUserManager();
    }

    private static SignInManager<BzsUser> CreateSignInManager(UserManager<BzsUser> userManager)
    {
        return Substitute.For<SignInManager<BzsUser>>(
            userManager,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<BzsUser>>(),
            Options.Create(new IdentityOptions()),
            Substitute.For<ILogger<SignInManager<BzsUser>>>(),
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<BzsUser>>());
    }

    private sealed class TestExternalUserManager()
        : UserManager<BzsUser>(
            Substitute.For<IUserStore<BzsUser>>(),
            Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
            new PasswordHasher<BzsUser>(),
            Array.Empty<IUserValidator<BzsUser>>(),
            Array.Empty<IPasswordValidator<BzsUser>>(),
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            new ServiceCollection().BuildServiceProvider(),
            Substitute.For<ILogger<UserManager<BzsUser>>>())
    {
        public Func<string, Task<BzsUser?>> FindByEmailAsyncHandler { get; set; } = static _ => Task.FromResult<BzsUser?>(null);
        public Func<string, Task<BzsUser?>> FindByNameAsyncHandler { get; set; } = static _ => Task.FromResult<BzsUser?>(null);
        public Func<BzsUser, Task<IdentityResult>> CreateAsyncHandler { get; set; } = static _ => Task.FromResult(IdentityResult.Success);
        public Func<BzsUser, UserLoginInfo, Task<IdentityResult>> AddLoginAsyncHandler { get; set; } = static (_, _) => Task.FromResult(IdentityResult.Success);
        public Func<BzsUser, Task<bool>> IsLockedOutAsyncHandler { get; set; } = static _ => Task.FromResult(false);

        public override Task<BzsUser?> FindByEmailAsync(string email)
        {
            return FindByEmailAsyncHandler(email);
        }

        public override Task<BzsUser?> FindByNameAsync(string userName)
        {
            return FindByNameAsyncHandler(userName);
        }

        public override Task<IdentityResult> CreateAsync(BzsUser user)
        {
            return CreateAsyncHandler(user);
        }

        public override Task<IdentityResult> AddLoginAsync(BzsUser user, UserLoginInfo login)
        {
            return AddLoginAsyncHandler(user, login);
        }

        public override Task<bool> IsLockedOutAsync(BzsUser user)
        {
            return IsLockedOutAsyncHandler(user);
        }
    }
}
