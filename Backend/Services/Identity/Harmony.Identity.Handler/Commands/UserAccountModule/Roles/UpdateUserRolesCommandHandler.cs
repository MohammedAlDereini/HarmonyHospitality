namespace Harmony.Identity.Handler.Commands.UserAccountModule.Roles;

using Harmony.Core.Identity.Permissions;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;

public class UpdateUserRolesCommandHandler : IRequestHandler<UpdateUserRolesCommand, CallResponse>
{
    private readonly IUserAccountRepository userAccounts;
    private readonly IRoleRepository roles;
    private readonly ICacheService cacheService;

    public UpdateUserRolesCommandHandler(IUserAccountRepository userAccounts, IRoleRepository roles, ICacheService cacheService)
    {
        this.userAccounts = userAccounts;
        this.roles = roles;
        this.cacheService = cacheService;
    }

    public async Task<CallResponse> Handle(UpdateUserRolesCommand command, CancellationToken cancellationToken)
    {
        var user = await this.userAccounts.GetWithRolesAsync(command.Id, cancellationToken);
        if (user is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        // Every id must be a role of this tenant; the tenant filter on the query is the wall.
        var wanted = command.RoleIds.Distinct().ToList();
        var known = await this.roles.Query().Where(r => wanted.Contains(r.Id)).Select(r => r.Id).ToListAsync(cancellationToken);
        if (known.Count != wanted.Count)
        {
            return Fail(BusinessErrorCodes.Identity.Role.NotFound);
        }

        user.UpdateRoles(wanted);
        await this.userAccounts.SaveChangesAsync(cancellationToken);

        // Roles travel inside the token. A changed set bumped the version, and the published number makes every
        // token issued with the old roles fail at its next request.
        await this.cacheService.AddAsync(
            SecurityVersionCacheKeys.UserVersionKey(user.TenantId, user.Id),
            user.SecurityVersion,
            SecurityVersionCacheKeys.UserVersionLifetime,
            cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }

    private static CallResponse Fail(string errorCode)
        => CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(errorCode));
}
