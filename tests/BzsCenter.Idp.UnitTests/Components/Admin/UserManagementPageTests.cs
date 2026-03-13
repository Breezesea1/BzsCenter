using System.Security.Claims;
using Bunit;
using Bunit.JSInterop;
using BzsCenter.Idp.Components.Admin;
using BzsCenter.Idp.Models;
using BzsCenter.Idp.Services.Identity;
using BzsCenter.Idp.UnitTests.TestDoubles;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using NSubstitute;

namespace BzsCenter.Idp.UnitTests.Components.Admin;

public sealed class UserManagementPageTests
{
    [Fact]
    public void Search_WhenTriggeredFromLaterPage_ResetsToFirstPageOfMatches()
    {
        using var context = CreateContext();

        var users = Enumerable.Range(1, 15)
            .Select(index => new BzsUser
            {
                Id = Guid.Parse($"00000000-0000-0000-0000-{index:000000000000}"),
                UserName = $"alpha-user-{index:00}",
                Email = $"alpha{index:00}@example.com"
            })
            .Concat(Enumerable.Range(16, 10).Select(index => new BzsUser
            {
                Id = Guid.Parse($"00000000-0000-0000-0000-{index:000000000000}"),
                UserName = $"beta-user-{index:00}",
                Email = $"beta{index:00}@example.com"
            }))
            .ToArray();

        var userService = Substitute.For<IUserService>();
        userService.GetAllAsync(Arg.Any<CancellationToken>()).Returns(Task.FromResult<IReadOnlyList<BzsUser>>(users));

        context.Services.AddSingleton(userService);
        context.Services.AddSingleton<UserManager<BzsUser>>(new TestUserManager());
        context.Services.AddSingleton<IStringLocalizer<UserManagement>, TestStringLocalizer<UserManagement>>();
        context.Services.AddSingleton<IHttpContextAccessor>(CreateAdminHttpContextAccessor());

        var cut = context.Render<UserManagement>();

        cut.WaitForAssertion(() => Assert.Equal(10, cut.FindAll("tbody tr").Count));

        cut.FindAll("button").Single(button => button.TextContent.Contains("NextPage", StringComparison.Ordinal)).Click();
        cut.FindAll("button").Single(button => button.TextContent.Contains("NextPage", StringComparison.Ordinal)).Click();

        cut.WaitForAssertion(() => Assert.Contains("beta-user-21", cut.Markup, StringComparison.Ordinal));

        cut.Find("#user-search").Input("alpha");

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("alpha-user-01", cut.Markup, StringComparison.Ordinal);
            Assert.DoesNotContain("alpha-user-11", cut.Markup, StringComparison.Ordinal);
            Assert.Equal(10, cut.FindAll("tbody tr").Count);
        });
    }

    private static BunitContext CreateContext()
    {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.JSInterop.SetupModule("./Components/Admin/AdminDialogShell.razor.js")
            .SetupVoid("activate", _ => true);
        return context;
    }

    private static IHttpContextAccessor CreateAdminHttpContextAccessor()
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.Role, "Admin"),
                new Claim(ClaimTypes.Role, "admin")
            ],
            "TestAuth");

        return new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };
    }
}
