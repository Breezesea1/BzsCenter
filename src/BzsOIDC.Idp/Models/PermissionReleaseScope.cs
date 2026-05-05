namespace BzsOIDC.Idp.Models;

public sealed class PermissionReleaseScope
{
    private PermissionReleaseScope()
    {
    }

    private PermissionReleaseScope(PermissionDefinition permission, string scope)
    {
        ArgumentNullException.ThrowIfNull(permission);

        Permission = permission;
        PermissionDefinitionId = permission.Id;
        Scope = NormalizeScope(scope);
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid PermissionDefinitionId { get; private set; }
    public PermissionDefinition Permission { get; private set; } = null!;
    public string Scope { get; private set; } = string.Empty;

    public static PermissionReleaseScope Create(PermissionDefinition permission, string scope)
    {
        return new PermissionReleaseScope(permission, scope);
    }

    public static string NormalizeScope(string scope)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(scope);
        return scope.Trim().ToLowerInvariant();
    }
}
