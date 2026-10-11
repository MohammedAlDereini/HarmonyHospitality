namespace Harmony.Identity.Infrastructure.RateLimiting;

/// <summary>
/// IdentitySettings:RateLimits, optional: how many tries one caller address gets at the anonymous doors. The defaults fit
/// a hotel, where the whole staff shares one public address: a shift change of 30 people (password + code) fits the
/// sign-in burst, while one attacker's address is held to 20 tries a minute. Checked at startup.
/// </summary>
public sealed class RateLimitSettings
{
    public const string Section = "IdentitySettings:RateLimits";

    /// <summary>Sign-in tries (password, two-step code, two-step setup, the forced password change) one address may make at once.</summary>
    public int SignInBurst { get; set; } = 60;

    /// <summary>Sign-in tries given back to the address every minute, up to the burst.</summary>
    public int SignInPerMinute { get; set; } = 20;

    /// <summary>Forgot-password requests one address may make in the window (each account also gets at most one mail a minute).</summary>
    public int ForgotPasswordPerWindow { get; set; } = 10;

    /// <summary>
    /// The window for forgot password, in minutes, 1 to 60: it moves in one-minute steps (every wait is a minute or less),
    /// and each caller keeps one small counter per step.
    /// </summary>
    public int WindowMinutes { get; set; } = 15;

    /// <summary>
    /// New passwords set with a link's token one address may send at once. The same door takes invitations, so a hotel's
    /// onboarding day (everyone setting a first password on the hotel's network) must fit; a wrong token costs us nothing.
    /// </summary>
    public int ResetPasswordBurst { get; set; } = 30;

    /// <summary>New-password sends given back to the address every minute, up to the burst.</summary>
    public int ResetPasswordPerMinute { get; set; } = 10;

    /// <summary>Passkey calls (a challenge, an answer, a proof of identity) one signed-in person may make at once: a few registrations in a row.</summary>
    public int PasskeyBurst { get; set; } = 30;

    /// <summary>Passkey calls given back to the person every minute, up to the burst.</summary>
    public int PasskeyPerMinute { get; set; } = 10;

    /// <summary>Every number 1 or more (.NET's limiters refuse 0 too, but only at the first request); the window at most an hour.</summary>
    public static bool IsValid(RateLimitSettings settings)
        => settings.SignInBurst >= 1
            && settings.SignInPerMinute >= 1
            && settings.ForgotPasswordPerWindow >= 1
            && settings.WindowMinutes is >= 1 and <= 60
            && settings.ResetPasswordBurst >= 1
            && settings.ResetPasswordPerMinute >= 1
            && settings.PasskeyBurst >= 1
            && settings.PasskeyPerMinute >= 1;
}
