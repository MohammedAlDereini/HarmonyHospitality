using System.Buffers.Text;
using System.Globalization;
using System.Security.Claims;
using System.Security.Cryptography;
using Duende.IdentityServer;
using Duende.IdentityServer.Services;
using Harmony.Core.Identity.Implementations.Platform;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Handler.Commands.UserAccountModule.SecurityTrail;
using Harmony.Identity.Infrastructure.IdentityServer;
using Harmony.Identity.Infrastructure.IdentityServer.SignIn;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Harmony.Identity.Api.Pages.Account;

/// <summary>
/// What the sign-in pages do once a person has proved who they are, and the short pause between the password and the
/// second step. Both live in cookies the browser cannot read from script and sends only to this host over HTTPS.
/// </summary>
internal static class SignInSession
{
    /// <summary>Holds the two-step challenge between the password page and the code page (5 minutes, like the challenge).</summary>
    public const string SecondStepCookie = "__Host-harmony-two-step";

    /// <summary>Marks this browser as a device (4b): a random key, kept 400 days (the longest a browser allows). Only its hash is stored.</summary>
    public const string DeviceCookie = "__Host-harmony-device";

    /// <summary>
    /// How a passkey sign-in is named in the token's amr (RFC 8176): "swk", proof of possession of a key (the attestation
    /// is not verified, so a hardware key is never claimed), and "mfa", because the person also verified themselves on the
    /// device (NIST SP 800-63B-4: a multi-factor cryptographic authenticator). Joined with a comma where one string is kept.
    /// </summary>
    public static readonly string[] PasskeyMethods = ["swk", "mfa"];

    /// <summary>
    /// Signs the person in at Identity (Duende's cookie, set up in 1.3). The cookie carries the SecurityVersion the person
    /// signed in with, so a later change (password, roles, suspension) can end this session. The methods say how: "pwd",
    /// "mfa", or the passkey pair. Returns the sign-in session Duende opened and this browser's device key, for the page's
    /// RecordSignInCommand.
    /// </summary>
    public static async Task<SignedIn> SignInAsync(HttpContext http, User user, params string[] methods)
    {
        var person = new IdentityServerUser(user.Id.ToString("D"))
        {
            DisplayName = user.DisplayName,
            AdditionalClaims = { new Claim(PlatformClaimTypes.SecurityVersion, user.SecurityVersion.ToString(CultureInfo.InvariantCulture)) },
        };
        foreach (var method in methods)
        {
            person.AuthenticationMethods.Add(method);
        }

        // Not persistent: the cookie ends with the browser, and after 30 idle minutes in any case.
        await http.SignInAsync(person, new AuthenticationProperties { IsPersistent = false });

        var session = await http.RequestServices.GetRequiredService<IUserSession>().GetSessionIdAsync(http.RequestAborted);
        return new SignedIn(session, EnsureDeviceKey(http));
    }

    /// <summary>
    /// This browser's device key: the one in its cookie, or a new random one, set in the cookie now. The login page calls
    /// this when it OPENS, so a send that reaches the server twice carries one key, and the device is remembered once.
    /// </summary>
    internal static string EnsureDeviceKey(HttpContext http)
    {
        var key = http.Request.Cookies[DeviceCookie];
        if (key is { Length: RecordSignInCommandHandler.DeviceKeyLength })
        {
            return key;
        }

        key = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        http.Response.Cookies.Append(DeviceCookie, key, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/",
            IsEssential = true,
            MaxAge = TimeSpan.FromDays(400),
        });
        return key;
    }

    public static void HoldSecondStep(HttpResponse response, string challenge)
        => response.Cookies.Append(SecondStepCookie, challenge, SecondStepOptions(MfaChallengeStore.Lifetime));

    public static string? SecondStep(HttpRequest request) => request.Cookies[SecondStepCookie];

    public static void ReleaseSecondStep(HttpResponse response)
        => response.Cookies.Delete(SecondStepCookie, SecondStepOptions(null));

    /// <summary>
    /// Ties a two-step setup to this browser (15 minutes): the e-mailed link works only where this cookie is.
    /// Lax, not Strict: the link is clicked in a mail program or webmail, another site, and Strict would not be sent.
    /// </summary>
    public const string SetupCookie = "__Host-harmony-two-step-setup";

    public static void HoldSetup(HttpResponse response, string browser)
        => response.Cookies.Append(SetupCookie, browser, SetupOptions(TwoStepSetup.Lifetime));

    public static string? Setup(HttpRequest request) => request.Cookies[SetupCookie];

    public static void ReleaseSetup(HttpResponse response)
        => response.Cookies.Delete(SetupCookie, SetupOptions(null));

    /// <summary>Ties an e-mailed sign-in link to this browser (10 minutes). Lax for the same reason as the setup cookie.</summary>
    public const string EmailLinkCookie = "__Host-harmony-email-sign-in";

    public static void HoldEmailLink(HttpResponse response, string browser)
        => response.Cookies.Append(EmailLinkCookie, browser, SetupOptions(EmailSignIn.Lifetime));

    public static string? EmailLink(HttpRequest request) => request.Cookies[EmailLinkCookie];

    public static void ReleaseEmailLink(HttpResponse response)
        => response.Cookies.Delete(EmailLinkCookie, SetupOptions(null));

    /// <summary>Holds the passkey challenge between the request for it and the browser's signed answer (5 minutes, like the challenge).</summary>
    public const string PasskeyCookie = "__Host-harmony-passkey";

    public static void HoldPasskey(HttpResponse response, string secret)
        => response.Cookies.Append(PasskeyCookie, secret, SecondStepOptions(PasskeySignIn.Lifetime));

    public static string? Passkey(HttpRequest request) => request.Cookies[PasskeyCookie];

    public static void ReleasePasskey(HttpResponse response)
        => response.Cookies.Delete(PasskeyCookie, SecondStepOptions(null));

    /// <summary>Holds a proved sign-in while the person replaces a password somebody else chose (10 minutes, like the hold).</summary>
    public const string PasswordChangeCookie = "__Host-harmony-password-change";

    public static void HoldPasswordChange(HttpResponse response, string secret)
        => response.Cookies.Append(PasswordChangeCookie, secret, SecondStepOptions(ForcedPasswordChange.Lifetime));

    public static string? PasswordChange(HttpRequest request) => request.Cookies[PasswordChangeCookie];

    public static void ReleasePasswordChange(HttpResponse response)
        => response.Cookies.Delete(PasswordChangeCookie, SecondStepOptions(null));

    /// <summary>
    /// The same pause from every way a person proves who they are: the sign-in goes on hold with how it was proved and
    /// the trail line it will write, and the change page finishes it. Nobody is signed in until the password is theirs.
    /// </summary>
    public static async Task<IActionResult> HoldForPasswordChangeAsync(PageModel page, ForcedPasswordChange forced, User user, string method, string trailAction, string? returnUrl, string? detail = null)
    {
        var secret = await forced.HoldAsync(user, method, trailAction, page.HttpContext.RequestAborted, detail);
        HoldPasswordChange(page.Response, secret);
        return page.RedirectToPage("ChangePassword", new { returnUrl });
    }

    private static CookieOptions SecondStepOptions(TimeSpan? maxAge) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        IsEssential = true,
        MaxAge = maxAge,
    };

    private static CookieOptions SetupOptions(TimeSpan? maxAge) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        IsEssential = true,
        MaxAge = maxAge,
    };
}

/// <summary>What a page sign-in opened: Duende's sign-in session (null if Duende gave none) and this browser's device key.</summary>
internal sealed record SignedIn(string? SessionId, string DeviceKey);
