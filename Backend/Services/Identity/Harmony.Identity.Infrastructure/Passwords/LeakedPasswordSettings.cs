namespace Harmony.Identity.Infrastructure.Passwords;

/// <summary>
/// IdentitySettings:LeakedPasswords, optional: the defaults are Have I Been Pwned's free public range API and a
/// 3-second wait. Checked at startup.
/// </summary>
public sealed class LeakedPasswordSettings
{
    public const string Section = "IdentitySettings:LeakedPasswords";

    /// <summary>The range address; the first 5 characters of the password's SHA-1 are added to it.</summary>
    public string RangeUrl { get; set; } = "https://api.pwnedpasswords.com/range/";

    /// <summary>How long a password change waits for the answer before going on without it.</summary>
    public int TimeoutSeconds { get; set; } = 3;

    /// <summary>An absolute https address ending in '/' (plain http only on this machine, for tests), and 1 to 30 seconds.</summary>
    public static bool IsValid(LeakedPasswordSettings settings)
        => Uri.TryCreate(settings.RangeUrl, UriKind.Absolute, out var url)
            && settings.RangeUrl.EndsWith('/')
            && (url.Scheme == Uri.UriSchemeHttps || (url.Scheme == Uri.UriSchemeHttp && url.IsLoopback))
            && settings.TimeoutSeconds is >= 1 and <= 30;
}
