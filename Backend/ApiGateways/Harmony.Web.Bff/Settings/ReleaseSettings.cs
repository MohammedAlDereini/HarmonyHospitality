namespace Harmony.Web.Bff.Settings;

using System.Reflection;

/// <summary>
/// What the web app learns about this deployment (D33, D37, D38): the environment and release it talks to, the oldest
/// build it may keep running, the release ring this host serves, and the feature flags with the rings each is on for.
/// Rollback of a feature is one setting: Enabled false, or the ring taken off its list.
/// </summary>
public sealed class ReleaseSettings
{
    public const string Section = "Release";
    public static readonly string[] Rings = ["canary", "early", "all"];

    /// <summary>The ring this host belongs to: canary (the platform's own people), early (a few hotels), all.</summary>
    public string Ring { get; set; } = "all";

    /// <summary>The release served, as the app shows it and sends it in telemetry; the assembly's own version when empty.</summary>
    public string Version { get; set; } = string.Empty;

    /// <summary>The oldest web-app build allowed to keep running; an older build reloads itself. Empty: no floor.</summary>
    public string MinimumClientVersion { get; set; } = string.Empty;

    /// <summary>Feature flags by name: on for the rings listed, or for every ring when none is listed.</summary>
    public Dictionary<string, FlagSetting> Flags { get; set; } = new(StringComparer.Ordinal);

    /// <summary>The floor as the app reads it: "0" means no floor (the app refuses an empty field, fail fast).</summary>
    public string EffectiveMinimumClientVersion
        => string.IsNullOrWhiteSpace(this.MinimumClientVersion) ? "0" : this.MinimumClientVersion.Trim();

    public string EffectiveVersion
        => string.IsNullOrWhiteSpace(this.Version)
            ? typeof(ReleaseSettings).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0"
            : this.Version;

    /// <summary>Whether a flag is on for this host's ring: enabled, and the ring listed or no ring listed at all.</summary>
    public bool IsOn(FlagSetting flag)
        => flag.Enabled && (flag.Rings.Length == 0 || flag.Rings.Contains(this.Ring, StringComparer.Ordinal));

    public static bool IsValid(ReleaseSettings settings)
        => Rings.Contains(settings.Ring, StringComparer.Ordinal)
            && settings.Flags.Keys.All(name => System.Text.RegularExpressions.Regex.IsMatch(name, "^[a-z][a-z0-9-]{1,63}$"))
            && settings.Flags.Values.All(flag => flag.Rings.All(ring => Rings.Contains(ring, StringComparer.Ordinal)));
}

/// <summary>One feature flag: on or off, and for which rings.</summary>
public sealed class FlagSetting
{
    public bool Enabled { get; set; }

    public string[] Rings { get; set; } = [];
}
