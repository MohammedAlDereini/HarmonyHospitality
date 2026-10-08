namespace Harmony.Identity.Handler.Commands.UserAccountModule.Roles;

using Harmony.Core.Identity.Permissions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;

public class UpdateUserRolesCommandHandler : IRequestHandler<UpdateUserRolesCommand, CallResponse>
{
    private readonly IUserAccountRepository userAccounts;
    private readonly IRoleRepository roles;
    private readonly ICacheService cacheService;
    private readonly ISessionService sessionService;
    private readonly ISecurityAuditEventRepository auditEvents;
    private readonly ISecurityEventFactory securityEvents;

    public UpdateUserRolesCommandHandler(IUserAccountRepository userAccounts, IRoleRepository roles, ICacheService cacheService, ISessionService sessionService, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents)
    {
        this.userAccounts = userAccounts;
        this.roles = roles;
        this.cacheService = cacheService;
        this.sessionService = sessionService;
        this.auditEvents = auditEvents;
        this.securityEvents = securityEvents;
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

        // A changed set bumps the version. The same set is a no-op success, like PropX's UpdateRolePermissions: nothing is
        // saved, no trail row, and nobody is signed out because an admin pressed Save without changing anything.
        var before = user.SecurityVersion;
        user.UpdateRoles(wanted);
        if (user.SecurityVersion == before)
        {
            return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
        }

        // Staged here, saved with the roles below.
        await this.auditEvents.AddAsync(this.securityEvents.Create(user, SecurityAuditActions.RolesChanged), cancellationToken);
        await this.userAccounts.SaveChangesAsync(cancellationToken);

        // Roles travel inside the token. A changed set bumped the version, and the published number makes every
        // token issued with the old roles fail at its next request.
        await this.cacheService.AddAsync(
            SecurityVersionCacheKeys.UserVersionKey(user.TenantId, user.Id),
            user.SecurityVersion,
            SecurityVersionCacheKeys.UserVersionLifetime,
            cancellationToken);
        await this.sessionService.EndAllAsync(user.Id, cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }

    private static CallResponse Fail(string errorCode)
        => CallResponseBuilder.CreateResponse(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(errorCode));
}
