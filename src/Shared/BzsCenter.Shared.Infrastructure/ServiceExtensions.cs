using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace BzsCenter.Shared.Infrastructure;

public static class ServiceExtensions
{
    // ServiceCollection 
    extension(IServiceCollection sc)
    {
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
            sc.AddOptions<BzsForwardedHeadersOptions>().Bind(config.GetSection("ForwardedHeaders"));

            sc.AddOptions<ForwardedHeadersOptions>()
                .Configure((ForwardedHeadersOptions o, IOptions<BzsForwardedHeadersOptions> headersOptions) =>
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
    }
}