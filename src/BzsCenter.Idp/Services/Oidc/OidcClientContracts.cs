using BzsCenter.Shared.Infrastructure.Authorization;
using OpenIddict.Abstractions;

namespace BzsCenter.Idp.Services.Oidc;

public sealed class OidcClientUpsertRequest
{
    public string? ClientId { get; init; }
    public string? ClientSecret { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public bool PublicClient { get; init; }
    public bool RequireProofKeyForCodeExchange { get; init; } = true;
    public string[] GrantTypes { get; init; } = [OpenIddictConstants.GrantTypes.AuthorizationCode];
    public string[] Scopes { get; init; } = [PermissionConstants.ScopeApi];
    public string[] RedirectUris { get; init; } = [];
    public string[] PostLogoutRedirectUris { get; init; } = [];
}

public sealed class OidcClientResponse
{
    public string ClientId { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public bool PublicClient { get; init; }
    public string[] GrantTypes { get; init; } = [];
    public string[] Scopes { get; init; } = [];
    public string[] RedirectUris { get; init; } = [];
    public string[] PostLogoutRedirectUris { get; init; } = [];
    public string[] Permissions { get; init; } = [];
    public string[] Requirements { get; init; } = [];
}

public sealed class OidcClientRegistrationResponse
{
    public string ClientId { get; init; } = string.Empty;
    public string? ClientSecret { get; init; }
    public string DisplayName { get; init; } = string.Empty;
}
