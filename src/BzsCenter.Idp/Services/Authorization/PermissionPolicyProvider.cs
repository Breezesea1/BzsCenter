using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace BzsCenter.Idp.Services.Authorization;

public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> authorizationOptions,
    IOptions<PermissionPolicyOptions> permissionOptions) : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallbackProvider = new(authorizationOptions);

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var configuredPrefix = permissionOptions.Value.PolicyPrefix;
        var prefixes = new[]
        {
            configuredPrefix,
            PermissionPolicyOptions.DefaultPolicyPrefix,
        }
            .Where(static prefix => !string.IsNullOrWhiteSpace(prefix))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var prefix in prefixes)
        {
            if (!policyName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var permission = policyName[prefix.Length..].Trim();
            if (string.IsNullOrWhiteSpace(permission))
            {
                continue;
            }

            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();

            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallbackProvider.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return _fallbackProvider.GetDefaultPolicyAsync();
    }

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return _fallbackProvider.GetFallbackPolicyAsync();
    }
}
