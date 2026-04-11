namespace BzsOIDC.Idp.Services.Oidc;

public sealed class OidcScopeUpsertRequest
{
    public string? Name { get; init; }
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string[] Resources { get; init; } = [];
}

public sealed class OidcScopeResponse
{
    public string Name { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string? Description { get; init; }
    public string[] Resources { get; init; } = [];
}

public enum OidcScopeCommandStatus
{
    Success,
    ValidationFailed,
    Conflict,
    NotFound,
}

public sealed class OidcScopeCommandResult<T>
{
    public OidcScopeCommandStatus Status { get; init; }
    public T? Value { get; init; }
    public string[] Errors { get; init; } = [];
}
