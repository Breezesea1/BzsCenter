using System.Net;
using BzsCenter.Idp.Infra.Oidc;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BzsCenter.Idp.UnitTests.Infra.Oidc;

public class ForwardedHeadersServiceExtensionsTests
{
    [Fact]
    public void AddForwardedHeaders_ConfiguresForwardedHeadersAndTrustedProxies()
    {
        var services = new ServiceCollection();
        services.AddOptions<BzsForwardedHeadersOptions>().Configure(options =>
        {
            options.KnownProxies = ["127.0.0.1", "invalid-ip"];
            options.KnownIpNetworks = ["10.0.0.0/16", "invalid-cidr"];
        });

        services.AddForwardedHeaders();

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<ForwardedHeadersOptions>>().Value;

        var expectedForwardedHeaders =
            ForwardedHeaders.XForwardedProto |
            ForwardedHeaders.XForwardedFor |
            ForwardedHeaders.XForwardedHost;

        Assert.Equal(expectedForwardedHeaders, options.ForwardedHeaders);
        Assert.Equal(2, options.ForwardLimit);
        Assert.Single(options.KnownProxies);
        Assert.Equal(IPAddress.Parse("127.0.0.1"), options.KnownProxies[0]);
        Assert.Single(options.KnownIPNetworks);
        Assert.Equal(16, options.KnownIPNetworks[0].PrefixLength);
        Assert.Equal("10.0.0.0/16", options.KnownIPNetworks[0].ToString());
    }
}
