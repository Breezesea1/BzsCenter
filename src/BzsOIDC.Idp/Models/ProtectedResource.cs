namespace BzsOIDC.Idp.Models;

public sealed class ProtectedResource
{
    private readonly List<PermissionDefinition> _permissions = [];

    private ProtectedResource()
    {
    }

    private ProtectedResource(string key, string displayName, string? description)
    {
        Key = NormalizeKey(key);
        DisplayName = NormalizeDisplayName(displayName, Key);
        Description = NormalizeOptional(description);
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public string Key { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    public IReadOnlyCollection<PermissionDefinition> Permissions => _permissions;

    public static ProtectedResource Create(string key, string? displayName = null, string? description = null)
    {
        return new ProtectedResource(key, displayName ?? key, description);
    }

    public void Update(string displayName, string? description, bool isActive)
    {
        DisplayName = NormalizeDisplayName(displayName, Key);
        Description = NormalizeOptional(description);
        IsActive = isActive;
    }

    public PermissionDefinition AddPermission(string name, string? displayName = null, string? description = null)
    {
        var normalizedName = PermissionDefinition.NormalizeName(name);
        if (_permissions.Any(permission => string.Equals(permission.Name, normalizedName, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"Permission '{normalizedName}' already exists on resource '{Key}'.");
        }

        var permission = PermissionDefinition.Create(this, normalizedName, displayName, description);
        _permissions.Add(permission);
        return permission;
    }

    public static string NormalizeKey(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        return key.Trim().ToLowerInvariant();
    }

    private static string NormalizeDisplayName(string? displayName, string fallback)
    {
        return string.IsNullOrWhiteSpace(displayName) ? fallback : displayName.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
