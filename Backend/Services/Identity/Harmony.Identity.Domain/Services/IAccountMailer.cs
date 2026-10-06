using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

namespace Harmony.Identity.Domain.Services;

/// <summary>
/// The mails an account can receive. The handler supplies the person and the one-time token; the mailer owns the
/// wording and the link it goes into. A refused or failed send surfaces as an exception, the caller decides what it means.
/// </summary>
public interface IAccountMailer
{
    /// <summary>"Reset your password": the link carries the token that ResetPassword accepts once.</summary>
    Task SendPasswordResetAsync(User user, string token, CancellationToken cancellationToken);

    /// <summary>"Welcome, set your password": the same kind of link, for an account created without a password.</summary>
    Task SendInvitationAsync(User user, string token, CancellationToken cancellationToken);

    /// <summary>"Set up two-step sign-in": the link opens the setup on the computer where the person just signed in.</summary>
    Task SendTwoStepSetupAsync(User user, string link, CancellationToken cancellationToken);

    /// <summary>"Your sign-in link": the second step for people who chose the e-mail link, for the computer that typed the password.</summary>
    Task SendSignInLinkAsync(User user, string link, CancellationToken cancellationToken);

    /// <summary>
    /// "Something changed on your account": two-step on, off or reset, a password changed or set, a backup code used.
    /// Never throws: the change it reports already happened and stands, so a failed send is logged for the operators.
    /// </summary>
    Task SendSecurityAlertAsync(User user, SecurityAlert alert, CancellationToken cancellationToken);
}
