namespace BzsOIDC.Idp.Services.Authorization;

public sealed class PermissionPolicyOptions
{
    public const string DefaultPolicyPrefix = "perm:";
    public string PolicyPrefix { get; init; } = DefaultPolicyPrefix;
}
