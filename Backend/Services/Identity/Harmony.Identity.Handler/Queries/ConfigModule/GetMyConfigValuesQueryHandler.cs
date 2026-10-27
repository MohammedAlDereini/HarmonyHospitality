using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;
using Harmony.Identity.Domain.Models.ConfigModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Handler.Commands.ConfigModule.Values;

namespace Harmony.Identity.Handler.Queries.ConfigModule;

public class GetMyConfigValuesQueryHandler : IRequestHandler<GetMyConfigValuesQuery, CallResponse<PagedResult<ConfigValueModel>>>
{
    private readonly IConfigValueRepository values;
    private readonly IPropertyRepository properties;
    private readonly IPropertyGroupRepository groups;
    private readonly ITenantRepository tenants;
    private readonly IPropertyGroupMemberRepository members;
    private readonly ICallContext callContext;
    private readonly TimeProvider clock;

    public GetMyConfigValuesQueryHandler(
        IConfigValueRepository values,
        IPropertyRepository properties,
        IPropertyGroupRepository groups,
        ITenantRepository tenants,
        IPropertyGroupMemberRepository members,
        ICallContext callContext,
        TimeProvider clock)
    {
        this.values = values;
        this.properties = properties;
        this.groups = groups;
        this.tenants = tenants;
        this.members = members;
        this.callContext = callContext;
        this.clock = clock;
    }

    public async Task<CallResponse<PagedResult<ConfigValueModel>>> Handle(GetMyConfigValuesQuery request, CancellationToken cancellationToken)
    {
        var query = this.values.Query();
        if (!string.IsNullOrWhiteSpace(request.Key))
        {
            var key = request.Key.Trim();
            query = query.Where(v => v.Key == key);
        }

        if (ConfigValueRules.LevelOf(request.Scope) is { } level)
        {
            query = query.Where(v => v.Scope == level);
        }

        if (request.ScopeId is { } scopeId)
        {
            query = query.Where(v => v.ScopeId == scopeId);
        }

        // A hotel on the call: what reaches that hotel, nothing of the rest of the chain.
        if (Guid.TryParse(this.callContext.PropertyId(), out var hotel) && hotel != Guid.Empty
            && Guid.TryParse(this.callContext.TenantId(), out var tenantId))
        {
            var groupIds = (await this.members.GroupsOfHotelAsync(tenantId, hotel, this.clock.GetUtcNow(), cancellationToken)).Select(g => g.Id).ToList();
            query = query.Where(v => v.Scope == ConfigScopeLevel.Tenant
                || (v.Scope == ConfigScopeLevel.Property && v.ScopeId == hotel)
                || (v.Scope == ConfigScopeLevel.PropertyGroup && groupIds.Contains(v.ScopeId)));
        }

        var page = await query
            .Select(v => new ConfigValueModel
            {
                Id = v.Id,
                Key = v.Key,
                Scope = v.Scope == ConfigScopeLevel.Tenant ? nameof(ConfigScopeLevel.Tenant) : v.Scope == ConfigScopeLevel.PropertyGroup ? nameof(ConfigScopeLevel.PropertyGroup) : nameof(ConfigScopeLevel.Property),
                ScopeId = v.ScopeId,
                ScopeCode = string.Empty,
                Value = v.Value,
                ModifiedDate = v.ModifiedDate ?? v.CreatedDate,
            })
            .GetPagedResultAsync(request, true, cancellationToken);

        // The places in words: two small reads inside the wall, then the customer's own code.
        var ids = page.Results.Select(r => r.ScopeId).Distinct().ToList();
        var hotelCodes = await this.properties.Query().Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Code, cancellationToken);
        var groupCodes = await this.groups.Query().Where(g => ids.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.Code, cancellationToken);
        var tenantCodes = new Dictionary<Guid, string>();
        foreach (var row in page.Results)
        {
            row.ScopeCode = row.Scope switch
            {
                nameof(ConfigScopeLevel.Property) => hotelCodes.GetValueOrDefault(row.ScopeId, string.Empty),
                nameof(ConfigScopeLevel.PropertyGroup) => groupCodes.GetValueOrDefault(row.ScopeId, string.Empty),
                _ => tenantCodes.TryGetValue(row.ScopeId, out var code)
                    ? code
                    : tenantCodes[row.ScopeId] = (await this.tenants.FindAsync(row.ScopeId, cancellationToken))?.Code ?? string.Empty,
            };
        }

        return CallResponseBuilder.CreateResponse<PagedResult<ConfigValueModel>>(eCallResponseStatus.Success).HasData(page);
    }
}
