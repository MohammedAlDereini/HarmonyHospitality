namespace Harmony.Identity.Infrastructure.Mail;

/// <summary>
/// WebLinkSettings — where the mails send people: the web app's pages, as URL templates. Identity never renders a page;
/// it puts {email} and {token} into these templates and the web app does the rest. Both are required and must be https.
/// </summary>
public class WebLinkSettings
{
    public const string Section = "IdentitySettings:Web";

    /// <summary>The page where a person types a new password after "forgot password". Must contain {email} and {token}.</summary>
    public string ResetPasswordUrl { get; set; } = string.Empty;

    /// <summary>The page where an invited person sets the first password. Must contain {email} and {token}.</summary>
    public string InvitationUrl { get; set; } = string.Empty;

    /// <summary>An absolute https URL carrying both placeholders. Checked at startup.</summary>
    public static bool IsValidTemplate(string? template)
        => !string.IsNullOrWhiteSpace(template)
           && template.Contains("{email}", StringComparison.Ordinal)
           && template.Contains("{token}", StringComparison.Ordinal)
           && Uri.TryCreate(template.Replace("{email}", "e").Replace("{token}", "t"), UriKind.Absolute, out var uri)
           && uri.Scheme == Uri.UriSchemeHttps;

    /// <summary>The template with both values URL-encoded in place.</summary>
    public static string Fill(string template, string email, string token)
        => template.Replace("{email}", Uri.EscapeDataString(email), StringComparison.Ordinal)
                   .Replace("{token}", Uri.EscapeDataString(token), StringComparison.Ordinal);
}
