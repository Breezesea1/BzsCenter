using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace BzsCenter.Idp.Services.Authorization;

public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> authorizationOptions,
    IOptions<PermissionPolicyOptions> permissionOptions) : IAuthorizationPolicyProvider
{
    /// <summary>
    /// 执行new。
    /// </summary>
    /// <param name="authorizationOptions">参数authorizationOptions。</param>
    /// <returns>执行结果。</returns>
    private readonly DefaultAuthorizationPolicyProvider _fallbackProvider = new(authorizationOptions);

    /// <summary>
    /// 获取指定策略名称对应的授权策略。
    /// </summary>
    /// <param name="policyName">策略名称。</param>
    /// <returns>匹配到的授权策略。</returns>
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

    /// <summary>
    /// 获取默认授权策略。
    /// </summary>
    /// <returns>默认授权策略。</returns>
    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        return _fallbackProvider.GetDefaultPolicyAsync();
    }

    /// <summary>
    /// 获取回退授权策略。
    /// </summary>
    /// <returns>回退授权策略。</returns>
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return _fallbackProvider.GetFallbackPolicyAsync();
    }
}
