namespace BzsCenter.Shared.Infrastructure.AspNetCore;

public class OidcOptions
{
    public required string[] EncryptionCertificatePath { get; init; }
    public required string[] SigninCertificatePath { get; init; }
}