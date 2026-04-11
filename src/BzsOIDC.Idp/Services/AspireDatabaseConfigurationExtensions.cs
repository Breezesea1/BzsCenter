namespace BzsOIDC.Idp.Services;

internal static class AspireDatabaseConfigurationExtensions
{
    internal static bool ShouldEnrichIdpDbFromAspire(this IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (configuration.IsSmokeTestingEnabled())
        {
            return false;
        }

        var connectionString = configuration.GetConnectionString("DefaultConnection");
        return !string.IsNullOrWhiteSpace(connectionString)
            ? !connectionString.StartsWith("Data Source=", StringComparison.OrdinalIgnoreCase)
            : true;
    }
}
