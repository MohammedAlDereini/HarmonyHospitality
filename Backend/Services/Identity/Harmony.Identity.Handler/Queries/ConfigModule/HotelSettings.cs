using Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;
using Harmony.Identity.Domain.Models.ConfigModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.ConfigModule;

/// <summary>
/// Loads what a hotel's resolution reads, in four small queries (the hotel, the active keys, the groups it is in now,
/// the customer's values at those places), and lets <see cref="SettingResolver"/> decide each key. The hotel is read
/// across customers: the property-access check already decided the caller may act there (the platform may anywhere).
/// </summary>
public static class HotelSettings
{
    /// <summary>The hotel's own customer, and every active key (narrowed to one key or one owner when given) resolved there.</summary>
    public sealed record Answer(Guid TenantId, List<EffectiveSettingModel> Settings);

    /// <summary>The active keys (narrowed to one key or one owner when given) resolved at the hotel; null when the hotel does not exist.</summary>
    public static async Task<Answer?> ResolveAsync(
        IPropertyRepository properties,
        IConfigSettingRepository settings,
        IPropertyGroupMemberRepository members,
        IConfigValueRepository values,
        Guid propertyId,
        string? key,
        string? ownerService,
        DateTimeOffset at,
        CancellationToken cancellationToken)
    {
        var hotel = await properties.QueryAcrossTenants()
            .Where(p => p.Id == propertyId)
            .Select(p => new { p.Id, p.TenantId, p.Code, p.PropertyTypeCode })
            .FirstOrDefaultAsync(cancellationToken);
        if (hotel is null)
        {
            return null;
        }

        var keys = settings.Query().Where(s => s.IsActive);
        if (key is not null)
        {
            keys = keys.Where(s => s.Id == key);
        }

        if (!string.IsNullOrWhiteSpace(ownerService))
        {
            var owner = ownerService.Trim();
            keys = keys.Where(s => s.OwnerService == owner);
        }

        var registered = await keys.OrderBy(s => s.Id).ToListAsync(cancellationToken);
        if (registered.Count == 0)
        {
            return new Answer(hotel.TenantId, []);
        }

        var groups = await members.GroupsOfHotelAsync(hotel.TenantId, hotel.Id, at, cancellationToken);
        var rows = await values.ForHotelAsync(
            hotel.TenantId,
            hotel.Id,
            groups.Select(g => g.Id).ToList(),
            key is null ? null : registered.Select(s => s.Key).ToList(),
            cancellationToken);

        var facts = new HotelFacts(hotel.Id, hotel.Code, hotel.PropertyTypeCode);
        var byKey = rows.ToLookup(r => r.Key, StringComparer.OrdinalIgnoreCase);
        var answers = new List<EffectiveSettingModel>(registered.Count);
        foreach (var setting in registered)
        {
            var mine = byKey[setting.Key].ToList();
            var resolution = SettingResolver.Resolve(
                setting,
                facts,
                mine.FirstOrDefault(r => r.Scope == ConfigScopeLevel.Property)?.Value,
                groups.Select(g => new GroupStanding(g.Id, g.Code, g.Precedence, g.IsActive, mine.FirstOrDefault(r => r.Scope == ConfigScopeLevel.PropertyGroup && r.ScopeId == g.Id)?.Value)).ToList(),
                mine.FirstOrDefault(r => r.Scope == ConfigScopeLevel.Tenant)?.Value);
            answers.Add(Model(setting, resolution));
        }

        return new Answer(hotel.TenantId, answers);
    }

    private static EffectiveSettingModel Model(ConfigSetting setting, Resolution resolution) => new()
    {
        Key = setting.Key,
        OwnerService = setting.OwnerService,
        DataType = setting.DataType,
        Outcome = resolution.Outcome.ToString(),
        Value = resolution.Value,
        Source = resolution.Source?.ToString(),
        SourceCode = resolution.SourceCode,
        Trace = resolution.Trace.Select(step => new ResolutionStepModel
        {
            Level = step.Level.ToString(),
            Id = step.Id,
            Code = step.Code,
            Precedence = step.Precedence,
            Value = step.Value,
            Verdict = step.Verdict.ToString(),
        }).ToList(),
    };
}
