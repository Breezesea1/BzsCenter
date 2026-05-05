namespace BzsOIDC.Idp.Models;

public sealed class PermissionDefinition
{
    private readonly List<PermissionReleaseScope> _releaseScopes = [];

    private PermissionDefinition()
    {
    }

    private PermissionDefinition(ProtectedResource resource, string name, string? displayName, string? description)
    {
        ArgumentNullException.ThrowIfNull(resource);

        Resource = resource;
        ResourceId = resource.Id;
        Name = NormalizeName(name);
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? Name : displayName.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid ResourceId { get; private set; }
    public ProtectedResource Resource { get; private set; } = null!;
    public string Name { get; private set; } = string.Empty;
    public string DisplayName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;
    public IReadOnlyCollection<PermissionReleaseScope> ReleaseScopes => _releaseScopes;

    public static PermissionDefinition Create(ProtectedResource resource, string name, string? displayName = null, string? description = null)
    {
        return new PermissionDefinition(resource, name, displayName, description);
    }

    public void Update(string? displayName, string? description, bool isActive)
    {
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? Name : displayName.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        IsActive = isActive;
    }

    public void SyncReleaseScopes(IEnumerable<string> scopes)
    {
        var targetScopes = scopes
            .Where(static scope => !string.IsNullOrWhiteSpace(scope))
            .Select(PermissionReleaseScope.NormalizeScope)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (targetScopes.Length == 0)
        {
            throw new ArgumentException("At least one release scope is required.", nameof(scopes));
        }

        var toRemove = _releaseScopes
            .Where(scope => !targetScopes.Contains(scope.Scope, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        foreach (var scope in toRemove)
        {
            _releaseScopes.Remove(scope);
        }

        var existingScopes = _releaseScopes
            .Select(static scope => scope.Scope)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var scope in targetScopes.Where(scope => !existingScopes.Contains(scope)))
        {
            _releaseScopes.Add(PermissionReleaseScope.Create(this, scope));
        }
    }

    public static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return name.Trim().ToLowerInvariant();
    }
}
