using Harmony.Identity.Domain.Models.UserAccountModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.UserAccountModule.Access;

/// <summary>An account's assignments and grants with the role and the scope named, current first; inside the wall.</summary>
public class GetAccountAccessQueryHandler : IRequestHandler<GetAccountAccessQuery, CallResponse<AccountAccessModel>>
{
    private readonly IUserAccountRepository userAccounts;
    private readonly IRoleRepository roles;
    private readonly IPropertyRepository properties;
    private readonly IPropertyGroupRepository groups;
    private readonly TimeProvider clock;

    public GetAccountAccessQueryHandler(IUserAccountRepository userAccounts, IRoleRepository roles, IPropertyRepository properties, IPropertyGroupRepository groups, TimeProvider clock)
    {
        this.userAccounts = userAccounts;
        this.roles = roles;
        this.properties = properties;
        this.groups = groups;
        this.clock = clock;
    }

    public async Task<CallResponse<AccountAccessModel>> Handle(GetAccountAccessQuery request, CancellationToken cancellationToken)
    {
        var user = await this.userAccounts.Query().Where(u => u.Id == request.Id).Select(u => new { u.Id, Assignments = u.RoleAssignments.ToList(), Grants = u.AccessGrants.ToList() }).FirstOrDefaultAsync(cancellationToken);
        if (user is null)
        {
            return CallResponseBuilder.CreateResponse<AccountAccessModel>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.UserAccount.NotFound));
        }

        var roleCodes = await this.roles.Query().Select(r => new { r.Id, r.Code }).ToDictionaryAsync(r => r.Id, r => r.Code, cancellationToken);
        var hotelCodes = await this.properties.Query().Select(p => new { p.Id, p.Code }).ToDictionaryAsync(p => p.Id, p => p.Code, cancellationToken);
        var groupCodes = await this.groups.Query().Select(g => new { g.Id, g.Code }).ToDictionaryAsync(g => g.Id, g => g.Code, cancellationToken);
        var now = this.clock.GetUtcNow();
        string ScopeOf(Guid? hotel, Guid? group) => hotel is { } h ? hotelCodes.GetValueOrDefault(h, "?") : groupCodes.GetValueOrDefault(group ?? Guid.Empty, "?");

        var model = new AccountAccessModel
        {
            Id = user.Id,
            Assignments = user.Assignments
                .Select(a => new RoleAssignmentModel { Id = a.Id, RoleId = a.RoleId, RoleCode = roleCodes.GetValueOrDefault(a.RoleId, "?"), PropertyId = a.Scope.PropertyId, PropertyGroupId = a.Scope.PropertyGroupId, ScopeCode = ScopeOf(a.Scope.PropertyId, a.Scope.PropertyGroupId), EndsAtUtc = a.EndsAtUtc, IsCurrent = a.IsCurrentAt(now) })
                .OrderByDescending(a => a.IsCurrent).ThenBy(a => a.RoleCode).ThenBy(a => a.ScopeCode).ToList(),
            Grants = user.Grants
                .Select(g => new AccessGrantModel { Id = g.Id, RoleId = g.RoleId, RoleCode = roleCodes.GetValueOrDefault(g.RoleId, "?"), PropertyId = g.Scope.PropertyId, PropertyGroupId = g.Scope.PropertyGroupId, ScopeCode = ScopeOf(g.Scope.PropertyId, g.Scope.PropertyGroupId), FromUtc = g.FromUtc, ToUtc = g.ToUtc, RevokedAtUtc = g.RevokedAtUtc, GrantedById = g.GrantedById, Reason = g.Reason, IsActive = g.IsActiveAt(now) })
                .OrderByDescending(g => g.IsActive).ThenByDescending(g => g.FromUtc).ToList(),
        };
        return CallResponseBuilder.CreateResponse<AccountAccessModel>(eCallResponseStatus.Success).HasData(model);
    }
}
