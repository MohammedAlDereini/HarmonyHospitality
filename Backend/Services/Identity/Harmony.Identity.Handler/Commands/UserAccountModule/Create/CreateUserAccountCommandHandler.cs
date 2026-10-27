namespace Harmony.Identity.Handler.Commands.UserAccountModule.Create;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.EnrolmentModule;
using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

/// <summary>
/// An admin creates an account (User.New + roles). Two ways to give it a password: by default an invitation
/// mail carries a one-time link and the person chooses the password, so nobody else ever knows it; or the admin types an
/// initial password, and the account is born with MustChangePassword so that password works exactly once. If the invitation
/// cannot be sent the account is not kept: no half-created person. The email is the user name and is unique inside the
/// tenant. Every role must be a role of this tenant. Identity hashes the password and applies the password rules.
/// </summary>
public class CreateUserAccountCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<CreateUserAccountCommand, CallResponse<Guid>>
{
    private readonly IRoleRepository roles;
    private readonly IUserAccountRepository userAccounts;
    private readonly ICallContext callContext;
    private readonly IAccountMailer mailer;
    private readonly IFederatedAuthorityRepository authorities;
    private readonly ILocalStaffEnrolmentRepository enrolments;
    private readonly TimeProvider clock;
    private readonly ILogger<CreateUserAccountCommandHandler> logger;

    public CreateUserAccountCommandHandler(UserManager<User> userManager, ICacheService cacheService, ISessionService sessionService, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, IRoleRepository roles, IUserAccountRepository userAccounts, ICallContext callContext, IAccountMailer mailer, IFederatedAuthorityRepository authorities, ILocalStaffEnrolmentRepository enrolments, TimeProvider clock, ILogger<CreateUserAccountCommandHandler> logger)
        : base(userManager, cacheService, sessionService, auditEvents, securityEvents)
    {
        this.authorities = authorities;
        this.enrolments = enrolments;
        this.clock = clock;
        this.roles = roles;
        this.userAccounts = userAccounts;
        this.callContext = callContext;
        this.mailer = mailer;
        this.logger = logger;
    }

    public async Task<CallResponse<Guid>> Handle(CreateUserAccountCommand command, CancellationToken cancellationToken)
    {
        // The validator already refused anything that is not one clean address (no spaces around it).
        var email = command.Email;

        // One e-mail is one person on the whole platform (decided 2026-10-16): a different case of the same address, in
        // this tenant or any other, is the same person and cannot be a second account.
        if ((await this.userAccounts.FindByUserNameAcrossTenantsAsync(this.UserManager.NormalizeName(email), cancellationToken)).Count > 0)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.UserAccount.EmailTaken);
        }

        // Every id must be a role of this tenant; the tenant filter on the query is the wall.
        var wanted = command.RoleIds.Distinct().ToList();
        var known = await this.roles.Query().Where(r => wanted.Contains(r.Id)).Select(r => new { r.PrivilegeLevel, r.IsSuperRole }).ToListAsync(cancellationToken);
        if (known.Count != wanted.Count)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Role.NotFound);
        }

        // The rank rule: a new account is born at or below its maker's own rank, never above.
        var callerRank = await this.userAccounts.RankOfAsync(RankRules.CallerId(this.callContext), cancellationToken);
        if (known.Any(r => RoleRank.Of(r.PrivilegeLevel, r.IsSuperRole) > callerRank))
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Rank.AboveSelf);
        }

        var user = User.Create(command.DisplayName, email, email);
        user.UpdateRoles(wanted);

        var byInvitation = string.IsNullOrEmpty(command.InitialPassword);

        // An e-mail the customer's company login covers needs no password: the company signs the person in.
        var company = byInvitation ? await this.CompanyCoveringAsync(email, cancellationToken) : null;
        if (!byInvitation)
        {
            user.RequirePasswordChange();
        }

        var result = byInvitation
            ? await this.UserManager.CreateAsync(user)
            : await this.UserManager.CreateAsync(user, command.InitialPassword!);
        if (!result.Succeeded)
        {
            var codes = result.Errors.Select(e => e.Code).ToList();
            this.logger.LogInformation("Create account refused: {Codes}.", string.Join(", ", codes));

            if (codes.Any(code => code.StartsWith("Duplicate", StringComparison.Ordinal)))
            {
                return Fail<Guid>(BusinessErrorCodes.Identity.UserAccount.EmailTaken);
            }

            if (codes.Any(code => code.StartsWith("Password", StringComparison.Ordinal)))
            {
                return Fail<Guid>(PasswordRefusal(codes));
            }

            return Fail<Guid>(BusinessErrorCodes.Identity.UserAccount.StoreRefused);
        }

        if (company is not null)
        {
            try
            {
                await this.mailer.SendCompanyWelcomeAsync(user, company.Scheme.ToString(), cancellationToken);
            }
            catch (Exception exception)
            {
                // The account works without the mail: the person signs in with the company. The log says so.
                this.logger.LogWarning(exception, "The company welcome mail for account {UserId} could not be sent.", user.Id);
            }
        }
        else if (byInvitation)
        {
            // The link carries a one-time token bound to the account's security stamp; it dies with the first password set.
            var token = await this.UserManager.GeneratePasswordResetTokenAsync(user);
            try
            {
                await this.mailer.SendInvitationAsync(user, token, cancellationToken);
            }
            catch (Exception exception)
            {
                // No mail, no account: the admin sees the refusal and tries again, nobody is left half-created.
                this.logger.LogError(exception, "Invitation mail could not be sent; the new account {UserId} is removed again.", user.Id);
                await this.UserManager.DeleteAsync(user);
                return Fail<Guid>(BusinessErrorCodes.Identity.UserAccount.InvitationNotSent);
            }
        }

        // Written once the account is kept for good: an invitation that fails removes the account again, and the trail must
        // never point at an account that does not exist.
        await this.TrailAsync(user, SecurityAuditActions.AccountCreated, cancellationToken);
        if (byInvitation && company is null)
        {
            // The invitation as a record (design 3.3): who sent it, until when; the e-mail only as a digest.
            var local = (await this.authorities.OfTenantAsync(user.TenantId, cancellationToken)).First(a => a.Scheme == AuthorityScheme.Local);
            await this.enrolments.AddAsync(LocalStaffEnrolment.Invite(user.TenantId, RankRules.CallerId(this.callContext), local.Id, user.Id, email, this.clock.GetUtcNow()), cancellationToken);
        }

        await this.AuditEvents.SaveChangesAsync(cancellationToken);

        // The cache follows the database at once, so the first token this person gets finds its version.
        await this.PublishSecurityVersionAsync(user, cancellationToken);
        this.logger.LogInformation(
            "Account {UserId} created in tenant {Tenant} with {Roles} role(s); {How}.",
            user.Id,
            user.TenantId,
            wanted.Count,
            byInvitation ? "an invitation link was sent" : "the password must be changed at first login");

        return CallResponseBuilder.CreateResponse<Guid>(eCallResponseStatus.Created).HasData(user.Id);
    }

    /// <summary>The caller's customer's switched-on company login that covers the e-mail, or null.</summary>
    private async Task<FederatedAuthority?> CompanyCoveringAsync(string email, CancellationToken cancellationToken)
    {
        var domain = FederatedAuthority.DomainOf(email);
        if (domain is null || !FederatedAuthority.IsDomainName(domain))
        {
            return null;
        }

        var login = await this.authorities.CoveringAsync(domain, cancellationToken);
        return login is not null && login.TenantId.ToString("D") == this.callContext.TenantId() ? login : null;
    }
}
