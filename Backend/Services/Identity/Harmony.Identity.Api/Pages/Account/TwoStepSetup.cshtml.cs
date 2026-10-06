using Duende.IdentityServer.Services;
using Harmony.Identity.Infrastructure.IdentityServer.SignIn;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using QRCoder;

namespace Harmony.Identity.Api.Pages.Account;

/// <summary>
/// Setting up two-step sign-in during sign-in, like Mews, but the QR code only shows after the e-mailed link was opened in
/// the browser that signed in: the password alone never reaches the secret. Then the person types one code from the app,
/// two-step is on, the ten backup codes are shown once, and the sign-in continues.
/// </summary>
[AllowAnonymous]
[SignInPageHeaders]
public sealed class TwoStepSetupModel : PageModel
{
    public enum Steps
    {
        CheckEmail,
        Scan,
        Done,
        WrongBrowser,
        Expired,
    }

    private readonly IIdentityServerInteractionService interaction;
    private readonly TwoStepSetup setup;

    public TwoStepSetupModel(IIdentityServerInteractionService interaction, TwoStepSetup setup)
    {
        this.interaction = interaction;
        this.setup = setup;
    }

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    public string? Code { get; set; }

    public Steps Step { get; private set; }

    public string? Error { get; private set; }

    /// <summary>The QR code as SVG markup, made here from the account's own otpauth address (no outside input).</summary>
    public string? QrSvg { get; private set; }

    public string? Key { get; private set; }

    public IReadOnlyList<string> RecoveryCodes { get; private set; } = [];

    /// <summary>Back to the sign-in request, only while Duende still knows it.</summary>
    public string? ContinueUrl { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? token)
    {
        var browser = SignInSession.Setup(this.Request);

        if (token is not null)
        {
            if (browser is null)
            {
                this.Step = Steps.WrongBrowser;
                return this.Page();
            }

            if (!await this.setup.OpenAsync(browser, token, this.HttpContext.RequestAborted))
            {
                this.Step = Steps.Expired;
                return this.Page();
            }

            // The link's secret leaves the address bar and the history; the rest runs on this browser's cookie.
            return this.RedirectToPage(new { this.ReturnUrl });
        }

        return await this.ShowAsync(browser);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var browser = SignInSession.Setup(this.Request);
        var result = await this.setup.EnableAsync(browser, this.Code, this.HttpContext.RequestAborted);

        if (result.Status == TwoStepSetupStatus.Enabled)
        {
            SignInSession.ReleaseSetup(this.Response);
            await SignInSession.SignInAsync(this.HttpContext, result.User!, "mfa");
            this.RecoveryCodes = result.RecoveryCodes;
            this.ContinueUrl = await this.interaction.GetAuthorizationContextAsync(this.ReturnUrl, this.HttpContext.RequestAborted) is null
                ? null
                : this.ReturnUrl;
            this.Step = Steps.Done;
            return this.Page();
        }

        if (result.Status == TwoStepSetupStatus.NotOpened)
        {
            this.Step = Steps.Expired;
            return this.Page();
        }

        this.Error = result.Status switch
        {
            TwoStepSetupStatus.LockedOut => "Too many wrong attempts. The account is locked for a few minutes.",
            TwoStepSetupStatus.AccountNotActive => "This account cannot sign in. Contact an administrator.",
            _ => "The code did not verify. Check the time on your phone and try the next code.",
        };
        return await this.ShowAsync(browser);
    }

    private async Task<IActionResult> ShowAsync(string? browser)
    {
        var user = await this.setup.OpenedAccountAsync(browser, this.HttpContext.RequestAborted);
        if (user is null)
        {
            this.Step = await this.setup.IsWaitingAsync(browser, this.HttpContext.RequestAborted) ? Steps.CheckEmail : Steps.Expired;
            return this.Page();
        }

        var (key, uri) = await this.setup.KeyAsync(user);
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(uri, QRCodeGenerator.ECCLevel.M);
        using var svg = new SvgQRCode(data);
        this.QrSvg = svg.GetGraphic(5, "#17202a", "#ffffff", true, SvgQRCode.SizingMode.ViewBoxAttribute);
        this.Key = string.Join(' ', key.Chunk(4).Select(part => new string(part)));
        this.Step = Steps.Scan;
        return this.Page();
    }
}
