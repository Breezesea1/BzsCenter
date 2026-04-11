namespace BzsOIDC.Idp.Models;

public sealed class PermissionScopeMapping
{
    private PermissionScopeMapping()
    {
    }

    private PermissionScopeMapping(string permission, string scope)
    {
        Permission = NormalizeRequiredValue(permission, nameof(permission));
        Scope = NormalizeRequiredValue(scope, nameof(scope));
    }

    public string Permission { get; private set; } = string.Empty;
    public string Scope { get; private set; } = string.Empty;

    public static PermissionScopeMapping Create(string permission, string scope)
    {
        return new PermissionScopeMapping(permission, scope);
    }

    private static string NormalizeRequiredValue(string value, string paramName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, paramName);
        return value.Trim().ToLowerInvariant();
    }
}
