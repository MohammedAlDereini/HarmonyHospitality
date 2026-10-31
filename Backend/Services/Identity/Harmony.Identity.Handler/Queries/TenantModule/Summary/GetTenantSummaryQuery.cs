using Harmony.Identity.Domain.Models.TenantModule;

namespace Harmony.Identity.Handler.Queries.TenantModule.Summary;

/// <summary>The customers in numbers, for the console's home. No parameter: the numbers are the platform's.</summary>
public class GetTenantSummaryQuery : BaseQueryRequest<CallResponse<TenantSummaryModel>>
{
}
