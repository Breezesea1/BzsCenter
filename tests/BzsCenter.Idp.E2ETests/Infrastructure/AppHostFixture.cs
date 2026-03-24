using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace BzsCenter.Idp.E2ETests.Infrastructure;

public sealed class AppHostFixture : IAsyncLifetime
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);
    private DistributedApplication? _app;

    public HttpClient IdpClient { get; private set; } = null!;

    public Uri IdpBaseUri { get; private set; } = null!;

    public string BuildUrl(string relativePath)
    {
        return new Uri(IdpBaseUri, relativePath).ToString();
    }

    public async Task InitializeAsync()
    {
        using var cancellationTokenSource = new CancellationTokenSource(StartupTimeout);
        var cancellationToken = cancellationTokenSource.Token;

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.BzsCenter_AppHost>(
            ["Testing:E2E:Enabled=true"]);

        _app = await appHost.BuildAsync(cancellationToken);
        await _app.StartAsync(cancellationToken);

        await _app.ResourceNotifications.WaitForResourceAsync(
            "idp",
            notification =>
                notification.Snapshot.State?.Text == KnownResourceStates.Running
                && notification.Snapshot.Urls.Length > 0,
            cancellationToken);

        IdpBaseUri = _app.GetEndpoint("idp", "https");
        IdpClient = CreateClient(IdpBaseUri);
        await WaitForIdpAsync();
    }

    public async Task DisposeAsync()
    {
        if (IdpClient is not null)
        {
            IdpClient.Dispose();
        }

        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static HttpClient CreateClient(Uri idpBaseUri)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };

        return new HttpClient(handler)
        {
            BaseAddress = idpBaseUri,
        };
    }

    private async Task<bool> IsIdpReadyAsync()
    {
        try
        {
            using var response = await IdpClient.GetAsync("/login");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task WaitForIdpAsync()
    {
        var timeoutAt = DateTimeOffset.UtcNow.Add(StartupTimeout);

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (await IsIdpReadyAsync())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        throw new TimeoutException($"The IDP did not become ready within {StartupTimeout} after Aspire.Hosting.Testing started the AppHost.");
    }

    internal static Uri ResolveIdpBaseUri(HttpClient client)
    {
        if (client.BaseAddress is null)
        {
            throw new InvalidOperationException("The IDP HttpClient must have a BaseAddress before resolving the runtime URL.");
        }

        return client.BaseAddress;
    }
}
