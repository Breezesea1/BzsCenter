using System.Net.Http;

namespace BzsCenter.Idp.E2ETests.Infrastructure;

public sealed class AppHostFixtureTests
{
    [Fact]
    public void ResolveIdpBaseUri_WhenClientHasBaseAddress_ReturnsBaseAddress()
    {
        using var client = new HttpClient
        {
            BaseAddress = new Uri("https://127.0.0.1:4321/", UriKind.Absolute),
        };

        Assert.Equal(client.BaseAddress, AppHostFixture.ResolveIdpBaseUri(client));
    }

    [Fact]
    public void ResolveIdpBaseUri_WhenClientHasNoBaseAddress_ThrowsInvalidOperationException()
    {
        using var client = new HttpClient();

        var exception = Assert.Throws<InvalidOperationException>(() => AppHostFixture.ResolveIdpBaseUri(client));

        Assert.Contains("BaseAddress", exception.Message, StringComparison.Ordinal);
    }
}
