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
