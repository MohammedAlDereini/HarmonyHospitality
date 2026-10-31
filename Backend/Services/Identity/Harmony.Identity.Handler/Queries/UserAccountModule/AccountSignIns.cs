using Harmony.Identity.Domain.Models.UserAccountModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.UserAccountModule;

/// <summary>
/// What the console says about an account's sign-ins beside its row (C17) and at the head of its page (C18): the newest
/// sign-in and the device seen last. One read of the sessions and one of the devices for the whole page.
/// </summary>
internal static class AccountSignIns
{
    public static async Task DescribeAsync(
        IList<UserAccountModel> rows,
        Guid tenantId,
        IOperatorSessionRepository sessions,
        IUserDeviceRepository devices,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return;
        }

        var ids = rows.Select(r => r.Id).ToList();
        var latest = await sessions.LatestStartsAsync(tenantId, ids, cancellationToken);
        var seen = await devices.Query()
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId && ids.Contains(d.UserAccountId))
            .Select(d => new { d.UserAccountId, d.Label, d.LastSeenUtc })
            .ToListAsync(cancellationToken);
        var lastDevice = seen
            .GroupBy(d => d.UserAccountId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(d => d.LastSeenUtc).First().Label);

        foreach (var row in rows)
        {
            row.LastSignInUtc = latest.TryGetValue(row.Id, out var at) ? at : null;
            row.LastSignInDevice = lastDevice.GetValueOrDefault(row.Id);
        }
    }
}
