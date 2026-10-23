namespace Harmony.Identity.Infrastructure.IdentityServer.Yarp;

/// <summary>
/// The console's BFF host: the same host code on its own address, for the platform's own people. No section means no
/// console client: nothing to sign in to until the console exists. With a section, the same rules as the web host.
/// </summary>
public sealed class ConsoleBffSettings : BffClientSettings
{
    public const string Section = "IdentitySettings:ConsoleBff";

    /// <summary>The console BFF's client id at Duende.</summary>
    public const string ClientId = "harmony-console-bff";

    /// <summary>A section with at least one origin: the console client is registered and its settings are checked at startup.</summary>
    public bool IsPresent => this.Origins.Length > 0;
}
