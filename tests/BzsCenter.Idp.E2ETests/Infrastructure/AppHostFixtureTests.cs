using System.Diagnostics;

namespace BzsCenter.Idp.E2ETests.Infrastructure;

public sealed class AppHostFixtureTests
{
    [Fact]
    public void CreateAppHostStartInfo_UsesDotnetRunWithoutBuild()
    {
        var startInfo = AppHostFixture.CreateAppHostStartInfo();

        Assert.Equal("dotnet", startInfo.FileName);
        Assert.Contains("run", startInfo.Arguments, StringComparison.Ordinal);
        Assert.Contains("--no-build", startInfo.Arguments, StringComparison.Ordinal);
        Assert.Contains("src/BzsCenter.AppHost/BzsCenter.AppHost.csproj", startInfo.Arguments, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("true", startInfo.Environment["Testing__E2E__Enabled"]);
    }
}
