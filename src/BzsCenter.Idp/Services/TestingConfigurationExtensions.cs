namespace BzsCenter.Idp.Services;

internal static class TestingConfigurationExtensions
{
    internal static bool IsSmokeTestingEnabled(this IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return string.Equals(
            configuration["Testing:Smoke:Enabled"],
            bool.TrueString,
            StringComparison.OrdinalIgnoreCase);
    }
}
