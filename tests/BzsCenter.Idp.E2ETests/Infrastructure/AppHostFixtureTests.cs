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

    [Theory]
    [InlineData(@"/home/runner/work/BzsCenter/BzsCenter/tests/BzsCenter.Idp.E2ETests/bin/Release/net10.0/", "Release")]
    [InlineData(@"D:\Coding\BzsCenter\tests\BzsCenter.Idp.E2ETests\bin\Debug\net10.0\", "Debug")]
    public void CreateAppHostStartInfo_UsesCurrentBuildConfiguration(string baseDirectory, string expectedConfiguration)
    {
        var startInfo = AppHostFixture.CreateAppHostStartInfo(baseDirectory);

        Assert.Contains($"-c {expectedConfiguration}", startInfo.Arguments, StringComparison.Ordinal);
    }

    [Fact]
    public void CreateAppHostStartInfo_WhenConfigurationCannotBeInferred_DefaultsToDebug()
    {
        var startInfo = AppHostFixture.CreateAppHostStartInfo(@"/tmp/bzscenter-e2e/");

        Assert.Contains("-c Debug", startInfo.Arguments, StringComparison.Ordinal);
    }
}
