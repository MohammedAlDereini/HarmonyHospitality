using Harmony.Identity.Domain.Models.TenantModule;

namespace Harmony.Identity.Handler.Queries.TenantModule.Mine;

/// <summary>The caller's own customer row with its whole contract. No parameter: the caller's token says which customer.</summary>
public class GetMyTenantQuery : BaseQueryRequest<CallResponse<TenantModel>>
{
}
