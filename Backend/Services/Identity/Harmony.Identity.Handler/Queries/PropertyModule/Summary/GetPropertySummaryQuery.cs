using Harmony.Identity.Domain.Models.PropertyModule;

namespace Harmony.Identity.Handler.Queries.PropertyModule.Summary;

/// <summary>The hotels in numbers, for the console's home. No parameter: the numbers are the platform's.</summary>
public class GetPropertySummaryQuery : BaseQueryRequest<CallResponse<PropertySummaryModel>>
{
}
