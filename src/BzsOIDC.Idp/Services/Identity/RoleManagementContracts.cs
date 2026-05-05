namespace BzsOIDC.Idp.Services.Identity;

public sealed record RoleResponse
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string NormalizedName { get; init; } = string.Empty;
    public bool IsProtected { get; init; }
    public int PermissionCount { get; init; }
    public string[] Permissions { get; init; } = [];
}

public sealed record RoleUpsertRequest
{
    public string Name { get; init; } = string.Empty;
}

public sealed record RolePermissionSyncRequest
{
    public string[] Permissions { get; init; } = [];
}

public enum RoleManagementCommandStatus
{
    Success,
    ValidationFailed,
    NotFound,
    Conflict,
    Protected,
}

public sealed record RoleManagementCommandResult<T>
{
    public RoleManagementCommandStatus Status { get; init; }
    public T? Value { get; init; }
    public string[] Errors { get; init; } = [];

    public static RoleManagementCommandResult<T> Success(T value)
    {
        return new RoleManagementCommandResult<T>
        {
            Status = RoleManagementCommandStatus.Success,
            Value = value,
        };
    }

    public static RoleManagementCommandResult<T> Failure(RoleManagementCommandStatus status, params string[] errors)
    {
        return new RoleManagementCommandResult<T>
        {
            Status = status,
            Errors = errors,
        };
    }
}
