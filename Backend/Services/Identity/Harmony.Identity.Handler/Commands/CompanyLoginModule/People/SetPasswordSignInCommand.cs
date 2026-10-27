namespace Harmony.Identity.Handler.Commands.CompanyLoginModule.People;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Handler.Commands.UserAccountModule;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// The administrator decides how one person signs in: kept on a Harmony password (true), or following the customer's
/// company login (false). Only the administrator decides this; a person never changes it for themselves.
/// </summary>
public class SetPasswordSignInCommand : BaseCommandRequest<CallResponse>
{
    /// <summary>The account.</summary>
    public Guid Id { get; set; }

    public bool PasswordSignIn { get; set; }

    public class SetPasswordSignInCommandValidation : AbstractValidator<SetPasswordSignInCommand>
    {
        public SetPasswordSignInCommandValidation()
        {
            this.RuleFor(e => e.Id).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.IdRequired);
        }
    }
}

/// <summary>
/// Inside the caller's customer (UserManager walls it), under the rank rule. Giving the last password administrator back
/// to the company login is refused while it covers them (00127): somebody must be able to get in if the company cannot.
/// </summary>
public class SetPasswordSignInCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<SetPasswordSignInCommand, CallResponse>
{
    private readonly IUserAccountRepository userAccounts;
    private readonly IFederatedAuthorityRepository authorities;
    private readonly ICallContext callContext;

    public SetPasswordSignInCommandHandler(
        UserManager<User> userManager,
        ICacheService cacheService,
        ISessionService sessionService,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        IUserAccountRepository userAccounts,
        IFederatedAuthorityRepository authorities,
        ICallContext callContext)
        : base(userManager, cacheService, sessionService, auditEvents, securityEvents)
    {
        this.userAccounts = userAccounts;
        this.authorities = authorities;
        this.callContext = callContext;
    }

    public async Task<CallResponse> Handle(SetPasswordSignInCommand command, CancellationToken cancellationToken)
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

        if (user.PasswordSignIn == command.PasswordSignIn)
        {
            return Ok();
        }

        if (!command.PasswordSignIn)
        {
            var logins = await this.authorities.OfTenantAsync(user.TenantId, cancellationToken);
            if (!await CompanyLoginRules.FallbackHoldsAsync(this.userAccounts, user.TenantId, logins, user.Id, personPasswordSignIn: false, cancellationToken))
            {
                return Fail(BusinessErrorCodes.Identity.CompanyLogin.FallbackAdministratorRequired);
            }

            user.FollowCompanySignIn();
        }
        else
        {
            user.KeepPasswordSignIn();
        }

        await this.AuditEvents.AddAsync(this.SecurityEvents.Create(user, SecurityAuditActions.SignInMethodChanged, command.PasswordSignIn ? "password" : "company"), cancellationToken);
        await this.SaveAsync(user);
        return Ok();
    }
}
