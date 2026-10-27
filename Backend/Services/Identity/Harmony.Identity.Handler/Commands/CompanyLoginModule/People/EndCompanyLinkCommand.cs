namespace Harmony.Identity.Handler.Commands.CompanyLoginModule.People;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Handler.Commands.UserAccountModule;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// The administrator ends one company link of a person (the wrong Microsoft account was bound, or it is not theirs any
/// more). That company identity cannot sign in to this account again; a different one can link on its first sign-in.
/// </summary>
public class EndCompanyLinkCommand : BaseCommandRequest<CallResponse>
{
    /// <summary>The account.</summary>
    public Guid Id { get; set; }

    public Guid LinkId { get; set; }

    public class EndCompanyLinkCommandValidation : AbstractValidator<EndCompanyLinkCommand>
    {
        public EndCompanyLinkCommandValidation()
        {
            this.RuleFor(e => e.Id).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.IdRequired);
            this.RuleFor(e => e.LinkId).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.CompanyLogin.LinkIdRequired);
        }
    }
}

/// <summary>
/// The period closes and every session of the account ends (SecurityVersion bump, refresh tokens removed): a session made
/// through the ended link must not outlive it. Already ended: nothing to do.
/// </summary>
public class EndCompanyLinkCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<EndCompanyLinkCommand, CallResponse>
{
    private readonly IUserAccountRepository userAccounts;
    private readonly IExternalIdentityLinkRepository links;
    private readonly IFederatedAuthorityRepository authorities;
    private readonly ICallContext callContext;
    private readonly TimeProvider clock;

    public EndCompanyLinkCommandHandler(
        UserManager<User> userManager,
        ICacheService cacheService,
        ISessionService sessionService,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        IUserAccountRepository userAccounts,
        IExternalIdentityLinkRepository links,
        IFederatedAuthorityRepository authorities,
        ICallContext callContext,
        TimeProvider clock)
        : base(userManager, cacheService, sessionService, auditEvents, securityEvents)
    {
        this.userAccounts = userAccounts;
        this.links = links;
        this.authorities = authorities;
        this.callContext = callContext;
        this.clock = clock;
    }

    public async Task<CallResponse> Handle(EndCompanyLinkCommand command, CancellationToken cancellationToken)
    {
        var user = await this.LoadAsync(command.Id);
        if (user is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        if (await RankRules.OutrankRefusalAsync(this.userAccounts, RankRules.CallerId(this.callContext), user, cancellationToken) is { } refusal)
        {
            return Fail(refusal);
        }

        var link = (await this.links.OfAccountAsync(user.TenantId, user.Id, cancellationToken)).FirstOrDefault(l => l.Id == command.LinkId);
        if (link is null)
        {
            return Fail(BusinessErrorCodes.Identity.CompanyLogin.LinkNotFound);
        }

        if (!link.IsOpen)
        {
            return Ok();
        }

        var provider = (await this.authorities.FindAsync(user.TenantId, link.FederatedAuthorityId, cancellationToken))?.Scheme.ToString() ?? "company";
        link.End(this.clock.GetUtcNow());
        user.EndAllSessions();
        await this.AuditEvents.AddAsync(this.SecurityEvents.Create(user, SecurityAuditActions.CompanyLinkEnded, provider), cancellationToken);
        await this.SaveAsync(user);
        await this.PublishSecurityVersionAsync(user, cancellationToken);
        return Ok();
    }
}
