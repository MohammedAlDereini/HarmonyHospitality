namespace Harmony.Identity.Handler.Commands.PropertyGroupModule;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Handler.Commands.TenantModule;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

/// <summary>
/// What the group doors share with the tenant doors: platform-only, the trail on the acting platform person, and the
/// group rows. Extends the tenant base so the gates are written once.
/// </summary>
public abstract class PropertyGroupCommandHandlerBase : TenantCommandHandlerBase
{
    /// <summary>The code the base context gives every unique-index violation; on these doors it can only be the group code.</summary>
    protected const string ConflictCode = "Conflict";

    protected PropertyGroupCommandHandlerBase(
        ITenantRepository tenants,
        IPropertyGroupRepository groups,
        IPropertyGroupTypeRepository groupTypes,
        IUserAccountRepository userAccounts,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        ICallContext callContext,
        IOptions<PlatformTenantSettings> platform)
        : base(tenants, userAccounts, auditEvents, securityEvents, callContext, platform)
    {
        this.Groups = groups;
        this.GroupTypes = groupTypes;
    }

    protected IPropertyGroupRepository Groups { get; }

    protected IPropertyGroupTypeRepository GroupTypes { get; }
}
