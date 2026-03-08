using System.Net;
using BzsCenter.Idp.Controllers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BzsCenter.Idp.IntegrationTests;

public sealed class PreferencesControllerIntegrationTests : IAsyncLifetime
{
    private WebApplication _app = null!;
    private HttpClient _client = null!;

    [Fact]
    public async Task SetCulture_ThroughLegacyAccountRoute_SetsCultureCookieAndRedirects()
    {
        using var response = await _client.GetAsync("/account/set-culture?culture=en-US&returnUrl=%2Flogin");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/login", response.Headers.Location?.OriginalString);

        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains(".AspNetCore.Culture", setCookie, StringComparison.Ordinal);
        Assert.Contains("c%3Den-US%7Cuic%3Den-US", setCookie, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SetTheme_ThroughPreferencesRoute_SetsThemeCookieAndRedirects()
    {
        using var response = await _client.GetAsync("/preferences/set-theme?theme=dark&returnUrl=%2Fsettings");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("/settings", response.Headers.Location?.OriginalString);

        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("bzs-theme=dark", setCookie, StringComparison.Ordinal);
    }

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            EnvironmentName = Environments.Development,
        });

        builder.WebHost.UseTestServer();
        builder.Services
            .AddControllers()
            .AddApplicationPart(typeof(PreferencesController).Assembly);

        _app = builder.Build();
        _app.MapControllers();

        await _app.StartAsync();
        _client = _app.GetTestClient();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
