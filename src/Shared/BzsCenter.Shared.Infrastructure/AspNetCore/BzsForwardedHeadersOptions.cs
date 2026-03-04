namespace BzsCenter.Shared.Infrastructure.AspNetCore;

public class BzsForwardedHeadersOptions
{
    public string[] KnownProxies { get; set; } = [];
    public string[] KnownIpNetworks { get; set; } = [];
}