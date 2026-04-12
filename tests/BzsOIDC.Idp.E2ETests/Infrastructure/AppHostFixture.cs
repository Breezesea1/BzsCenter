using System.Diagnostics;
using System.Text.Json;

namespace BzsOIDC.Idp.E2ETests.Infrastructure;

public sealed class AppHostFixture : IAsyncLifetime
{
    private const string AspireExecutableName = "aspire";
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public HttpClient IdpClient { get; private set; } = null!;

    public Uri IdpBaseUri { get; private set; } = null!;

    public string BuildUrl(string relativePath)
    {
        return new Uri(IdpBaseUri, relativePath).ToString();
    }

    public async Task InitializeAsync()
    {
        var runningInCi = IsRunningInCi();

        await StopAppHostAsync();
        await RunAspireCommandAsync(
            BuildAspireStartArguments(),
            ResolveAspireStartupTimeout(runningInCi),
            BuildAspireEnvironment());
        await RunAspireCommandAsync(
            BuildAspireWaitArguments("idp"),
            ResolveAspireStartupTimeout(runningInCi));

        IdpBaseUri = await ResolveIdpBaseUriFromCliAsync(ResolveAspireStartupTimeout(runningInCi));
        IdpClient = CreateClient(IdpBaseUri);
        await WaitForIdpAsync(ResolveIdpReadinessTimeout(runningInCi));
    }

    public async Task DisposeAsync()
    {
        if (IdpClient is not null)
        {
            IdpClient.Dispose();
        }

        await StopAppHostAsync();
    }

    private static string AppHostProjectPath => ResolveAppHostProjectPath();

    private static string AppHostDirectory => Path.GetDirectoryName(AppHostProjectPath)
        ?? throw new InvalidOperationException($"Could not resolve AppHost directory from '{AppHostProjectPath}'.");

    private static string ResolveAppHostProjectPath()
    {
        var projectPath = Projects.BzsOIDC_AppHost.ProjectPath;
        if (string.Equals(Path.GetExtension(projectPath), ".csproj", StringComparison.OrdinalIgnoreCase))
        {
            return projectPath;
        }

        return Path.Combine(projectPath, "BzsOIDC.AppHost.csproj");
    }

    private static string BuildAspireStartArguments()
    {
        return $"start --apphost \"{AppHostProjectPath}\"";
    }

    private static string BuildAspireWaitArguments(string resourceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(resourceName);
        return $"wait {resourceName} --apphost \"{AppHostProjectPath}\"";
    }

    private static string BuildAspireResourcesArguments()
    {
        return $"resources --apphost \"{AppHostProjectPath}\" --format Json";
    }

    private static string BuildAspireStopArguments()
    {
        return $"stop --apphost \"{AppHostProjectPath}\"";
    }

    private static Dictionary<string, string?> BuildAspireEnvironment()
    {
        return new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["TESTING__E2E__ENABLED"] = bool.TrueString,
            ["TESTING__SMOKE__ENABLED"] = IsSmokeProfileEnabled().ToString(),
        };
    }

    private static async Task StopAppHostAsync()
    {
        await RunAspireCommandAsync(BuildAspireStopArguments(), TimeSpan.FromMinutes(1), allowNonZeroExit: true);
    }

    private static async Task<Uri> ResolveIdpBaseUriFromCliAsync(TimeSpan timeout)
    {
        var resourcesJson = await RunAspireCommandAsync(BuildAspireResourcesArguments(), timeout);
        var payload = JsonSerializer.Deserialize<AspireResourcesPayload>(resourcesJson, _jsonOptions)
            ?? throw new InvalidOperationException("Aspire CLI returned empty resources payload.");

        var idpResource = payload.Resources.FirstOrDefault(resource =>
            string.Equals(resource.DisplayName, "idp", StringComparison.OrdinalIgnoreCase));
        if (idpResource is null)
        {
            throw new InvalidOperationException("Could not find the 'idp' resource in Aspire CLI output.");
        }

        var httpsUrl = idpResource.Urls.FirstOrDefault(url =>
            string.Equals(url.Name, "https", StringComparison.OrdinalIgnoreCase))?.Url;
        if (!Uri.TryCreate(httpsUrl, UriKind.Absolute, out var baseUri))
        {
            throw new InvalidOperationException("Could not resolve the IDP https URL from Aspire CLI output.");
        }

        return baseUri;
    }

    private static async Task<string> RunAspireCommandAsync(
        string arguments,
        TimeSpan timeout,
        IReadOnlyDictionary<string, string?>? environmentVariables = null,
        bool allowNonZeroExit = false)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = AspireExecutableName,
                Arguments = arguments,
                WorkingDirectory = AppHostDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            }
        };

        if (environmentVariables is not null)
        {
            foreach (var (key, value) in environmentVariables)
            {
                process.StartInfo.Environment[key] = value;
            }
        }

        process.Start();

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        var waitTask = process.WaitForExitAsync();

        var completedTask = await Task.WhenAny(waitTask, Task.Delay(timeout));
        if (completedTask != waitTask)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Ignore cleanup exceptions from timed out processes.
            }

            throw new TimeoutException($"Command '{AspireExecutableName} {arguments}' did not complete within {timeout}.");
        }

        await waitTask;
        var output = await outputTask;
        var error = await errorTask;
        var combinedOutput = string.IsNullOrWhiteSpace(error)
            ? output
            : $"{output}{Environment.NewLine}{error}";

        if (!allowNonZeroExit && process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Command '{AspireExecutableName} {arguments}' exited with code {process.ExitCode}.{Environment.NewLine}{combinedOutput}");
        }

        return combinedOutput;
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
        var configuredValue = Environment.GetEnvironmentVariable("TESTING__SMOKE__ENABLED");
        if (configuredValue is null)
        {
            return true;
        }

        return string.Equals(configuredValue, bool.TrueString, StringComparison.OrdinalIgnoreCase);
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

    private sealed class AspireResourcesPayload
    {
        public AspireResource[] Resources { get; init; } = [];
    }

    private sealed class AspireResource
    {
        public string? DisplayName { get; init; }

        public AspireUrl[] Urls { get; init; } = [];
    }

    private sealed class AspireUrl
    {
        public string? Name { get; init; }

        public string? Url { get; init; }
    }
}
