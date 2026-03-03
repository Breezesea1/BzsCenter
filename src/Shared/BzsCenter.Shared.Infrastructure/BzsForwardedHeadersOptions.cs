namespace BzsCenter.Shared.Infrastructure;

public class BzsForwardedHeadersOptions
{
    public string[] KnownProxies { get; set; } = [];
    public string[] KnownIpNetworks { get; set; } = [];
}