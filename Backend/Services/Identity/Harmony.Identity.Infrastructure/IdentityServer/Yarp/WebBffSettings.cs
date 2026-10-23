namespace Harmony.Identity.Infrastructure.IdentityServer.Yarp;

/// <summary>The web app's BFF host: the one hotel staff sign in through. Its rules are the shared ones in <see cref="BffClientSettings"/>.</summary>
public class WebBffSettings : BffClientSettings
{
    public const string Section = "IdentitySettings:WebBff";

    /// <summary>The web BFF's client id at Duende.</summary>
    public const string ClientId = "harmony-web-bff";
}
