namespace Harmony.Identity.Handler.Commands.CompanyLoginModule.SignIn;

using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;
using Harmony.Identity.Domain.Entities.Aggregates.IdentityLinkModule;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

/// <summary>
/// The company sign-in rules, in order:
/// 1. the company is one of our customers' company logins (Microsoft: the exact issuer, which carries the company's tenant
///    id; Google: the Workspace domain), and it is on;
/// 2. the person is found through an open link (company login + permanent id); on the first company sign-in, through the
///    work e-mail, once, inside that customer only, on a listed domain, verified by the company; then linked for good;
/// 3. the account and the customer are active, the administrator did not keep the person on a password, and the company
///    login still covers their e-mail.
/// A refusal of a known account goes in its trail; an unknown one only in the log.
/// </summary>
public class CompanySignInCommandHandler : IRequestHandler<CompanySignInCommand, CompanySignInResult>
{
    private readonly IFederatedAuthorityRepository authorities;
    private readonly IExternalIdentityLinkRepository links;
    private readonly IUserAccountRepository userAccounts;
    private readonly ITenantRepository tenants;
    private readonly ISecurityAuditEventRepository auditEvents;
    private readonly ISecurityEventFactory securityEvents;
    private readonly ILookupNormalizer normalizer;
    private readonly TimeProvider clock;
    private readonly ILogger<CompanySignInCommandHandler> logger;

    public CompanySignInCommandHandler(
        IFederatedAuthorityRepository authorities,
        IExternalIdentityLinkRepository links,
        IUserAccountRepository userAccounts,
        ITenantRepository tenants,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        ILookupNormalizer normalizer,
        TimeProvider clock,
        ILogger<CompanySignInCommandHandler> logger)
    {
        this.authorities = authorities;
        this.links = links;
        this.userAccounts = userAccounts;
        this.tenants = tenants;
        this.auditEvents = auditEvents;
        this.securityEvents = securityEvents;
        this.normalizer = normalizer;
        this.clock = clock;
        this.logger = logger;
    }

    public async Task<CompanySignInResult> Handle(CompanySignInCommand command, CancellationToken cancellationToken)
    {
        var login = await this.CompanyLoginAsync(command, cancellationToken);
        if (login is null)
        {
            this.logger.LogInformation("Company sign-in refused: no customer has this {Scheme} company login.", command.Scheme);
            return CompanySignInResult.Refused(CompanySignInResult.CompanyUnknown);
        }

        if (!login.IsEnabled)
        {
            this.logger.LogInformation("Company sign-in refused: the {Scheme} company login of tenant {TenantId} is off.", command.Scheme, login.TenantId);
            return CompanySignInResult.Refused(CompanySignInResult.CompanyLoginOff);
        }

        var provider = login.Scheme.ToString();
        var link = await this.links.FindAsync(login.Id, command.Subject, cancellationToken);
        User? user;
        if (link is not null)
        {
            user = await this.userAccounts.FindByIdAcrossTenantsAsync(link.UserAccountId, cancellationToken);
            if (user is null || user.TenantId != login.TenantId)
            {
                return CompanySignInResult.Refused(CompanySignInResult.NoAccount);
            }

            if (!link.IsOpen)
            {
                return await this.RefuseAsync(user, CompanySignInResult.LinkEnded, cancellationToken);
            }
        }
        else
        {
            var first = await this.FirstSignInAsync(command, login, cancellationToken);
            if (first.Refusal is not null)
            {
                return first.User is null
                    ? CompanySignInResult.Refused(first.Refusal)
                    : await this.RefuseAsync(first.User, first.Refusal, cancellationToken);
            }

            user = first.User!;
        }

        if (!user.CanSignIn)
        {
            return await this.RefuseAsync(user, CompanySignInResult.AccountNotActive, cancellationToken);
        }

        if (await this.tenants.StatusOfAsync(user.TenantId, cancellationToken) != TenantStatus.Active)
        {
            return await this.RefuseAsync(user, CompanySignInResult.TenantNotActive, cancellationToken);
        }

        if (user.PasswordSignIn || !login.Covers(user.Email))
        {
            return await this.RefuseAsync(user, CompanySignInResult.PasswordSignIn, cancellationToken);
        }

        if (link is null)
        {
            await this.links.AddAsync(ExternalIdentityLink.Bind(login.TenantId, login.Id, command.Subject, user.Id, ExternalIdentityLink.FirstSignIn, this.clock.GetUtcNow()), cancellationToken);
            await this.auditEvents.AddAsync(this.securityEvents.Create(user, SecurityAuditActions.CompanyLinked, provider), cancellationToken);
            await this.links.SaveChangesAsync(cancellationToken);
            this.logger.LogInformation("Account {UserId} linked to its {Scheme} company identity on its first company sign-in.", user.Id, login.Scheme);
        }

        return CompanySignInResult.In(user);
    }

    /// <summary>Which customer's company login the token comes from: Microsoft by its exact issuer, Google by its Workspace domain.</summary>
    private async Task<FederatedAuthority?> CompanyLoginAsync(CompanySignInCommand command, CancellationToken cancellationToken)
    {
        if (command.Scheme == AuthorityScheme.Microsoft)
        {
            return await this.authorities.FindByIssuerAsync(AuthorityScheme.Microsoft, command.Issuer, cancellationToken);
        }

        if (command.Scheme == AuthorityScheme.Google && command.HostedDomain is { Length: > 0 } hosted && FederatedAuthority.IsDomainName(hosted))
        {
            var login = await this.authorities.ListingAsync(FederatedAuthority.DomainName(hosted), cancellationToken);
            return login is { Scheme: AuthorityScheme.Google } ? login : null;
        }

        return null;
    }

    /// <summary>The first company sign-in: the work e-mail finds the account, once, inside this customer only.</summary>
    private async Task<(User? User, string? Refusal)> FirstSignInAsync(CompanySignInCommand command, FederatedAuthority login, CancellationToken cancellationToken)
    {
        var domain = FederatedAuthority.DomainOf(command.Email);
        if (domain is null || !command.EmailVerified)
        {
            return (null, CompanySignInResult.NoVerifiedEmail);
        }

        if (login.Domains.All(d => d.Domain != domain))
        {
            return (null, CompanySignInResult.DomainNotListed);
        }

        var candidates = await this.userAccounts.FindByUserNameAcrossTenantsAsync(this.normalizer.NormalizeName(command.Email!.Trim()), cancellationToken);
        var user = candidates.SingleOrDefault(u => u.TenantId == login.TenantId);
        if (user is null)
        {
            this.logger.LogInformation("Company sign-in refused: tenant {TenantId} has no account for this work e-mail.", login.TenantId);
            return (null, CompanySignInResult.NoAccount);
        }

        // One company identity per account and company login: a second one never slips in beside an open link.
        var held = await this.links.OfAccountAsync(user.TenantId, user.Id, cancellationToken);
        if (held.Any(l => l.FederatedAuthorityId == login.Id && l.IsOpen))
        {
            return (user, CompanySignInResult.LinkedToAnother);
        }

        return (user, null);
    }

    private async Task<CompanySignInResult> RefuseAsync(User user, string why, CancellationToken cancellationToken)
    {
        this.logger.LogInformation("Company sign-in refused for account {UserId}: {Reason}.", user.Id, why);
        await this.auditEvents.AddAsync(this.securityEvents.Create(user, SecurityAuditActions.SignInCompanyRefused, why), cancellationToken);
        await this.auditEvents.SaveChangesAsync(cancellationToken);
        return CompanySignInResult.Refused(why);
    }
}
