using BzsOIDC.Idp.Models;

namespace BzsOIDC.Idp.UnitTests.Models;

public sealed class BzsUserTests
{
    [Fact]
    public void CreateExternal_WhenDisplayNameMissing_FallsBackToUserName()
    {
        var user = BzsUser.CreateExternal(" octocat ", "octocat@users.noreply.github.com", null);

        Assert.Equal("octocat", user.UserName);
        Assert.Equal("octocat@users.noreply.github.com", user.Email);
        Assert.True(user.EmailConfirmed);
        Assert.Equal("octocat", user.DisplayName);
    }

    [Fact]
    public void CreateExternal_WhenDisplayNameProvided_TrimsDisplayName()
    {
        var user = BzsUser.CreateExternal("octocat", "octocat@users.noreply.github.com", "  The Octocat  ");

        Assert.Equal("The Octocat", user.DisplayName);
    }

    [Fact]
    public void UpdateDisplayName_WhenValueBlank_UsesCurrentUserName()
    {
        var user = BzsUser.CreateExternal("octocat", null, "The Octocat");

        user.UpdateDisplayName("   ");

        Assert.Equal("octocat", user.DisplayName);
    }
}
