using BzsOIDC.Idp.Services;
using Microsoft.Extensions.Configuration;

namespace BzsOIDC.Idp.UnitTests.Services;

public sealed class AspireDatabaseConfigurationExtensionsTests
{
    [Theory]
    [InlineData(null, null, true)]
    [InlineData("Host=postgres;Database=idp", null, true)]
    [InlineData("Data Source=smoke-idp.db", null, false)]
    [InlineData(null, "true", false)]
    [InlineData("Host=postgres;Database=idp", "true", false)]
    public void ShouldEnrichIdpDbFromAspire_ReturnsExpectedResult(string? connectionString, string? smokeEnabled, bool expected)
    {
        var values = new Dictionary<string, string?>();

        if (connectionString is not null)
        {
            values["ConnectionStrings:DefaultConnection"] = connectionString;
        }

        if (smokeEnabled is not null)
        {
            values["Testing:Smoke:Enabled"] = smokeEnabled;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        Assert.Equal(expected, configuration.ShouldEnrichIdpDbFromAspire());
    }
}
