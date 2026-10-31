namespace Harmony.Identity.Infrastructure.IdentityServer.SignIn;

/// <summary>
/// The optional words and links on the console's sign-in frame (the platform-only client's pages): a contact line for the
/// platform team, and the help, privacy and terms addresses. An empty value leaves that line out; nothing is invented.
/// </summary>
public class SignInPagesSettings
{
    public const string Section = "IdentitySettings:SignInPages";

    /// <summary>How platform people reach the on-call, as one line: "Platform on-call: ext. 4400 · #platform-ops".</summary>
    public string PlatformContact { get; set; } = string.Empty;

    /// <summary>Where "Help" and "Can't sign in?" lead; https.</summary>
    public string HelpUrl { get; set; } = string.Empty;

    public string PrivacyUrl { get; set; } = string.Empty;

    public string TermsUrl { get; set; } = string.Empty;
}
