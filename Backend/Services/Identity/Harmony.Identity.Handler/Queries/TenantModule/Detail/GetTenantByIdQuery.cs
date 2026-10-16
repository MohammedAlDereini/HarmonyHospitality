using Harmony.Identity.Domain.Models.TenantModule;

namespace Harmony.Identity.Handler.Queries.TenantModule.Detail;

public class GetTenantByIdQuery : BaseQueryRequest<CallResponse<TenantModel>>
{
    public Guid Id { get; set; }
}
