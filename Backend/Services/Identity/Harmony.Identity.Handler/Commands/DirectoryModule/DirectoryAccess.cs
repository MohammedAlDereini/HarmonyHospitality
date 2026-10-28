namespace Harmony.Identity.Handler.Commands.DirectoryModule;

using Harmony.Core.Identity.Permissions;
using Harmony.Identity.Domain.Entities.Aggregates.DirectoryModule;
using Harmony.Identity.Domain.Entities.Aggregates.EnrolmentModule;
using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

/// <summary>A directory person whose access was worked out again: what to publish once the save is done.</summary>
public sealed record RebuiltAccess(User User, int VersionBefore, bool Changed, bool FirstAccess);

/// <summary>
/// "The group is the gate" (stage E, the Mews way). A company directory person holds exactly what their groups give:
/// each group with a role gives that role, at its hotel, its hotel group, or (no place) at every hotel. Whenever a
/// person's groups, a group's role or the person's state changes, their access is worked out again here; nothing is
/// handed out by hand. The first time a person gets any access and has no Harmony password yet, the sign-up mail goes
/// out (the password is the fallback when the company sign-in is off or broken, as in Mews).
/// </summary>
public sealed class DirectoryAccess
{
    private const string DirectoryReason = "from the company directory's groups";

    private readonly IUserAccountRepository userAccounts;
    private readonly IDirectoryGroupRepository groups;
    private readonly ISecurityAuditEventRepository auditEvents;
    private readonly ISecurityEventFactory securityEvents;
    private readonly ICacheService cache;
    private readonly ISessionService sessions;
    private readonly UserManager<User> userManager;
    private readonly IAccountMailer mailer;
    private readonly IFederatedAuthorityRepository authorities;
    private readonly ILocalStaffEnrolmentRepository enrolments;
    private readonly TimeProvider clock;
    private readonly ILogger<DirectoryAccess> logger;

    public DirectoryAccess(
        IUserAccountRepository userAccounts,
        IDirectoryGroupRepository groups,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        ICacheService cache,
        ISessionService sessions,
        UserManager<User> userManager,
        IAccountMailer mailer,
        IFederatedAuthorityRepository authorities,
        ILocalStaffEnrolmentRepository enrolments,
        TimeProvider clock,
        ILogger<DirectoryAccess> logger)
    {
        this.userAccounts = userAccounts;
        this.groups = groups;
        this.auditEvents = auditEvents;
        this.securityEvents = securityEvents;
        this.cache = cache;
        this.sessions = sessions;
        this.userManager = userManager;
        this.mailer = mailer;
        this.authorities = authorities;
        this.enrolments = enrolments;
        this.clock = clock;
        this.logger = logger;
    }

    /// <summary>The trail's actor for a directory's change: "directory:" and its client id.</summary>
    public static string ActorOf(DirectoryCaller caller) => $"directory:{caller.ClientId}";

    /// <summary>
    /// Works out one person's access again and stages the change (saved by the caller's next save). <paramref name="changed"/>
    /// is a group changed in memory and not saved yet: its members and role are taken from memory, and a group being
    /// deleted (<paramref name="changedGone"/>) gives nothing. Null for an account that is not a live directory person.
    /// </summary>
    public async Task<RebuiltAccess?> RebuildAsync(Guid userId, DirectoryGroup? changed, bool changedGone, string actor, CancellationToken cancellationToken)
    {
        var user = await this.userAccounts.GetWithAccessAsync(userId, cancellationToken);
        if (user is not { IsDirectoryManaged: true } || user.State == AdministrativeState.Terminated)
        {
            return null;
        }

        var giving = (await this.groups.MappedOfMemberAsync(userId, cancellationToken)).Where(g => g.Id != changed?.Id).ToList();
        if (changed is { RoleId: not null } && !changedGone && changed.Members.Any(m => m.UserAccountId == userId))
        {
            giving.Add(changed);
        }

        return await this.ApplyAsync(user, giving, actor, cancellationToken);
    }

    /// <summary>The same for a person already loaded with their access (a rehire: turned back on).</summary>
    public async Task<RebuiltAccess> RebuildAsync(User user, string actor, CancellationToken cancellationToken)
        => await this.ApplyAsync(user, (await this.groups.MappedOfMemberAsync(user.Id, cancellationToken)).ToList(), actor, cancellationToken);

    /// <summary>After the save: the new version is published (old tokens die), hotel copies go, first access gets the sign-up mail.</summary>
    public async Task FinishAsync(IEnumerable<RebuiltAccess?> rebuilt, CancellationToken cancellationToken)
    {
        foreach (var access in rebuilt.OfType<RebuiltAccess>())
        {
            await this.PublishAsync(access.User, access.VersionBefore, cancellationToken);
            if (access.Changed)
            {
                await this.cache.DeleteByPrefixAsync(PermissionCacheKeys.HotelRolePrefixOf(access.User.TenantId, access.User.Id), cancellationToken);
            }

            if (access.FirstAccess && access.User.PasswordHash is null && access.User.State == AdministrativeState.Active)
            {
                await this.InviteAsync(access.User, cancellationToken);
            }
        }
    }

    /// <summary>A moved version is published at once, and every session of the person ends with it.</summary>
    public async Task PublishAsync(User user, int versionBefore, CancellationToken cancellationToken)
    {
        if (user.SecurityVersion == versionBefore)
        {
            return;
        }

        await this.cache.AddAsync(SecurityVersionCacheKeys.UserVersionKey(user.TenantId, user.Id), user.SecurityVersion, SecurityVersionCacheKeys.UserVersionLifetime, cancellationToken);
        await this.sessions.EndAllAsync(user.Id, cancellationToken);
    }

    private async Task<RebuiltAccess> ApplyAsync(User user, IReadOnlyList<DirectoryGroup> giving, string actor, CancellationToken cancellationToken)
    {
        var now = this.clock.GetUtcNow();
        var before = user.SecurityVersion;
        var hadAccess = user.UserRoles.Count > 0 || user.RoleAssignments.Any(a => a.IsCurrentAt(now));

        // Every hotel: tenant-wide roles, which travel in the token (a change bumps the version).
        var everywhere = giving.Where(g => g.Scope is null).Select(g => g.RoleId!.Value).Distinct().ToList();
        user.UpdateRoles(everywhere);
        var rolesChanged = user.SecurityVersion != before;
        if (rolesChanged)
        {
            await this.auditEvents.AddAsync(this.securityEvents.CreateBy(user, SecurityAuditActions.RolesChanged, actor, DirectoryReason), cancellationToken);
        }

        // One hotel or one hotel group: assignments, read per call (no version bump, the hotel copies go after the save).
        var placed = giving.Where(g => g.Scope is not null).Select(g => (Role: g.RoleId!.Value, Scope: g.Scope!)).Distinct().ToList();
        var assignmentsChanged = false;
        foreach (var assignment in user.RoleAssignments.Where(a => a.IsCurrentAt(now)).ToList())
        {
            if (!placed.Any(p => p.Role == assignment.RoleId && p.Scope == assignment.Scope))
            {
                user.EndAssignment(assignment.Id, now);
                await this.auditEvents.AddAsync(this.securityEvents.CreateBy(user, SecurityAuditActions.RoleAssignmentEnded, actor, DirectoryReason), cancellationToken);
                assignmentsChanged = true;
            }
        }

        foreach (var (role, scope) in placed)
        {
            if (!user.RoleAssignments.Any(a => a.RoleId == role && a.Scope == scope && a.IsCurrentAt(now)))
            {
                user.AssignRole(role, scope, null, now);
                await this.auditEvents.AddAsync(this.securityEvents.CreateBy(user, SecurityAuditActions.RoleAssigned, actor, DirectoryReason), cancellationToken);
                assignmentsChanged = true;
            }
        }

        var hasAccess = everywhere.Count > 0 || placed.Count > 0;
        return new RebuiltAccess(user, before, rolesChanged || assignmentsChanged, !hadAccess && hasAccess);
    }

    /// <summary>
    /// The sign-up mail with its one-time link (24 hours, bound to the account's stamp), recorded like every invitation.
    /// The directory sent the person, so the record names the person as their own sponsor. A failed mail only logs: the
    /// person still signs in with the company, and the administrator can send the invitation again.
    /// </summary>
    private async Task InviteAsync(User user, CancellationToken cancellationToken)
    {
        var now = this.clock.GetUtcNow();
        if (await this.enrolments.LatestOfAccountAsync(user.Id, cancellationToken) is { } open && open.IsOpen(now))
        {
            return;
        }

        var token = await this.userManager.GeneratePasswordResetTokenAsync(user);
        try
        {
            await this.mailer.SendInvitationAsync(user, token, cancellationToken);
        }
        catch (Exception exception)
        {
            this.logger.LogWarning(exception, "The sign-up mail for directory account {UserId} could not be sent.", user.Id);
            return;
        }

        var local = (await this.authorities.OfTenantAsync(user.TenantId, cancellationToken)).First(a => a.Scheme == AuthorityScheme.Local);
        await this.enrolments.AddAsync(LocalStaffEnrolment.Invite(user.TenantId, user.Id, local.Id, user.Id, user.Email!, now), cancellationToken);
        await this.enrolments.SaveChangesAsync(cancellationToken);
    }
}
