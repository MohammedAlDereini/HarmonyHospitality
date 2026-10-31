using System.Linq.Expressions;
using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Models.UserAccountModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.UserAccountModule.List
{
    public class GetPagedUserAccountsQueryHandler : IRequestHandler<GetPagedUserAccountsQuery, CallResponse<PagedResult<UserAccountModel>>>
    {
        /// <summary>One projection for list and detail, so neither can ever select a hash or digest by accident.</summary>
        public static readonly Expression<Func<User, UserAccountModel>> Projection = a => new UserAccountModel
        {
            Id = a.Id,
            UserName = a.UserName!,
            Email = a.Email,
            DisplayName = a.DisplayName,
            TwoFactorEnabled = a.TwoFactorEnabled,
            TwoStepExemptReason = a.TwoStepExemptReason,
            RoleIds = a.UserRoles.Select(r => r.RoleId).ToList(),
            State = a.State,
            StateReason = a.StateReason,
            IsDirectoryManaged = a.ProvisioningConnectionId != null,
            SecurityVersion = a.SecurityVersion,
            CreatedDate = a.CreatedDate,
            CreatedBy = a.CreatedBy,
            ModifiedDate = a.ModifiedDate,
            ModifiedBy = a.ModifiedBy,
        };

        private readonly IUserAccountRepository userAccounts;
        private readonly IOperatorSessionRepository sessions;
        private readonly IUserDeviceRepository devices;
        private readonly ICallContext callContext;

        public GetPagedUserAccountsQueryHandler(IUserAccountRepository userAccounts, IOperatorSessionRepository sessions, IUserDeviceRepository devices, ICallContext callContext)
        {
            this.userAccounts = userAccounts;
            this.sessions = sessions;
            this.devices = devices;
            this.callContext = callContext;
        }

        public async Task<CallResponse<PagedResult<UserAccountModel>>> Handle(GetPagedUserAccountsQuery request, CancellationToken cancellationToken)
        {
            var keyword = request.SearchText?.Trim();
            var query = this.userAccounts.Query();

            query = query.WhereIf(
                keyword.IsNotNullOrEmpty() && keyword!.Length >= 2,
                a => a.UserName!.Contains(keyword!) || a.DisplayName.Contains(keyword!) || (a.Email != null && a.Email.Contains(keyword!)));

            query = query.WhereIf(request.State.HasValue, a => a.State == request.State!.Value);
            query = query.WhereIf(request.RoleId.HasValue, a => a.UserRoles.Any(r => r.RoleId == request.RoleId!.Value));
            var twoStep = request.TwoStep?.Trim().ToLowerInvariant();
            query = query.WhereIf(twoStep == "on", a => a.TwoFactorEnabled && a.TwoStepExemptReason == null);
            query = query.WhereIf(twoStep == "off", a => !a.TwoFactorEnabled && a.TwoStepExemptReason == null);
            query = query.WhereIf(twoStep == "exempt", a => a.TwoStepExemptReason != null);

            var pagedResult = await query.Select(Projection).GetPagedResultAsync(request, true, cancellationToken);
            if (Guid.TryParse(this.callContext.TenantId(), out var tenantId))
            {
                await AccountSignIns.DescribeAsync(pagedResult.Results, tenantId, this.sessions, this.devices, cancellationToken);
            }

            return CallResponseBuilder.CreateResponse<PagedResult<UserAccountModel>>(eCallResponseStatus.Success).HasData(pagedResult);
        }
    }
}
