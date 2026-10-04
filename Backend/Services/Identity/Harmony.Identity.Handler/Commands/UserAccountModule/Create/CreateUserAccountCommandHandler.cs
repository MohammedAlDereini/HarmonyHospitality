namespace Harmony.Identity.Handler.Commands.UserAccountModule.Create;

using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

/// <summary>
/// An admin creates an account the PropX way (User.New + roles), with one bridge until e-mail exists: the admin types an
/// initial password. The account is born with MustChangePassword, so that password works exactly once and the admin never
/// knows the real one. The email is the user name and is unique inside the tenant (the store is tenant-filtered, the index
/// is per tenant). Every role must be a role of this tenant. Identity hashes the password and applies the password rules.
/// </summary>
public class CreateUserAccountCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<CreateUserAccountCommand, CallResponse<Guid>>
{
    private readonly IRoleRepository roles;
    private readonly ILogger<CreateUserAccountCommandHandler> logger;

    public CreateUserAccountCommandHandler(UserManager<User> userManager, ICacheService cacheService, IRoleRepository roles, ILogger<CreateUserAccountCommandHandler> logger)
        : base(userManager, cacheService)
    {
        this.roles = roles;
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
        user.RequirePasswordChange();

        var result = await this.UserManager.CreateAsync(user, command.InitialPassword);
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

        // PropX: the cache follows the database at once, so the first token this person gets finds its version.
        await this.PublishSecurityVersionAsync(user, cancellationToken);
        this.logger.LogInformation("Account {UserId} created in tenant {Tenant} with {Roles} role(s); the password must be changed at first login.", user.Id, user.TenantId, wanted.Count);

        return CallResponseBuilder.CreateResponse<Guid>(eCallResponseStatus.Created).HasData(user.Id);
    }
}
