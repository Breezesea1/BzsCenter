using BzsCenter.AppHost;

using BzsCenter.Idp.Services.Identity;

namespace BzsCenter.Idp.E2ETests.Infrastructure;

public sealed class AppHostE2EOptimizationTests
{
    [Theory]
    [InlineData("true", null, "Memory")]
    [InlineData("TRUE", null, "Memory")]
    [InlineData(null, "true", "Memory")]
    [InlineData(null, "TRUE", "Memory")]
    [InlineData(null, null, "Redis")]
    [InlineData("", "", "Redis")]
    public void ResolveCacheType_UsesMemoryForE2EAndSmokeModes(string? e2eEnabled, string? smokeEnabled, string expected)
    {
        Assert.Equal(expected, AppHostModelSettings.ResolveCacheType(e2eEnabled, smokeEnabled));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData(null, false)]
    [InlineData("", false)]
    public void IsSmokeProfileEnabled_DetectsSmokeMode(string? smokeEnabled, bool expected)
    {
        Assert.Equal(expected, AppHostModelSettings.IsSmokeProfileEnabled(smokeEnabled));
    }

    [Theory]
    [InlineData("true", "Data Source=")]
    [InlineData(null, "Host=")]
    public void ResolveIdentityConnectionString_UsesSqliteForSmoke(string? smokeEnabled, string expectedPrefix)
    {
        var connectionString = AppHostModelSettings.ResolveIdentityConnectionString(smokeEnabled, "Host=postgres;Database=idp");

        Assert.StartsWith(expectedPrefix, connectionString, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true, 4)]
    [InlineData(false, 7)]
    public void ResolveSeedMode_ReturnsExpectedSeedShape(bool smokeEnabled, int expectedPermissionScopeCount)
    {
        var options = SmokeIdentitySeedProfile.Resolve(smokeEnabled, new IdentitySeedOptions());

        Assert.Equal(expectedPermissionScopeCount, options.PermissionScopes.Count);
    }

    [Theory]
    [InlineData("true", false)]
    [InlineData("TRUE", false)]
    [InlineData(null, true)]
    [InlineData("", true)]
    public void ShouldUsePersistentPostgresVolume_DisablesVolumeInE2EMode(string? e2eEnabled, bool expected)
    {
        Assert.Equal(expected, AppHostModelSettings.ShouldUsePersistentPostgresVolume(e2eEnabled));
    }
}
