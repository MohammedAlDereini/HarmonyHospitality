using Harmony.Core.Notification.Abstractions;
using Harmony.Core.Notification.Models;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Infrastructure.Mail;

/// <summary>
/// The account mails, as plain text through the framework's mail client (SMTP today; the provider is a setting).
/// Plain text on purpose: nothing to inject, renders everywhere, and the link is the only thing that matters.
/// The token never goes to the log; the subject and the account id do.
/// </summary>
public sealed class AccountMailer : IAccountMailer
{
    private readonly IEmailClient emailClient;
    private readonly INotificationSettings notificationSettings;
    private readonly WebLinkSettings links;
    private readonly ILogger<AccountMailer> logger;

    public AccountMailer(IEmailClient emailClient, INotificationSettings notificationSettings, IOptions<WebLinkSettings> links, ILogger<AccountMailer> logger)
    {
        this.emailClient = emailClient;
        this.notificationSettings = notificationSettings;
        this.links = links.Value;
        this.logger = logger;
    }

    public Task SendPasswordResetAsync(User user, string token, CancellationToken cancellationToken)
    {
        var link = WebLinkSettings.Fill(this.links.ResetPasswordUrl, user.Email!, token);
        var body =
            $"Hello {user.DisplayName},\n\n" +
            "Someone asked to reset the password of your Harmony account. If it was you, open this link within 24 hours:\n" +
            $"{link}\n\n" +
            "If it was not you, ignore this message. Nothing changes until the link is used.\n";

        return this.SendAsync(user, "Reset your Harmony password", body, cancellationToken);
    }

    public Task SendInvitationAsync(User user, string token, CancellationToken cancellationToken)
    {
        var link = WebLinkSettings.Fill(this.links.InvitationUrl, user.Email!, token);
        var body =
            $"Hello {user.DisplayName},\n\n" +
            "An account was created for you on Harmony. Choose your password with this link within 24 hours:\n" +
            $"{link}\n\n" +
            "Nobody else knows or chooses this password.\n";

        return this.SendAsync(user, "Welcome to Harmony: set your password", body, cancellationToken);
    }

    public Task SendTwoStepSetupAsync(User user, string link, CancellationToken cancellationToken)
    {
        var body =
            $"Hello {user.DisplayName},\n\n" +
            "You just signed in to Harmony. Every account needs two-step sign-in, so set it up now.\n" +
            "Open this link on the same computer, within 15 minutes:\n" +
            $"{link}\n\n" +
            "If you did not just sign in, someone knows your password. Do not open the link: change your password and tell your administrator.\n";

        return this.SendAsync(user, "Set up two-step sign-in for Harmony", body, cancellationToken);
    }

    public Task SendSignInLinkAsync(User user, string link, CancellationToken cancellationToken)
    {
        var body =
            $"Hello {user.DisplayName},\n\n" +
            "You are signing in to Harmony. Open this link on the same computer, within 10 minutes, to finish:\n" +
            $"{link}\n\n" +
            "If you did not just sign in, someone knows your password. Do not open the link: change your password and tell your administrator.\n";

        return this.SendAsync(user, "Your Harmony sign-in link", body, cancellationToken);
    }

    public async Task SendSecurityAlertAsync(User user, SecurityAlert alert, CancellationToken cancellationToken)
    {
        var (subject, what) = alert switch
        {
            SecurityAlert.TwoStepTurnedOn => ("Two-step sign-in was turned on", "Two-step sign-in was just turned on for your account."),
            SecurityAlert.TwoStepTurnedOff => ("Two-step sign-in was turned off", "Two-step sign-in was just turned off for your account."),
            SecurityAlert.TwoStepResetByAdmin => ("Two-step sign-in was reset", "An administrator just reset two-step sign-in for your account. You will set it up again at your next sign-in."),
            SecurityAlert.PasswordChanged => ("Your password was changed", "The password of your account was just changed."),
            SecurityAlert.PasswordSetByLink => ("Your password was set", "The password of your account was just set with a link from an e-mail."),
            SecurityAlert.BackupCodeUsed => ("A backup code was used", "One of your backup codes was just used to sign in. Each code works once."),
            _ => throw new ArgumentOutOfRangeException(nameof(alert)),
        };

        var body =
            $"Hello {user.DisplayName},\n\n" +
            $"{what}\n\n" +
            "If it was you, there is nothing to do.\n" +
            "If it was not you, change your password now and tell your administrator.\n";

        try
        {
            await this.SendAsync(user, "Harmony security: " + subject.ToLowerInvariant(), body, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The change already happened and stands; a lost alert is an operator problem, not an error for the person.
            this.logger.LogError(exception, "Security alert {Alert} for account {UserId} could not be sent.", alert, user.Id);
        }
    }

    private async Task SendAsync(User user, string subject, string body, CancellationToken cancellationToken)
    {
        var message = new NotificationEmailMessage
        {
            From = this.notificationSettings.EmailClient.FromAddress,
            Tos = [user.Email!],
            Subject = subject,
            Body = body,
            IsHtmlContent = false,
        };

        var response = await this.emailClient.SendAsync(message, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"The mail client refused '{subject}' ({response.StatusCode}).");
        }

        this.logger.LogInformation("Mail '{Subject}' sent to account {UserId}.", subject, user.Id);
    }
}
