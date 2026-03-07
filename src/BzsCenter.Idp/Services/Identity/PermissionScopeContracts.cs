namespace BzsCenter.Idp.Services.Identity;

public sealed record PermissionScopeUpsertRequest
{
    public string[] Scopes { get; init; } = [];
}

public sealed record PermissionScopeResponse
{
    public string Permission { get; init; } = string.Empty;
    public string[] Scopes { get; init; } = [];
}
