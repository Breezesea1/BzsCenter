using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;

namespace BzsOIDC.Idp.E2ETests.Infrastructure;

public sealed class AppHostFixture : IAsyncLifetime
{
    private DistributedApplication? _app;

    public HttpClient IdpClient { get; private set; } = null!;

    public Uri IdpBaseUri { get; private set; } = null!;

    public string BuildUrl(string relativePath)
    {
        return new Uri(IdpBaseUri, relativePath).ToString();
    }

    public async Task InitializeAsync()
    {
        var runningInCi = IsRunningInCi();
        using var startupCancellationTokenSource = new CancellationTokenSource(ResolveAspireStartupTimeout(runningInCi));
        var startupCancellationToken = startupCancellationTokenSource.Token;

        var appHost = await DistributedApplicationTestingBuilder.CreateAsync<Projects.BzsOIDC_AppHost>(
            ResolveAppHostArgs(IsSmokeProfileEnabled()));

        _app = await appHost.BuildAsync(startupCancellationToken);

        try
        {
            await _app.StartAsync(startupCancellationToken);
        }
        catch (OperationCanceledException) when (startupCancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException(
                $"Aspire.Hosting.Testing did not start the distributed application within {ResolveAspireStartupTimeout(runningInCi)}. " +
                "On cold CI runners, Postgres/Redis container startup and migrator completion can take longer than local runs.");
        }

        await _app.ResourceNotifications.WaitForResourceAsync(
            "idp",
            notification =>
                notification.Snapshot.State?.Text == KnownResourceStates.Running
                && notification.Snapshot.Urls.Length > 0,
            startupCancellationToken);

        IdpBaseUri = _app.GetEndpoint("idp", "https");
        IdpClient = CreateClient(IdpBaseUri);
        await WaitForIdpAsync(ResolveIdpReadinessTimeout(runningInCi));
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

    private async Task<string> GetIdpReadinessDiagnosticAsync()
    {
        try
        {
            using var response = await IdpClient.GetAsync("/login");
            var body = await response.Content.ReadAsStringAsync();
            var snippet = body.Length > 400 ? body[..400] : body;

            return $"Last /login probe returned {(int)response.StatusCode} {response.StatusCode}.{Environment.NewLine}{snippet}";
        }
        catch (Exception exception)
        {
            return $"Last /login probe failed with {exception.GetType().Name}: {exception.Message}";
        }
    }

    private async Task WaitForIdpAsync(TimeSpan readinessTimeout)
    {
        var timeoutAt = DateTimeOffset.UtcNow.Add(readinessTimeout);

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (await IsIdpReadyAsync())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        var diagnostic = await GetIdpReadinessDiagnosticAsync();
        throw new TimeoutException($"The IDP did not become ready within {readinessTimeout} after Aspire.Hosting.Testing started the AppHost.{Environment.NewLine}{diagnostic}");
    }

    internal static TimeSpan ResolveAspireStartupTimeout(bool isCi)
    {
        return isCi ? TimeSpan.FromMinutes(20) : TimeSpan.FromMinutes(5);
    }

    internal static TimeSpan ResolveIdpReadinessTimeout(bool isCi)
    {
        return TimeSpan.FromMinutes(5);
    }

    internal static bool IsRunningInCi()
    {
        return string.Equals(Environment.GetEnvironmentVariable("CI"), bool.TrueString, StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsSmokeProfileEnabled()
    {
        return string.Equals(Environment.GetEnvironmentVariable("TESTING__SMOKE__ENABLED"), bool.TrueString, StringComparison.OrdinalIgnoreCase);
    }

    internal static string[] ResolveAppHostArgs(bool smokeEnabled)
    {
        return smokeEnabled
            ? ["Testing:E2E:Enabled=true", "Testing:Smoke:Enabled=true"]
            : ["Testing:E2E:Enabled=true"];
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
