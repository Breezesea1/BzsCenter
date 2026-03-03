namespace BzsCenter.Shared.Infrastructure;

public class ForwardedHeadersOptions
{
    public string[] KnownProxies { get; set; } = [];
    public string[] KnownIpNetworks { get; set; } = [];
}