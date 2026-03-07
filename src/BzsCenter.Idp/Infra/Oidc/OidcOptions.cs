namespace BzsCenter.Idp.Infra.Oidc;

public class OidcOptions
{
    public string[] EncryptionCertificatePath { get; init; } = [];
    public string[] EncryptionCertificatePassword { get; init; } = [];
    public string[] SigninCertificatePath { get; init; } = [];
    public string[] SigninCertificatePassword { get; init; } = [];

    public string[] SigningCertificatePath { get; init; } = [];
    public string[] SigningCertificatePassword { get; init; } = [];

    public int AccessTokenLifetimeMinutes { get; init; } = 45;
    public int RefreshTokenLifetimeDays { get; init; } = 7;
    public bool DisableAccessTokenEncryption { get; init; } = true;
}
