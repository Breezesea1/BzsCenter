using Microsoft.Extensions.Configuration;

namespace BzsCenter.Idp.E2ETests.Infrastructure;

internal static class TestCredentials
{
    private static readonly IConfigurationRoot Configuration = BuildConfiguration();

    public static string AdminUserName => GetRequiredValue("Identity:Admin:UserName");

    public static string AdminPassword => GetRequiredValue("Identity:Admin:Password");

    private static IConfigurationRoot BuildConfiguration()
    {
        return new ConfigurationBuilder()
            .SetBasePath(Projects.BzsCenter_AppHost.ProjectPath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static string GetRequiredValue(string key)
    {
        return Configuration[key]
            ?? throw new InvalidOperationException($"Missing required test credential setting '{key}'.");
    }
}
