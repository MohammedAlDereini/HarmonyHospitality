namespace Harmony.Identity.Handler.Commands.UserAccountModule.SignInRecords;

using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.SessionModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Passkeys;
using Microsoft.Extensions.Logging;

/// <summary>A sign-out on the pages: the record of that Duende session ends. Sent by the logout page, never by a client.</summary>
public class EndSessionRecordCommand : BaseCommandRequest<CallResponse>
{
    public string SessionKey { get; set; } = null!;

    public string Reason { get; set; } = OperatorSession.SignedOut;
}

public class EndSessionRecordCommandHandler : IRequestHandler<EndSessionRecordCommand, CallResponse>
{
    private readonly IOperatorSessionRepository sessions;
    private readonly TimeProvider clock;

    public EndSessionRecordCommandHandler(IOperatorSessionRepository sessions, TimeProvider clock)
    {
        this.sessions = sessions;
        this.clock = clock;
    }

    public async Task<CallResponse> Handle(EndSessionRecordCommand command, CancellationToken cancellationToken)
    {
        // Ending an ended (or unknown) session is no error: sign-out arrives twice more often than you would think.
        if (!string.IsNullOrWhiteSpace(command.SessionKey) && await this.sessions.OpenByKeyAsync(command.SessionKey, cancellationToken) is { } row)
        {
            row.End(this.clock.GetUtcNow(), command.Reason);
            await this.sessions.SaveChangesAsync(cancellationToken);
        }

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }
}

/// <summary>
/// The person came back from their company's forced sign-in to confirm it is them (stage B, company people have no
/// password or passkey here). Sent by the CompanyConfirmed page with what the company's token said.
/// </summary>
public class ConfirmWithCompanyCommand : BaseCommandRequest<CallResponse>
{
    /// <summary>The person signed in at Identity: only they can confirm themselves.</summary>
    public Guid UserId { get; set; }

    public AuthorityScheme Scheme { get; set; }

    public string Issuer { get; set; } = null!;

    public string Subject { get; set; } = null!;

    public string? HostedDomain { get; set; }

    /// <summary>When the company signed them in (auth_time): within the confirmation's lifetime, or it is not fresh.</summary>
    public DateTimeOffset? AuthenticatedAt { get; set; }

    /// <summary>When the confirmation began here: a company sign-in from before it is an old session, not a fresh proof.</summary>
    public DateTimeOffset? StartedAt { get; set; }
}

public class ConfirmWithCompanyCommandHandler : IRequestHandler<ConfirmWithCompanyCommand, CallResponse>
{
    private static readonly TimeSpan ClockLeeway = TimeSpan.FromMinutes(1);

    private readonly IFederatedAuthorityRepository authorities;
    private readonly IExternalIdentityLinkRepository links;
    private readonly IUserAccountRepository userAccounts;
    private readonly IdentityConfirmation confirmation;
    private readonly ISecurityAuditEventRepository auditEvents;
    private readonly ISecurityEventFactory securityEvents;
    private readonly TimeProvider clock;
    private readonly ILogger<ConfirmWithCompanyCommandHandler> logger;

    public ConfirmWithCompanyCommandHandler(
        IFederatedAuthorityRepository authorities,
        IExternalIdentityLinkRepository links,
        IUserAccountRepository userAccounts,
        IdentityConfirmation confirmation,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        TimeProvider clock,
        ILogger<ConfirmWithCompanyCommandHandler> logger)
    {
        this.authorities = authorities;
        this.links = links;
        this.userAccounts = userAccounts;
        this.confirmation = confirmation;
        this.auditEvents = auditEvents;
        this.securityEvents = securityEvents;
        this.clock = clock;
        this.logger = logger;
    }

    public async Task<CallResponse> Handle(ConfirmWithCompanyCommand command, CancellationToken cancellationToken)
    {
        var refused = CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.UserAccount.IdentityConfirmationRequired));
        var user = await this.userAccounts.FindByIdAcrossTenantsAsync(command.UserId, cancellationToken);
        if (user is null)
        {
            return refused;
        }

        var login = command.Scheme == AuthorityScheme.Google
            ? (command.HostedDomain is { Length: > 0 } hd && FederatedAuthority.IsDomainName(hd) ? await this.authorities.ListingAsync(FederatedAuthority.DomainName(hd), cancellationToken) : null)
            : await this.authorities.FindByIssuerAsync(AuthorityScheme.Microsoft, command.Issuer, cancellationToken);

        // The company identity must be the one linked to this very person, signed in just now: after this confirmation
        // began (a minute's leeway for the two clocks), and within the confirmation's lifetime.
        var link = login is null ? null : await this.links.FindAsync(login.Id, command.Subject, cancellationToken);
        var now = this.clock.GetUtcNow();
        var fresh = command.AuthenticatedAt is { } at
            && command.StartedAt is { } started
            && at >= started - ClockLeeway
            && now - at < Domain.Entities.Aggregates.StepUpModule.StepUpAuthorization.Lifetime;
        if (login is null || link is null || !link.IsOpen || link.UserAccountId != user.Id || login.TenantId != user.TenantId || !fresh)
        {
            this.logger.LogInformation("Company confirmation refused for account {UserId} (link {Linked}, fresh {Fresh}).", user.Id, link?.IsOpen == true && link.UserAccountId == user.Id, fresh);
            return refused;
        }

        await this.confirmation.MarkAsync(user.TenantId, user.Id, "company", cancellationToken, login.Id);
        await this.auditEvents.AddAsync(this.securityEvents.Create(user, SecurityAuditActions.IdentityConfirmed, "company"), cancellationToken);
        await this.auditEvents.SaveChangesAsync(cancellationToken);
        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }
}
