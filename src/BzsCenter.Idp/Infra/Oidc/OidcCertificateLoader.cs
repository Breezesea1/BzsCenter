using System.Security.Cryptography.X509Certificates;

namespace BzsCenter.Idp.Infra.Oidc;

internal static class OidcCertificateLoader
{
    internal static (string[] paths, string[] passwords, string optionName) ResolveSigningCertificateConfig(
        OidcOptions oidcOptions)
    {
        if (oidcOptions.SigningCertificatePath.Length > 0)
        {
            return (
                oidcOptions.SigningCertificatePath,
                oidcOptions.SigningCertificatePassword,
                "Oidc:SigningCertificatePath");
        }

        return (
            oidcOptions.SigninCertificatePath,
            oidcOptions.SigninCertificatePassword,
            "Oidc:SigninCertificatePath");
    }

    internal static IReadOnlyList<X509Certificate2> LoadCertificates(
        string[] certificatePaths,
        string[] certificatePasswords,
        string optionName)
    {
        if (certificatePaths.Length == 0)
        {
            throw new InvalidOperationException($"{optionName} must contain at least one certificate path.");
        }

        var certificates = new List<X509Certificate2>(certificatePaths.Length);

        for (var i = 0; i < certificatePaths.Length; i++)
        {
            var path = certificatePaths[i];
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new InvalidOperationException($"{optionName}[{i}] is empty.");
            }

            var absolutePath = Path.IsPathRooted(path)
                ? path
                : Path.GetFullPath(path, AppContext.BaseDirectory);

            if (!File.Exists(absolutePath))
            {
                throw new FileNotFoundException($"Certificate file was not found: {absolutePath}", absolutePath);
            }

            var password = i < certificatePasswords.Length ? certificatePasswords[i] : null;

            var extension = Path.GetExtension(absolutePath);
            if (!extension.Equals(".pfx", StringComparison.OrdinalIgnoreCase) &&
                !extension.Equals(".p12", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Certificate '{absolutePath}' must be PKCS#12 (.pfx/.p12) for this loader.");
            }

            var certificate = X509CertificateLoader.LoadPkcs12FromFile(
                absolutePath,
                string.IsNullOrWhiteSpace(password) ? null : password,
                X509KeyStorageFlags.MachineKeySet | X509KeyStorageFlags.EphemeralKeySet);

            if (!certificate.HasPrivateKey)
            {
                throw new InvalidOperationException(
                    $"Certificate '{absolutePath}' does not contain a private key.");
            }

            certificates.Add(certificate);
        }

        return certificates
            .OrderByDescending(c => c.NotBefore)
            .ToArray();
    }
}
