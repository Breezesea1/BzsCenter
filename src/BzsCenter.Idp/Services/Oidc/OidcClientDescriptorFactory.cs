using System.Security.Cryptography;
using OpenIddict.Abstractions;

namespace BzsCenter.Idp.Services.Oidc;

public static class OidcClientDescriptorFactory
{
    /// <summary>
    /// 校验输入。
    /// </summary>
    /// <param name="request">参数request。</param>
    /// <returns>执行结果。</returns>
    public static IReadOnlyList<string> ValidateRequest(OidcClientUpsertRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            errors.Add("DisplayName is required.");
        }

        if (request.PublicClient && !string.IsNullOrWhiteSpace(request.ClientSecret))
        {
            errors.Add("Public clients must not specify ClientSecret.");
        }

        var grantTypes = request.GrantTypes
            .Where(static grantType => !string.IsNullOrWhiteSpace(grantType))
            .Select(static grantType => grantType.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (grantTypes.Length == 0)
        {
            errors.Add("At least one grant type must be provided.");
        }

        var hasAuthorizationCode = grantTypes.Contains(OpenIddictConstants.GrantTypes.AuthorizationCode,
            StringComparer.OrdinalIgnoreCase);
        var hasRedirectUris = request.RedirectUris.Any(static uri => !string.IsNullOrWhiteSpace(uri));

        if (hasAuthorizationCode && !hasRedirectUris)
        {
            errors.Add("Authorization code clients must provide at least one redirect URI.");
        }

        if (!TryValidateAbsoluteUris(request.RedirectUris, out var redirectError))
        {
            errors.Add(redirectError!);
        }

        if (!TryValidateAbsoluteUris(request.PostLogoutRedirectUris, out var postLogoutRedirectError))
        {
            errors.Add(postLogoutRedirectError!);
        }

        return errors;
    }

    /// <summary>
    /// 创建数据。
    /// </summary>
    /// <param name="request">参数request。</param>
    /// <param name="clientId">参数clientId。</param>
    /// <returns>执行结果。</returns>
    public static OpenIddictApplicationDescriptor CreateDescriptor(OidcClientUpsertRequest request, string clientId)
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            DisplayName = request.DisplayName.Trim(),
            ClientType = request.PublicClient
                ? OpenIddictConstants.ClientTypes.Public
                : OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Explicit,
        };

        if (!request.PublicClient)
        {
            descriptor.ClientSecret = string.IsNullOrWhiteSpace(request.ClientSecret)
                ? GenerateClientSecret()
                : request.ClientSecret.Trim();
        }

        descriptor.Permissions.UnionWith(BuildPermissions(request));

        if (request.RequireProofKeyForCodeExchange)
        {
            descriptor.Requirements.Add(OpenIddictConstants.Requirements.Features.ProofKeyForCodeExchange);
        }

        foreach (var uri in request.RedirectUris
                     .Where(static uri => !string.IsNullOrWhiteSpace(uri))
                     .Select(static uri => new Uri(uri.Trim(), UriKind.Absolute)))
        {
            descriptor.RedirectUris.Add(uri);
        }

        foreach (var uri in request.PostLogoutRedirectUris
                     .Where(static uri => !string.IsNullOrWhiteSpace(uri))
                     .Select(static uri => new Uri(uri.Trim(), UriKind.Absolute)))
        {
            descriptor.PostLogoutRedirectUris.Add(uri);
        }

        return descriptor;
    }

    /// <summary>
    /// 构建并返回结果。
    /// </summary>
    /// <param name="request">参数request。</param>
    /// <returns>执行结果。</returns>
    public static HashSet<string> BuildPermissions(OidcClientUpsertRequest request)
    {
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var grantTypes = request.GrantTypes
            .Where(static grantType => !string.IsNullOrWhiteSpace(grantType))
            .Select(static grantType => grantType.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var hasAuthorizationCode = grantTypes.Contains(OpenIddictConstants.GrantTypes.AuthorizationCode,
            StringComparer.OrdinalIgnoreCase);
        var hasRefreshToken = grantTypes.Contains(OpenIddictConstants.GrantTypes.RefreshToken,
            StringComparer.OrdinalIgnoreCase);
        var hasClientCredentials = grantTypes.Contains(OpenIddictConstants.GrantTypes.ClientCredentials,
            StringComparer.OrdinalIgnoreCase);

        if (hasAuthorizationCode)
        {
            permissions.Add(OpenIddictConstants.Permissions.Endpoints.Authorization);
            permissions.Add(OpenIddictConstants.Permissions.ResponseTypes.Code);
        }

        if (hasAuthorizationCode || hasRefreshToken || hasClientCredentials)
        {
            permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
        }

        if (hasRefreshToken)
        {
            permissions.Add(OpenIddictConstants.Permissions.Endpoints.Revocation);
        }

        if (request.PostLogoutRedirectUris.Any(static uri => !string.IsNullOrWhiteSpace(uri)))
        {
            permissions.Add(OpenIddictConstants.Permissions.Endpoints.EndSession);
        }

        foreach (var grantType in grantTypes)
        {
            permissions.Add(OpenIddictConstants.Permissions.Prefixes.GrantType + grantType);
        }

        foreach (var scope in request.Scopes
                     .Where(static scope => !string.IsNullOrWhiteSpace(scope))
                     .Select(static scope => scope.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            permissions.Add(OpenIddictConstants.Permissions.Prefixes.Scope + scope);
        }

        return permissions;
    }

    /// <summary>
    /// 生成结果。
    /// </summary>
    /// <returns>执行结果。</returns>
    private static string GenerateClientSecret()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    }

    /// <summary>
    /// 执行TryValidateAbsoluteUris。
    /// </summary>
    /// <param name="uris">参数uris。</param>
    /// <param name="error">参数error。</param>
    /// <returns>执行结果。</returns>
    private static bool TryValidateAbsoluteUris(IEnumerable<string> uris, out string? error)
    {
        foreach (var uri in uris.Where(static uri => !string.IsNullOrWhiteSpace(uri)).Select(static uri => uri.Trim()))
        {
            if (!Uri.TryCreate(uri, UriKind.Absolute, out _))
            {
                error = $"Invalid URI: '{uri}'.";
                return false;
            }
        }

        error = null;
        return true;
    }
}
