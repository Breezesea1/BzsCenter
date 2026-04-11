using BzsOIDC.Idp.Services;
using Microsoft.Extensions.Configuration;

namespace BzsOIDC.Idp.UnitTests.Services;

public sealed class TestingConfigurationExtensionsTests
{
    [Theory]
    [InlineData("true", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("", false)]
    public void IsSmokeTestingEnabled_ReturnsExpectedResult(string configuredValue, bool expected)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Testing:Smoke:Enabled"] = configuredValue,
            })
            .Build();

        Assert.Equal(expected, configuration.IsSmokeTestingEnabled());
    }

    [Fact]
    public void IsSmokeTestingEnabled_WhenMissing_ReturnsFalse()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();

        Assert.False(configuration.IsSmokeTestingEnabled());
    }
}
