namespace Harmony.Identity.Infrastructure.BackgroundServices.DataRetention;

/// <summary>
/// IdentitySettings:DataRetention, optional: how long the service keeps what it writes about people. The trail floor
/// is the card rules' twelve months (PCI DSS 10.5.1: at least a year of sign-in and admin history); devices follow
/// the common stale-device practice (Microsoft's cleanup band is 30-270 days), with the ceiling where the browser has
/// lost the device cookie anyway (400 days). Checked at startup.
/// </summary>
public sealed class DataRetentionSettings
{
    public const string Section = "IdentitySettings:DataRetention";

    /// <summary>Whether the nightly cleanup runs at all. Off only for a copy that must not delete (a test, a frozen archive).</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Months of security trail kept: 12 (the legal floor) to 120.</summary>
    public int SecurityTrailMonths { get; set; } = 12;

    /// <summary>Days a known device survives unseen: 30 to 400. Forgotten means the next sign-in from it warns again.</summary>
    public int UnseenDeviceDays { get; set; } = 180;

    /// <summary>The UTC hour of the nightly cleanup, 0 to 23: a quiet hour, never a check-out rush.</summary>
    public int CleanupHourUtc { get; set; } = 3;

    /// <summary>Seconds after start before the first cleanup, 5 to 3600: early enough that a broken one fails on day one.</summary>
    public int StartDelaySeconds { get; set; } = 60;

    /// <summary>Rows deleted per statement, 100 to 50000: small enough never to hold a table, big enough to finish.</summary>
    public int DeleteBatchSize { get; set; } = 1000;

    public static bool IsValid(DataRetentionSettings settings)
        => settings.SecurityTrailMonths is >= 12 and <= 120
            && settings.UnseenDeviceDays is >= 30 and <= 400
            && settings.CleanupHourUtc is >= 0 and <= 23
            && settings.StartDelaySeconds is >= 5 and <= 3600
            && settings.DeleteBatchSize is >= 100 and <= 50000;
}
