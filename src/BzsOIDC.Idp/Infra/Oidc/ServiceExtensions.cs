using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace BzsOIDC.Idp.Infra.Oidc;

public static class ServiceExtensions
{
    private const UnixFileMode LinuxDirMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute;

    // ServiceCollection 
    extension(IServiceCollection sc)
    {
        /// <summary>
        /// 配置数据保护密钥存储，将加密密钥持久化到指定目录。
        /// </summary>
        /// <param name="options">数据保护配置选项，包含应用程序名称、存储目录和密钥有效期。</param>
        /// <remarks>
        /// 此方法用于配置 ASP.NET Core 数据保护系统，将加密密钥持久化保存到文件系统。
        /// 默认情况下，密钥存储在内存中，应用重启后会丢失，导致用户登录状态失效。
        /// 通过将密钥持久化到指定目录，可以在应用重启后继续使用之前的密钥。
        /// 
        /// <paramref name="options"/> 参数说明：
        /// - <see cref="DataProtectionOptions.ApplicationName"/>：应用程序名称，用于隔离不同应用的数据保护密钥
        /// - <see cref="DataProtectionOptions.StorageDirectory"/>：用于路径
        /// - <see cref="DataProtectionOptions.KeyLifetimeDays"/>：密钥的有效期（天数），默认为存储密钥的目录 90 天
        /// 
        /// 主要用途：
        /// - 加密身份验证 Cookie
        /// - 生成和验证防伪标记（CSRF Token）
        /// - 保护会话状态数据
        /// 
        /// 密钥过期后会自动生成新密钥。
        /// </remarks>
        public IServiceCollection AddDataProtectionKeyStorage(DataProtectionOptions options)
        {
            if (!Directory.Exists(options.StorageDirectory))
            {
                if (OperatingSystem.IsWindows())
                    Directory.CreateDirectory(options.StorageDirectory);
                else
                    Directory.CreateDirectory(options.StorageDirectory, LinuxDirMode);
            }

            sc.AddDataProtection()
                .SetApplicationName(options.ApplicationName)
                .PersistKeysToFileSystem(new DirectoryInfo(options.StorageDirectory))
                .SetDefaultKeyLifetime(TimeSpan.FromDays(options.KeyLifetimeDays));

            return sc;
        }

        /// <summary>
        /// 配置应用程序处理转发头，支持代理场景（如负载均衡器或反向代理）。
        /// </summary>
        /// <remarks>此方法启用对 X-Forwarded-For、X-Forwarded-Proto 和 X-Forwarded-Host 头的处理，
        /// 这些头通常用于在应用程序位于代理后面时保留原始请求信息。请确保在生产环境中配置受信任的代理地址，
        /// 以防止转发头被欺骗。</remarks>
        public IServiceCollection AddForwardedHeaders()
        {
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