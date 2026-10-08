using System.Net;

namespace Harmony.Identity.Infrastructure.Proxies;

/// <summary>
/// IdentitySettings:TrustedProxies, optional: the front doors allowed to tell us the caller's real address
/// (X-Forwarded-For). Empty, the default, means no proxy: the connection's own address is the caller, and the header is
/// not read at all. A header from any sender not listed is ignored, so nobody can choose their own address. Checked at startup.
/// </summary>
public sealed class TrustedProxySettings
{
    public const string Section = "IdentitySettings:TrustedProxies";

    /// <summary>Single proxy addresses, e.g. "10.0.0.5".</summary>
    public string[] Addresses { get; set; } = [];

    /// <summary>Proxy address ranges, e.g. "172.17.0.0/16" (a Docker network).</summary>
    public string[] Networks { get; set; } = [];

    /// <summary>Every entry must be a real address or range: a typo would trust the wrong sender, or nobody.</summary>
    public static bool IsValid(TrustedProxySettings settings)
        => settings.Addresses.All(address => IPAddress.TryParse(address, out _))
            && settings.Networks.All(network => IPNetwork.TryParse(network, out _));
}
