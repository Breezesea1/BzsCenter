using Microsoft.Extensions.Options;

namespace BzsOIDC.Idp.Services.Identity;

public sealed record ExternalLoginProvider(string RouteSegment, string Scheme, string DisplayName)
{
    internal const string GitHubRouteSegment = "github";
    internal const string GitHubScheme = "GitHub";
}

public interface IExternalLoginProviderStore
{
    IReadOnlyList<ExternalLoginProvider> GetEnabledProviders();

    bool TryGetProvider(string routeSegment, out ExternalLoginProvider provider);
}

internal sealed class ExternalLoginProviderStore(IOptions<ExternalAuthenticationOptions> options) : IExternalLoginProviderStore
{
    private readonly IReadOnlyList<ExternalLoginProvider> _providers = BuildProviders(options.Value);

    public IReadOnlyList<ExternalLoginProvider> GetEnabledProviders()
    {
        return _providers;
    }

    public bool TryGetProvider(string routeSegment, out ExternalLoginProvider provider)
    {
        provider = _providers.FirstOrDefault(candidate =>
            string.Equals(candidate.RouteSegment, routeSegment, StringComparison.OrdinalIgnoreCase))
            ?? new ExternalLoginProvider(string.Empty, string.Empty, string.Empty);

        return !string.IsNullOrWhiteSpace(provider.Scheme);
    }

    private static IReadOnlyList<ExternalLoginProvider> BuildProviders(ExternalAuthenticationOptions options)
    {
        var providers = new List<ExternalLoginProvider>();

        if (options.GitHub.IsConfigured)
        {
            providers.Add(new ExternalLoginProvider(ExternalLoginProvider.GitHubRouteSegment, ExternalLoginProvider.GitHubScheme, "GitHub"));
        }

        return providers;
    }
}
