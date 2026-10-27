namespace Harmony.Identity.Domain.Entities.Aggregates.ConfigModule;

/// <summary>
/// Which value wins for a key at a hotel (design 5.1, the ERD's resolve(propertyId, key)): the hotel's own value, then
/// the highest-precedence active group the hotel is in that sets it, then the customer's value, then the system default.
/// Two groups at that same highest precedence are a conflict, reported with both names, never a coin toss. A domain
/// service, not an aggregate method: it reads across a customer, its groups and a hotel, which no single aggregate may.
/// Pure: the handler loads the rows, this decides. It never parses a value.
/// </summary>
public static class SettingResolver
{
    public static Resolution Resolve(
        ConfigSetting setting,
        HotelFacts hotel,
        string? hotelValue,
        IReadOnlyList<GroupStanding> groups,
        string? tenantValue)
    {
        ArgumentNullException.ThrowIfNull(setting);
        ArgumentNullException.ThrowIfNull(hotel);
        ArgumentNullException.ThrowIfNull(groups);

        if (!setting.AppliesTo(hotel.PropertyTypeCode))
        {
            return new Resolution(ResolutionOutcome.NotApplicable, null, null, null, []);
        }

        var trace = new List<ResolutionStep>();

        // 1. The hotel's own value beats everything.
        var won = hotelValue is not null;
        trace.Add(new ResolutionStep(ResolutionLevel.Property, hotel.Id, hotel.Code, null, hotelValue, won ? ResolutionVerdict.Won : ResolutionVerdict.NotSet));
        Resolution? answer = won ? new Resolution(ResolutionOutcome.Value, hotelValue, ResolutionLevel.Property, hotel.Code, trace) : null;

        // 2. The groups: the highest precedence among the active ones that set it; a tie there is a conflict.
        var setters = groups.Where(g => g.IsActive && g.Value is not null).ToList();
        var top = setters.Count == 0 ? (int?)null : setters.Max(g => g.Precedence);
        var tied = top is null ? [] : setters.Where(g => g.Precedence == top).ToList();
        foreach (var group in groups.OrderByDescending(g => g.Precedence).ThenBy(g => g.Code, StringComparer.Ordinal))
        {
            var verdict = group switch
            {
                { IsActive: false } => ResolutionVerdict.Inactive,
                { Value: null } => ResolutionVerdict.NotSet,
                _ when group.Precedence != top => ResolutionVerdict.Outranked,
                _ when tied.Count > 1 => ResolutionVerdict.Tied,
                _ when answer is not null => ResolutionVerdict.Overridden,
                _ => ResolutionVerdict.Won,
            };
            trace.Add(new ResolutionStep(ResolutionLevel.PropertyGroup, group.Id, group.Code, group.Precedence, group.Value, verdict));
        }

        if (answer is null && tied.Count > 1)
        {
            answer = new Resolution(ResolutionOutcome.Conflict, null, ResolutionLevel.PropertyGroup, null, trace);
        }
        else if (answer is null && tied.Count == 1)
        {
            answer = new Resolution(ResolutionOutcome.Value, tied[0].Value, ResolutionLevel.PropertyGroup, tied[0].Code, trace);
        }

        // 3. The customer's own value.
        trace.Add(new ResolutionStep(ResolutionLevel.Tenant, null, null, null, tenantValue, Verdict(tenantValue, answer)));
        if (answer is null && tenantValue is not null)
        {
            answer = new Resolution(ResolutionOutcome.Value, tenantValue, ResolutionLevel.Tenant, null, trace);
        }

        // 4. The registry's default.
        trace.Add(new ResolutionStep(ResolutionLevel.SystemDefault, null, null, null, setting.SystemDefault, Verdict(setting.SystemDefault, answer)));
        if (answer is null && setting.SystemDefault is not null)
        {
            answer = new Resolution(ResolutionOutcome.Value, setting.SystemDefault, ResolutionLevel.SystemDefault, null, trace);
        }

        return answer ?? new Resolution(ResolutionOutcome.Unset, null, null, null, trace);
    }

    private static ResolutionVerdict Verdict(string? value, Resolution? answer)
        => value is null ? ResolutionVerdict.NotSet : answer is null ? ResolutionVerdict.Won : ResolutionVerdict.Overridden;
}
