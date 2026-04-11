namespace BzsOIDC.Idp.Infra.Oidc;

public class DataProtectionOptions
{
    public required string ApplicationName { get; init; }
    public required string StorageDirectory { get; init; }
    public double KeyLifetimeDays { get; init; } = 90;
}