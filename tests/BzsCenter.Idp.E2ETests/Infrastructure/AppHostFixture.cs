using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace BzsCenter.Idp.E2ETests.Infrastructure;

public sealed class AppHostFixture : IAsyncLifetime
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromMinutes(5);
    private readonly StringBuilder _aspireLogs = new();
    private Process? _aspireProcess;
    private bool _startedAspire;

    public HttpClient IdpClient { get; private set; } = null!;

    public Uri IdpBaseUri { get; private set; } = ResolveIdpBaseUri();

    public string BuildUrl(string relativePath)
    {
        return new Uri(IdpBaseUri, relativePath).ToString();
    }

    public async Task InitializeAsync()
    {
        IdpClient = CreateClient(IdpBaseUri);

        if (await IsIdpReadyAsync())
        {
            return;
        }

        await StartAspireAsync();
        await WaitForIdpAsync();
    }

    public Task DisposeAsync()
    {
        IdpClient.Dispose();

        if (_startedAspire && _aspireProcess is { HasExited: false })
        {
            _aspireProcess.Kill(entireProcessTree: true);
            _aspireProcess.WaitForExit(15_000);
        }

        _aspireProcess?.Dispose();
        return Task.CompletedTask;
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

    private async Task StartAspireAsync()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "aspire",
            Arguments = "run",
            WorkingDirectory = GetRepositoryRoot(),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        startInfo.Environment.Remove("http_proxy");
        startInfo.Environment.Remove("https_proxy");
        startInfo.Environment.Remove("all_proxy");
        startInfo.Environment.Remove("no_proxy");
        startInfo.Environment["Testing__E2E__Enabled"] = "true";

        _aspireProcess = new Process
        {
            StartInfo = startInfo,
            EnableRaisingEvents = true,
        };

        _aspireProcess.OutputDataReceived += (_, args) => AppendLog(args.Data);
        _aspireProcess.ErrorDataReceived += (_, args) => AppendLog(args.Data);

        if (!_aspireProcess.Start())
        {
            throw new InvalidOperationException("Failed to start 'aspire run' for E2E tests.");
        }

        _aspireProcess.BeginOutputReadLine();
        _aspireProcess.BeginErrorReadLine();
        _startedAspire = true;

        await Task.Delay(TimeSpan.FromSeconds(2));
    }

    private async Task WaitForIdpAsync()
    {
        var timeoutAt = DateTimeOffset.UtcNow.Add(StartupTimeout);

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            if (_aspireProcess is { HasExited: true })
            {
                throw new InvalidOperationException($"'aspire run' exited before the IDP became ready.{Environment.NewLine}{_aspireLogs}");
            }

            if (await IsIdpReadyAsync())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        throw new TimeoutException($"The IDP did not become ready within {StartupTimeout}.{Environment.NewLine}{_aspireLogs}");
    }

    private static Uri ResolveIdpBaseUri()
    {
        var launchSettingsPath = Path.Combine(
            GetRepositoryRoot(),
            "src",
            "BzsCenter.Idp",
            "Properties",
            "launchSettings.json");

        using var stream = File.OpenRead(launchSettingsPath);
        using var document = JsonDocument.Parse(stream);

        if (!document.RootElement.TryGetProperty("profiles", out var profiles))
        {
            throw new InvalidOperationException("Unable to locate launch profiles for BzsCenter.Idp.");
        }

        foreach (var profile in profiles.EnumerateObject())
        {
            if (!profile.Value.TryGetProperty("applicationUrl", out var applicationUrlProperty))
            {
                continue;
            }

            var applicationUrls = applicationUrlProperty.GetString();
            if (string.IsNullOrWhiteSpace(applicationUrls))
            {
                continue;
            }

            var httpsUrl = applicationUrls
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault(static url => url.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(httpsUrl))
            {
                return new Uri(httpsUrl, UriKind.Absolute);
            }
        }

        throw new InvalidOperationException("Unable to resolve the HTTPS application URL for BzsCenter.Idp from launchSettings.json.");
    }

    private static string GetRepositoryRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
    }

    private void AppendLog(string? line)
    {
        if (!string.IsNullOrWhiteSpace(line))
        {
            _aspireLogs.AppendLine(line);
        }
    }
}
