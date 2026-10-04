namespace Harmony.Identity.Handler.Commands.UserAccountModule.Create;

using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

/// <summary>
/// An admin creates an account the PropX way (User.New + roles). Two ways to give it a password: by default an invitation
/// mail carries a one-time link and the person chooses the password, so nobody else ever knows it; or the admin types an
/// initial password, and the account is born with MustChangePassword so that password works exactly once. If the invitation
/// cannot be sent the account is not kept: no half-created person. The email is the user name and is unique inside the
/// tenant. Every role must be a role of this tenant. Identity hashes the password and applies the password rules.
/// </summary>
public class CreateUserAccountCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<CreateUserAccountCommand, CallResponse<Guid>>
{
    private readonly IRoleRepository roles;
    private readonly IAccountMailer mailer;
    private readonly ILogger<CreateUserAccountCommandHandler> logger;

    public CreateUserAccountCommandHandler(UserManager<User> userManager, ICacheService cacheService, ISessionRevoker sessionRevoker, IRoleRepository roles, IAccountMailer mailer, ILogger<CreateUserAccountCommandHandler> logger)
        : base(userManager, cacheService, sessionRevoker)
    {
        this.roles = roles;
        this.mailer = mailer;
        this.logger = logger;
    }

    public async Task<CallResponse<Guid>> Handle(CreateUserAccountCommand command, CancellationToken cancellationToken)
    {
        // The validator already refused anything that is not one clean address (no spaces around it).
        var email = command.Email;

        // Normalised lookup inside the caller's tenant: a different case of the same address is the same account.
        if (await this.UserManager.FindByNameAsync(email) is not null)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.UserAccount.EmailTaken);
        }

        // Every id must be a role of this tenant; the tenant filter on the query is the wall.
        var wanted = command.RoleIds.Distinct().ToList();
        var known = await this.roles.Query().CountAsync(r => wanted.Contains(r.Id), cancellationToken);
        if (known != wanted.Count)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Role.NotFound);
        }

        var user = User.Create(command.DisplayName, email, email);
        user.UpdateRoles(wanted);

        var byInvitation = string.IsNullOrEmpty(command.InitialPassword);
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
                return Fail<Guid>(BusinessErrorCodes.Identity.UserAccount.PasswordRejected);
            }

            return Fail<Guid>(BusinessErrorCodes.Identity.UserAccount.StoreRefused);
        }

        if (byInvitation)
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

        // PropX: the cache follows the database at once, so the first token this person gets finds its version.
        await this.PublishSecurityVersionAsync(user, cancellationToken);
        this.logger.LogInformation(
            "Account {UserId} created in tenant {Tenant} with {Roles} role(s); {How}.",
            user.Id,
            user.TenantId,
            wanted.Count,
            byInvitation ? "an invitation link was sent" : "the password must be changed at first login");

        return CallResponseBuilder.CreateResponse<Guid>(eCallResponseStatus.Created).HasData(user.Id);
    }
}
