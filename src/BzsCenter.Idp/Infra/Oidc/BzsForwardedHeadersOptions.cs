namespace BzsCenter.Idp.Infra.Oidc;

public class BzsForwardedHeadersOptions
{
    public string[] KnownProxies { get; set; } = [];
    public string[] KnownIpNetworks { get; set; } = [];
}