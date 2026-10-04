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
}
