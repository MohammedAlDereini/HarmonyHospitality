using Harmony.Core.Enums;
using Harmony.Core.Identity.Permissions;
using Harmony.Identity.Handler.Queries.UserAccountModule.Access;
using Harmony.MediatR;

namespace Harmony.Identity.Api.Access;

/// <summary>When Identity's own doors find no copy for the caller at a hotel, the answer comes from the tables, not over HTTP.</summary>
public sealed class LocalHotelAccessSource(IMediator mediator) : IHotelAccessSource
{
    public async Task<HotelAccess> FetchAsync(Guid propertyId, CancellationToken cancellationToken = default)
    {
        var answer = await mediator.Send(new GetMyAccessAtQuery { PropertyId = propertyId }, cancellationToken);
        return answer.Status == eCallResponseStatus.Success ? answer.Data : null!;
    }
}
