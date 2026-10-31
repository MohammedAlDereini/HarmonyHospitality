using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.Models.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Handler.Queries.UserAccountModule.List;

namespace Harmony.Identity.Handler.Queries.UserAccountModule.Detail
{
    public class GetUserAccountByIdQueryHandler : IRequestHandler<GetUserAccountByIdQuery, CallResponse<UserAccountModel>>
    {
        private readonly IUserAccountRepository userAccounts;
        private readonly IOperatorSessionRepository sessions;
        private readonly IUserDeviceRepository devices;
        private readonly ICallContext callContext;

        public GetUserAccountByIdQueryHandler(IUserAccountRepository userAccounts, IOperatorSessionRepository sessions, IUserDeviceRepository devices, ICallContext callContext)
        {
            this.userAccounts = userAccounts;
            this.sessions = sessions;
            this.devices = devices;
            this.callContext = callContext;
        }

        public async Task<CallResponse<UserAccountModel>> Handle(GetUserAccountByIdQuery request, CancellationToken cancellationToken)
        {
            var model = await this.userAccounts.Query()
                .Where(a => a.Id == request.Id)
                .Select(GetPagedUserAccountsQueryHandler.Projection)
                .FirstOrDefaultAsync(cancellationToken);

            if (model is null)
            {
                return CallResponseBuilder.CreateResponse<UserAccountModel>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.UserAccount.NotFound));
            }

            if (Guid.TryParse(this.callContext.TenantId(), out var tenantId))
            {
                await AccountSignIns.DescribeAsync([model], tenantId, this.sessions, this.devices, cancellationToken);
            }

            return CallResponseBuilder.CreateResponse<UserAccountModel>(eCallResponseStatus.Success).HasData(model);
        }
    }
}
