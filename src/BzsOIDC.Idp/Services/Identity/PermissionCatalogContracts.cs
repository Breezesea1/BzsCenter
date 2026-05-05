namespace BzsOIDC.Idp.Services.Identity;

public sealed record PermissionCatalogSeedResource
{
    public string ResourceKey { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public PermissionCatalogSeedPermission[] Permissions { get; init; } = [];
}

public sealed record PermissionCatalogSeedPermission
{
    public string Name { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string[] ReleaseScopes { get; init; } = [];
}

public sealed record ProtectedResourceUpsertRequest
{
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record PermissionDefinitionUpsertRequest
{
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record PermissionReleaseScopesUpsertRequest
{
    public string[] Scopes { get; init; } = [];
}

public sealed record ProtectedResourceResponse
{
    public string Key { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public PermissionDefinitionResponse[] Permissions { get; init; } = [];
}

public sealed record PermissionDefinitionResponse
{
    public string ResourceKey { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsActive { get; init; }
    public string[] ReleaseScopes { get; init; } = [];
    public RolePermissionAssignmentResponse[] AssignedRoles { get; init; } = [];
}

public sealed record RolePermissionAssignmentResponse
{
    public Guid RoleId { get; init; }
    public string RoleName { get; init; } = string.Empty;
    public bool Assigned { get; init; }
}

public enum PermissionCatalogCommandStatus
{
    Success,
    ValidationFailed,
    NotFound,
    Conflict,
}

public sealed record PermissionCatalogCommandResult<T>
{
    public PermissionCatalogCommandStatus Status { get; init; }
    public T? Value { get; init; }
    public string[] Errors { get; init; } = [];

    public static PermissionCatalogCommandResult<T> Success(T value)
    {
        return new PermissionCatalogCommandResult<T>
        {
            Status = PermissionCatalogCommandStatus.Success,
            Value = value,
        };
    }

    public static PermissionCatalogCommandResult<T> Failure(PermissionCatalogCommandStatus status, params string[] errors)
    {
        return new PermissionCatalogCommandResult<T>
        {
            Status = status,
            Errors = errors,
        };
    }
}
