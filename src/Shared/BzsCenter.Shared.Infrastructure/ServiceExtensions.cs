using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Savvy.Shared.Infrastructure.Cache;
using Savvy.Shared.Infrastructure.Polly;
using StackExchange.Redis;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Savvy.Shared.Infrastructure.Models.Options;

namespace Savvy.Shared.Infrastructure.Extensions;

public static class ServiceExtensions
{
    private const UnixFileMode LinuxDirMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute;

    // ServiceCollection 
    extension(IServiceCollection sc)
    {
        /// <summary>
        /// Adds the Polly policy factory to the service collection for dependency injection.
        /// </summary>
        /// <remarks>Call this method to register the default implementation of the IPollyPolicyFactory
        /// interface with the service collection. This enables the use of Polly-based resilience policies throughout
        /// the application via dependency injection.</remarks>
        public void AddPoly()
        {
            sc.AddSingleton<IPollyPolicyFactory, PollyPolicyFactory>();
        }

        public IServiceCollection AddDataProtectionToDirectory(string appName, string directoryPath)
        {
            if (!Directory.Exists(directoryPath))
            {
                if (OperatingSystem.IsWindows())
                    Directory.CreateDirectory(directoryPath);
                else
                    Directory.CreateDirectory(directoryPath, LinuxDirMode);
            }

            sc.AddDataProtection()
                .SetApplicationName(appName)
                .PersistKeysToFileSystem(new DirectoryInfo(directoryPath))
                .SetDefaultKeyLifetime(TimeSpan.FromDays(90));
            return sc;
        }


        /// <summary>
        /// Configures the application to process forwarded headers, enabling support for proxy scenarios such as load
        /// balancers or reverse proxies.
        /// </summary>
        /// <remarks>This method enables handling of the X-Forwarded-For, X-Forwarded-Proto, and
        /// X-Forwarded-Host headers, which are commonly used to preserve the original request information when the
        /// application is behind a proxy. Ensure that trusted proxy addresses are configured in production environments
        /// to prevent spoofing of forwarded headers.</remarks>
        public IServiceCollection AddForwardedHeaders(IConfiguration config)
        {
            sc.AddOptions<SavvyForwardedHeadersOptions>().Bind(config.GetSection("ForwardedHeaders"));

            sc.AddOptions<ForwardedHeadersOptions>()
                .Configure((ForwardedHeadersOptions o, IOptions<SavvyForwardedHeadersOptions> headersOptions) =>
                {
                    o.ForwardedHeaders =
                        ForwardedHeaders.XForwardedProto |
                        ForwardedHeaders.XForwardedFor |
                        ForwardedHeaders.XForwardedHost;


                    // 生产环境：只信任你的反向代理 / 内网网段

                    o.KnownIPNetworks.Clear();
                    o.KnownProxies.Clear();

                    // 单个代理 IP
                    if (headersOptions.Value.KnownProxies is { Length: > 0 })
                    {
                        foreach (var ip in headersOptions.Value.KnownProxies)
                        {
                            if (IPAddress.TryParse(ip, out var addr))
                            {
                                o.KnownProxies.Add(addr);
                            }
                        }
                    }

                    // 网段 CIDR，例如 "172.20.0.0/16"
                    if (headersOptions.Value.KnownIpNetworks is { Length: > 0 })
                    {
                        foreach (var cidr in headersOptions.Value.KnownIpNetworks)
                        {
                            var parts = cidr.Split('/', StringSplitOptions.RemoveEmptyEntries);
                            if (parts.Length == 2 &&
                                IPAddress.TryParse(parts[0], out var network) &&
                                int.TryParse(parts[1], out var prefix))
                            {
                                o.KnownIPNetworks.Add(new System.Net.IPNetwork(network, prefix));
                            }
                        }
                    }

                    o.ForwardLimit = 2;
                });
            return sc;
        }


        /// <summary>
        /// Adds and configures the caching service implementation to the service collection, using either in-memory or
        /// Redis-based caching.
        /// </summary>
        /// <remarks>This method registers an implementation of ISavvyCache with logging support. When
        /// useRedis is set to true, the method expects that IDistributedCache and IConnectionMultiplexer services are
        /// already registered in the service collection. When useRedis is false, IMemoryCache is registered if not
        /// already present. Choose the caching backend based on your application's scalability and persistence
        /// requirements.</remarks>
        /// <param name="useRedis">true to use Redis as the caching backend; false to use in-memory caching.</param>
        public void AddCacheService(bool useRedis)
        {
            if (!useRedis)
            {
                sc.AddMemoryCache();
                sc.AddSingleton<ISavvyCache>(sp =>
                {
                    var memoryCache = sp.GetRequiredService<IMemoryCache>();
                    var cache = new SavvyMemoryCache(memoryCache);
                    var loggingCache = new LoggingSavvyCache(cache, sp.GetRequiredService<ILogger<ISavvyCache>>());
                    return loggingCache;
                });
            }
            else
            {
                sc.AddSingleton<ISavvyCache>(sp =>
                {
                    var redisCache = sp.GetRequiredService<IDistributedCache>();
                    var connectionMultiplexer = sp.GetRequiredService<IConnectionMultiplexer>();
                    var cache = new SavvyRedisCache(redisCache, connectionMultiplexer);
                    var loggingCache = new LoggingSavvyCache(cache, sp.GetRequiredService<ILogger<ISavvyCache>>());
                    return loggingCache;
                });
            }
        }
    }
}