using Harmony.Identity.Domain.Models.UserAccountModule;

namespace Harmony.Identity.Handler.Queries.UserAccountModule.Detail
{
    public class GetUserAccountByIdQuery : BaseQueryRequest<CallResponse<UserAccountModel>>
    {
        public Guid Id { get; set; }
    }
}
