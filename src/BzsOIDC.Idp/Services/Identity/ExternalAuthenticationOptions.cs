namespace BzsOIDC.Idp.Services.Identity;

internal sealed class ExternalAuthenticationOptions
{
    public GitHubExternalAuthenticationOptions GitHub { get; init; } = new();
}

internal sealed class GitHubExternalAuthenticationOptions
{
    private static readonly HashSet<string> PlaceholderValues =
    [
        "github-client-id",
        "github-client-secret",
        "replace-with-github-client-id",
        "replace-with-github-client-secret",
    ];

    public string ClientId { get; init; } = string.Empty;
    public string ClientSecret { get; init; } = string.Empty;
    public string CallbackPath { get; init; } = "/signin-github";

    internal bool IsConfigured =>
        IsMeaningfulValue(ClientId) &&
        IsMeaningfulValue(ClientSecret);

    private static bool IsMeaningfulValue(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               !PlaceholderValues.Contains(value.Trim());
    }
}
