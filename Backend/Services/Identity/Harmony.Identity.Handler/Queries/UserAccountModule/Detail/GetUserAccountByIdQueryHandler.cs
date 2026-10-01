using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.Models.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Handler.Queries.UserAccountModule.List;

namespace Harmony.Identity.Handler.Queries.UserAccountModule.Detail
{
    public class GetUserAccountByIdQueryHandler : IRequestHandler<GetUserAccountByIdQuery, CallResponse<UserAccountModel>>
    {
        private readonly IUserAccountRepository userAccounts;

        public GetUserAccountByIdQueryHandler(IUserAccountRepository userAccounts)
        {
            this.userAccounts = userAccounts;
        }

        public async Task<CallResponse<UserAccountModel>> Handle(GetUserAccountByIdQuery request, CancellationToken cancellationToken)
        {
            var model = await this.userAccounts.Query()
                .Where(a => a.Id == request.Id)
                .Select(GetPagedUserAccountsQueryHandler.Projection)
                .FirstOrDefaultAsync(cancellationToken);

            if (model is null)
            {
                return CallResponseBuilder.CreateResponse<UserAccountModel>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(IdentityErrorCodes.UserAccountNotFound));
            }

            return CallResponseBuilder.CreateResponse<UserAccountModel>(eCallResponseStatus.Success).HasData(model);
        }
    }
}
