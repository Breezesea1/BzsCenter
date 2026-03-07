using BzsCenter.Idp.Infra.Oidc;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BzsCenter.Idp.UnitTests.Infra.Oidc;

public class DataProtectionServiceExtensionsTests
{
    [Fact]
    public void AddDataProtectionKeyStorage_CreatesStorageDirectoryAndConfiguresLifetime()
    {
        var storageDirectory = Path.Combine(Path.GetTempPath(), $"bzs-idp-dp-{Guid.NewGuid():N}");

        try
        {
            var services = new ServiceCollection();
            var options = new DataProtectionOptions
            {
                ApplicationName = "BzsCenter.Idp.Tests",
                StorageDirectory = storageDirectory,
                KeyLifetimeDays = 30,
            };

            services.AddDataProtectionKeyStorage(options);

            Assert.True(Directory.Exists(storageDirectory));

            using var serviceProvider = services.BuildServiceProvider();
            var keyManagementOptions = serviceProvider.GetRequiredService<IOptions<KeyManagementOptions>>().Value;
            Assert.Equal(TimeSpan.FromDays(30), keyManagementOptions.NewKeyLifetime);
        }
        finally
        {
            if (Directory.Exists(storageDirectory))
            {
                Directory.Delete(storageDirectory, recursive: true);
            }
        }
    }
}
