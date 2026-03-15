using System.Diagnostics;
using System.Text;

namespace BzsCenter.Idp.E2ETests.Infrastructure;

public sealed class AppHostFixture : IAsyncLifetime
{
    private static readonly Uri IdpUri = new("https://localhost:7076");
    private readonly StringBuilder _aspireLogs = new();
    private Process? _aspireProcess;
    private bool _startedAspire;

    public HttpClient IdpClient { get; private set; } = null!;

    public Uri IdpBaseUri => IdpUri;

    public string BuildUrl(string relativePath)
    {
        return new Uri(IdpBaseUri, relativePath).ToString();
    }

    public async Task InitializeAsync()
    {
        IdpClient = CreateClient();

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

    private static HttpClient CreateClient()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        };

        return new HttpClient(handler)
        {
            BaseAddress = IdpUri,
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
        var timeoutAt = DateTimeOffset.UtcNow.AddMinutes(2);

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

        throw new TimeoutException($"The IDP did not become ready within the allotted time.{Environment.NewLine}{_aspireLogs}");
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
