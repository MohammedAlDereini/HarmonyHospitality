using Harmony.Core.Enums;
using Harmony.Core.Identity.Settings;
using Harmony.Identity.Handler.Queries.ConfigModule;
using Harmony.MediatR;

namespace Harmony.Identity.Api.Access;

/// <summary>When Identity itself reads a hotel's settings and finds no copy, the answer comes from the tables, not over HTTP.</summary>
public sealed class LocalSettingsSource(IMediator mediator) : ISettingsSource
{
    public async Task<IReadOnlyList<EffectiveSetting>> FetchAsync(Guid propertyId, CancellationToken cancellationToken = default)
    {
        var answer = await mediator.Send(new GetEffectiveSettingsQuery { PropertyId = propertyId }, cancellationToken);
        return answer.Status == eCallResponseStatus.Success
            ? answer.Data.Select(s => new EffectiveSetting { Key = s.Key, DataType = s.DataType, Outcome = s.Outcome, Value = s.Value }).ToList()
            : null!;
    }
}
