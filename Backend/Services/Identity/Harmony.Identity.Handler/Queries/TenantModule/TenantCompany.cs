using Harmony.Core.Identity.Subscriptions;
using Harmony.Identity.Domain.Entities.Aggregates.EnrolmentModule;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Harmony.Identity.Domain.Models.TenantModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.TenantModule;

/// <summary>
/// What the console says about a customer beside its row (C2) and at the head of its page (C3): the hotels, the rooms
/// declared against the rooms licensed today, the modules with a line, the people, and the administrator with the
/// state of the invitation. A few reads for the whole page, never one per row; the detail adds the administrator's
/// two-step and last sign-in.
/// </summary>
internal static class TenantCompany
{
    public static async Task DescribeAsync(
        IList<TenantModel> rows,
        bool inFull,
        DateTimeOffset now,
        ITenantRepository tenants,
        IPropertyRepository properties,
        IUserAccountRepository userAccounts,
        ILocalStaffEnrolmentRepository enrolments,
        IOperatorSessionRepository sessions,
        CancellationToken cancellationToken)
    {
        var ids = rows.Select(r => r.Id).ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var today = DateOnly.FromDateTime(now.UtcDateTime);

        var hotels = await properties.QueryAcrossTenants()
            .Where(p => ids.Contains(p.TenantId))
            .Select(p => new { p.Id, p.TenantId, p.TotalRooms, p.IsActive })
            .ToListAsync(cancellationToken);

        var lines = await tenants.Query()
            .Where(t => ids.Contains(t.Id))
            .SelectMany(t => t.Subscriptions)
            .Select(s => new { s.TenantId, s.PropertyId, s.ModuleCode, s.IsEnabled, s.RenewsAutomatically, s.Rooms, s.Terminals, s.StartsOn, s.EndsOn })
            .ToListAsync(cancellationToken);

        // Licensed rooms follow the one definition every service uses: the hotel's largest line held today.
        var licensedByHotel = lines
            .Where(s => s.PropertyId != null && s.Rooms != null)
            .Where(s => new SubscriptionLine(s.PropertyId, s.ModuleCode, s.IsEnabled, s.RenewsAutomatically, s.Rooms, s.Terminals, s.StartsOn, s.EndsOn).IsHeldOn(today))
            .GroupBy(s => s.PropertyId!.Value)
            .ToDictionary(g => g.Key, g => g.Max(s => s.Rooms!.Value));

        var people = await userAccounts.QueryAcrossTenants()
            .Where(u => ids.Contains(u.TenantId))
            .GroupBy(u => u.TenantId)
            .Select(g => new
            {
                TenantId = g.Key,
                Total = g.Count(),
                WithPassword = g.Count(u => u.PasswordSignIn),
                Exceptions = g.Count(u => u.TwoStepExemptReason != null),
            })
            .ToListAsync(cancellationToken);

        var administrators = await userAccounts.QueryAcrossTenants()
            .Where(u => ids.Contains(u.TenantId) && u.UserRoles.Any(r => r.Role.TenantId == u.TenantId && r.Role.Code == SystemRoles.Administrator))
            .Select(u => new { u.Id, u.TenantId, u.DisplayName, u.Email, u.State, u.CreatedDate, u.IsMfaEnrolled, u.TwoStepMethod })
            .ToListAsync(cancellationToken);

        var administratorIds = administrators.Select(a => a.Id).ToList();
        var invitations = administratorIds.Count == 0
            ? []
            : await enrolments.QueryAcrossTenants()
                .Where(e => administratorIds.Contains(e.UserAccountId))
                .Select(e => new { e.UserAccountId, e.State, e.ExpiresAtUtc, e.CreatedDate })
                .ToListAsync(cancellationToken);

        foreach (var row in rows)
        {
            var mine = hotels.Where(h => h.TenantId == row.Id).ToList();
            row.Hotels = mine.Count;
            row.RoomsInUse = mine.Where(h => h.IsActive).Sum(h => h.TotalRooms);
            row.LicensedRooms = mine.Sum(h => licensedByHotel.TryGetValue(h.Id, out var licensed) ? licensed : 0);
            row.ModuleCodes = lines
                .Where(l => l.TenantId == row.Id && l.IsEnabled)
                .Select(l => l.ModuleCode)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var crowd = people.FirstOrDefault(p => p.TenantId == row.Id);
            row.People = crowd?.Total ?? 0;
            row.PeopleWithPassword = crowd?.WithPassword ?? 0;
            row.TwoStepExceptions = crowd?.Exceptions ?? 0;

            var administrator = administrators.Where(a => a.TenantId == row.Id).OrderBy(a => a.CreatedDate).FirstOrDefault();
            if (administrator is null)
            {
                continue;
            }

            var invitation = invitations.Where(i => i.UserAccountId == administrator.Id).OrderByDescending(i => i.CreatedDate).FirstOrDefault();
            var model = new TenantAdministratorModel
            {
                UserAccountId = administrator.Id,
                DisplayName = administrator.DisplayName,
                Email = administrator.Email,
                State = administrator.State,
                InvitationPending = invitation is { State: EnrolmentState.Pending } && invitation.ExpiresAtUtc > now,
                InvitationExpired = invitation is { State: EnrolmentState.Pending } && invitation.ExpiresAtUtc <= now,
                InvitedOn = invitation?.CreatedDate,
                TwoStepEnrolled = administrator.IsMfaEnrolled,
                TwoStepMethod = administrator.TwoStepMethod.ToString(),
            };

            if (inFull)
            {
                var latest = await sessions.LatestOfAccountAsync(row.Id, administrator.Id, 1, cancellationToken);
                var session = latest.FirstOrDefault();
                model.LastSignInUtc = session?.StartedAtUtc;
                model.LastSignInMethods = session?.Methods;
            }

            row.Administrator = model;
        }
    }
}
