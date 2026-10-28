namespace Harmony.Identity.Handler.Commands.DirectoryModule;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.DirectoryModule;
using Harmony.Identity.Domain.Entities.Aggregates.RoleModule;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Models.DirectoryModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Handler.Commands.ApprovalModule;
using Harmony.Identity.Handler.Commands.TenantModule;
using Harmony.Identity.Infrastructure.Signing;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

/// <summary>The customer's administrator opens the directory connection (201): the secret is shown this once.</summary>
public class OpenDirectoryConnectionCommand : BaseCommandRequest<CallResponse<DirectorySecretModel>>
{
}

/// <summary>A new secret; the old one and every token made from it stop working now. Shown this once.</summary>
public class NewDirectorySecretCommand : BaseCommandRequest<CallResponse<DirectorySecretModel>>
{
}

/// <summary>On or off. Off refuses the directory; its people stay read-only in Harmony.</summary>
public class SwitchDirectoryCommand : BaseCommandRequest<CallResponse>
{
    public bool On { get; set; }
}

/// <summary>
/// The platform removes a customer's connection on purpose (Mews: Customer Success does it). Its people are handed back
/// to the customer's administrators with the access they hold now; its groups go.
/// </summary>
public class RemoveDirectoryConnectionCommand : BaseCommandRequest<CallResponse>
{
    public Guid TenantId { get; set; }

    public class RemoveDirectoryConnectionCommandValidation : AbstractValidator<RemoveDirectoryConnectionCommand>
    {
        public RemoveDirectoryConnectionCommandValidation()
        {
            this.RuleFor(e => e.TenantId).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.IdRequired);
        }
    }
}

/// <summary>
/// The role a directory group gives, and where: one hotel, one hotel group, or neither (every hotel). No role: the group
/// gives nothing. Its members' access is worked out again at once.
/// </summary>
public class MapDirectoryGroupCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public Guid? RoleId { get; set; }

    public Guid? PropertyId { get; set; }

    public Guid? PropertyGroupId { get; set; }

    public class MapDirectoryGroupCommandValidation : AbstractValidator<MapDirectoryGroupCommand>
    {
        public MapDirectoryGroupCommandValidation()
        {
            this.RuleFor(e => e.Id).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Directory.GroupIdRequired);
            this.RuleFor(e => e).Must(e => e.PropertyId is null || e.PropertyGroupId is null).WithErrorCode(ModelValidationErrorCodes.Identity.Directory.OneScopeOnly);
            this.RuleFor(e => e).Must(e => e.RoleId is not null || (e.PropertyId is null && e.PropertyGroupId is null)).WithErrorCode(ModelValidationErrorCodes.Identity.Directory.RoleRequiredForScope);
        }
    }
}

/// <summary>The customer's days without a sign-in before an account is suspended: 1 to 90 (PCI DSS 8.2.6 sets the 90).</summary>
public class SetInactivityDaysCommand : BaseCommandRequest<CallResponse>
{
    public int Days { get; set; }

    public class SetInactivityDaysCommandValidation : AbstractValidator<SetInactivityDaysCommand>
    {
        public SetInactivityDaysCommandValidation()
        {
            this.RuleFor(e => e.Days).InclusiveBetween(1, Domain.Entities.Aggregates.TenantModule.Tenant.MaxInactivityDays)
                .WithErrorCode(ModelValidationErrorCodes.Identity.Directory.InactivityDaysOutOfRange);
        }
    }
}

/// <summary>The platform runs the nightly inactivity rule now; the answer is how many accounts it suspended.</summary>
public class RunInactivityRuleCommand : BaseCommandRequest<CallResponse<int>>
{
}

/// <summary>What the directory doors share: the caller's customer is the wall, the trail lands on the caller.</summary>
public abstract class DirectoryCommandHandlerBase : TenantCommandHandlerBase
{
    protected DirectoryCommandHandlerBase(
        IProvisioningConnectionRepository connections,
        ITenantRepository tenants,
        IUserAccountRepository userAccounts,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        ICallContext callContext,
        IOptions<PlatformTenantSettings> platform,
        IOptions<TokenIssuerSettings> issuer,
        TimeProvider clock)
        : base(tenants, userAccounts, auditEvents, securityEvents, callContext, platform)
    {
        this.Connections = connections;
        this.Issuer = issuer.Value.Issuer.TrimEnd('/');
        this.Clock = clock;
    }

    protected IProvisioningConnectionRepository Connections { get; }

    /// <summary>Identity's public address (the token issuer); the SCIM doors live under it.</summary>
    protected string Issuer { get; }

    protected TimeProvider Clock { get; }

    protected Guid CallerTenant => Guid.Parse(this.CallContext.TenantId());

    public static string ScimUrlOf(string issuer) => $"{issuer}/scim/v2";

    public static string TokenUrlOf(string issuer) => $"{issuer}/scim/token";

    protected DirectorySecretModel Shown(ProvisioningConnection connection, string secret) => new()
    {
        ClientId = connection.ClientId,
        Secret = secret,
        SecretToken = $"{connection.ClientId}.{secret}",
        ScimUrl = ScimUrlOf(this.Issuer),
        TokenUrl = TokenUrlOf(this.Issuer),
    };
}

public class OpenDirectoryConnectionCommandHandler : DirectoryCommandHandlerBase, IRequestHandler<OpenDirectoryConnectionCommand, CallResponse<DirectorySecretModel>>
{
    public OpenDirectoryConnectionCommandHandler(IProvisioningConnectionRepository connections, ITenantRepository tenants, IUserAccountRepository userAccounts, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, ICallContext callContext, IOptions<PlatformTenantSettings> platform, IOptions<TokenIssuerSettings> issuer, TimeProvider clock)
        : base(connections, tenants, userAccounts, auditEvents, securityEvents, callContext, platform, issuer, clock)
    {
    }

    public async Task<CallResponse<DirectorySecretModel>> Handle(OpenDirectoryConnectionCommand command, CancellationToken cancellationToken)
    {
        if (await this.Connections.OfTenantAsync(this.CallerTenant, cancellationToken) is not null)
        {
            return Fail<DirectorySecretModel>(BusinessErrorCodes.Identity.Directory.ConnectionExists);
        }

        var secret = ProvisioningConnection.NewSecretValue();
        var connection = ProvisioningConnection.Open(this.CallerTenant, ProvisioningConnection.NewClientId(), ProvisioningConnection.HashOf(secret), this.Clock.GetUtcNow());
        await this.Connections.AddAsync(connection, cancellationToken);
        await this.TrailAsync(SecurityAuditActions.DirectoryConnectionOpened, connection.ClientId, cancellationToken);
        await this.Connections.SaveChangesAsync(cancellationToken);
        return CallResponseBuilder.CreateResponse<DirectorySecretModel>(eCallResponseStatus.Created).HasData(this.Shown(connection, secret));
    }
}

public class NewDirectorySecretCommandHandler : DirectoryCommandHandlerBase, IRequestHandler<NewDirectorySecretCommand, CallResponse<DirectorySecretModel>>
{
    public NewDirectorySecretCommandHandler(IProvisioningConnectionRepository connections, ITenantRepository tenants, IUserAccountRepository userAccounts, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, ICallContext callContext, IOptions<PlatformTenantSettings> platform, IOptions<TokenIssuerSettings> issuer, TimeProvider clock)
        : base(connections, tenants, userAccounts, auditEvents, securityEvents, callContext, platform, issuer, clock)
    {
    }

    public async Task<CallResponse<DirectorySecretModel>> Handle(NewDirectorySecretCommand command, CancellationToken cancellationToken)
    {
        var connection = await this.Connections.OfTenantAsync(this.CallerTenant, cancellationToken);
        if (connection is null)
        {
            return Fail<DirectorySecretModel>(BusinessErrorCodes.Identity.Directory.ConnectionNotFound);
        }

        var secret = ProvisioningConnection.NewSecretValue();
        connection.NewSecret(ProvisioningConnection.HashOf(secret), this.Clock.GetUtcNow());
        await this.TrailAsync(SecurityAuditActions.DirectorySecretReplaced, connection.ClientId, cancellationToken);
        await this.Connections.SaveChangesAsync(cancellationToken);
        return CallResponseBuilder.CreateResponse<DirectorySecretModel>(eCallResponseStatus.Success).HasData(this.Shown(connection, secret));
    }
}

public class SwitchDirectoryCommandHandler : DirectoryCommandHandlerBase, IRequestHandler<SwitchDirectoryCommand, CallResponse>
{
    public SwitchDirectoryCommandHandler(IProvisioningConnectionRepository connections, ITenantRepository tenants, IUserAccountRepository userAccounts, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, ICallContext callContext, IOptions<PlatformTenantSettings> platform, IOptions<TokenIssuerSettings> issuer, TimeProvider clock)
        : base(connections, tenants, userAccounts, auditEvents, securityEvents, callContext, platform, issuer, clock)
    {
    }

    public async Task<CallResponse> Handle(SwitchDirectoryCommand command, CancellationToken cancellationToken)
    {
        var connection = await this.Connections.OfTenantAsync(this.CallerTenant, cancellationToken);
        if (connection is null)
        {
            return Fail(BusinessErrorCodes.Identity.Directory.ConnectionNotFound);
        }

        if (connection.IsEnabled == command.On)
        {
            return Ok();
        }

        if (command.On)
        {
            connection.SwitchOn();
        }
        else
        {
            connection.SwitchOff();
        }

        await this.TrailAsync(command.On ? SecurityAuditActions.DirectorySwitchedOn : SecurityAuditActions.DirectorySwitchedOff, connection.ClientId, cancellationToken);
        await this.Connections.SaveChangesAsync(cancellationToken);
        return Ok();
    }
}

public class RemoveDirectoryConnectionCommandHandler : DirectoryCommandHandlerBase, IRequestHandler<RemoveDirectoryConnectionCommand, CallResponse>
{
    public RemoveDirectoryConnectionCommandHandler(IProvisioningConnectionRepository connections, ITenantRepository tenants, IUserAccountRepository userAccounts, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, ICallContext callContext, IOptions<PlatformTenantSettings> platform, IOptions<TokenIssuerSettings> issuer, TimeProvider clock)
        : base(connections, tenants, userAccounts, auditEvents, securityEvents, callContext, platform, issuer, clock)
    {
    }

    public async Task<CallResponse> Handle(RemoveDirectoryConnectionCommand command, CancellationToken cancellationToken)
    {
        if (!this.CallerIsPlatform)
        {
            return Fail(BusinessErrorCodes.Identity.Tenant.PlatformOnly);
        }

        var tenant = await this.Tenants.FindAsync(command.TenantId, cancellationToken);
        var connection = tenant is null ? null : await this.Connections.OfTenantAsync(tenant.Id, cancellationToken);
        if (connection is null)
        {
            return Fail(BusinessErrorCodes.Identity.Directory.ConnectionNotFound);
        }

        // Handed back with the access they hold now; the groups and their members go with the connection (cascade).
        foreach (var person in await this.UserAccounts.OfDirectoryAsync(connection.Id, cancellationToken))
        {
            person.LeaveDirectory();
        }

        await this.Connections.DeleteAsync(connection, cancellationToken);
        await this.TrailAsync(SecurityAuditActions.DirectoryConnectionRemoved, tenant!.Code, cancellationToken);
        await this.Connections.SaveChangesAsync(cancellationToken);
        return Ok();
    }
}

/// <summary>
/// The rank rules of the role doors hold here too: the role is the customer's and not above the caller's own standing,
/// the caller is not in the group, and nobody in it stands above the caller. An administrator-level role waits for a
/// second administrator when the customer requires approvals (stage D).
/// </summary>
public class MapDirectoryGroupCommandHandler : DirectoryCommandHandlerBase, IRequestHandler<MapDirectoryGroupCommand, CallResponse>
{
    private readonly IDirectoryGroupRepository groups;
    private readonly IRoleRepository roles;
    private readonly IPropertyRepository properties;
    private readonly IPropertyGroupRepository propertyGroups;
    private readonly IApprovalGate gate;
    private readonly DirectoryAccess access;

    public MapDirectoryGroupCommandHandler(IProvisioningConnectionRepository connections, ITenantRepository tenants, IUserAccountRepository userAccounts, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, ICallContext callContext, IOptions<PlatformTenantSettings> platform, IOptions<TokenIssuerSettings> issuer, TimeProvider clock, IDirectoryGroupRepository groups, IRoleRepository roles, IPropertyRepository properties, IPropertyGroupRepository propertyGroups, IApprovalGate gate, DirectoryAccess access)
        : base(connections, tenants, userAccounts, auditEvents, securityEvents, callContext, platform, issuer, clock)
    {
        this.groups = groups;
        this.roles = roles;
        this.properties = properties;
        this.propertyGroups = propertyGroups;
        this.gate = gate;
        this.access = access;
    }

    public async Task<CallResponse> Handle(MapDirectoryGroupCommand command, CancellationToken cancellationToken)
    {
        var connection = await this.Connections.OfTenantAsync(this.CallerTenant, cancellationToken);
        var group = connection is null ? null : await this.groups.WithMembersAsync(connection.Id, command.Id, cancellationToken);
        if (group is null)
        {
            return Fail(connection is null ? BusinessErrorCodes.Identity.Directory.ConnectionNotFound : BusinessErrorCodes.Identity.Directory.GroupNotFound);
        }

        var callerId = RankRules.CallerId(this.CallContext);
        var callerRank = await this.UserAccounts.RankOfAsync(callerId, cancellationToken);
        var rank = RoleRank.None;
        if (command.RoleId is { } roleId)
        {
            var role = await this.roles.Query().Where(r => r.Id == roleId).Select(r => new { r.PrivilegeLevel, r.IsSuperRole }).FirstOrDefaultAsync(cancellationToken);
            if (role is null)
            {
                return Fail(BusinessErrorCodes.Identity.Role.NotFound);
            }

            rank = RoleRank.Of(role.PrivilegeLevel, role.IsSuperRole);
            if (rank > callerRank)
            {
                return Fail(BusinessErrorCodes.Identity.Rank.AboveSelf);
            }
        }

        AssignmentScope? scope = command.PropertyId is null && command.PropertyGroupId is null ? null : AssignmentScope.Of(command.PropertyId, command.PropertyGroupId);
        if (scope is not null && !await this.InTenantAsync(scope, cancellationToken))
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.ScopeNotOfTenant);
        }

        var members = group.Members.Select(m => m.UserAccountId).ToList();
        if (members.Contains(callerId))
        {
            return Fail(BusinessErrorCodes.Identity.Rank.OwnAccount);
        }

        foreach (var member in members)
        {
            if (await this.UserAccounts.RankOfAsync(member, cancellationToken) > callerRank)
            {
                return Fail(BusinessErrorCodes.Identity.Rank.HigherRank);
            }
        }

        if (group.RoleId == command.RoleId && group.Scope == scope)
        {
            return Ok();
        }

        if (rank >= RoleRank.Administrator
            && await this.gate.HoldAsync(ApprovalKinds.DirectoryAdministratorGroup, command, null, [], cancellationToken) is { } hold)
        {
            return Fail(hold);
        }

        group.MapTo(command.RoleId, scope);
        var actor = callerId.ToString("D");
        var rebuilt = new List<RebuiltAccess?>();
        foreach (var member in members)
        {
            rebuilt.Add(await this.access.RebuildAsync(member, group, changedGone: false, actor, cancellationToken));
        }

        await this.TrailAsync(SecurityAuditActions.DirectoryGroupMapped, group.DisplayName, cancellationToken);
        await this.groups.SaveChangesAsync(cancellationToken);
        await this.access.FinishAsync(rebuilt, cancellationToken);
        return Ok();
    }

    private async Task<bool> InTenantAsync(AssignmentScope scope, CancellationToken cancellationToken)
        => scope.IsHotel
            ? await this.properties.Query().AnyAsync(p => p.Id == scope.PropertyId, cancellationToken)
            : await this.propertyGroups.Query().AnyAsync(g => g.Id == scope.PropertyGroupId, cancellationToken);
}

public class SetInactivityDaysCommandHandler : DirectoryCommandHandlerBase, IRequestHandler<SetInactivityDaysCommand, CallResponse>
{
    public SetInactivityDaysCommandHandler(IProvisioningConnectionRepository connections, ITenantRepository tenants, IUserAccountRepository userAccounts, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, ICallContext callContext, IOptions<PlatformTenantSettings> platform, IOptions<TokenIssuerSettings> issuer, TimeProvider clock)
        : base(connections, tenants, userAccounts, auditEvents, securityEvents, callContext, platform, issuer, clock)
    {
    }

    public async Task<CallResponse> Handle(SetInactivityDaysCommand command, CancellationToken cancellationToken)
    {
        var tenant = await this.Tenants.FindAsync(this.CallerTenant, cancellationToken);
        if (tenant is null)
        {
            return Fail(BusinessErrorCodes.Identity.Tenant.NotFound);
        }

        if (tenant.InactivityDays == command.Days)
        {
            return Ok();
        }

        tenant.SuspendWhenInactiveFor(command.Days);
        await this.TrailAsync(SecurityAuditActions.InactivityDaysSet, command.Days.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken);
        await this.AuditEvents.SaveChangesAsync(cancellationToken);
        return Ok();
    }
}

public class RunInactivityRuleCommandHandler : DirectoryCommandHandlerBase, IRequestHandler<RunInactivityRuleCommand, CallResponse<int>>
{
    private readonly IInactivitySuspension rule;

    public RunInactivityRuleCommandHandler(IProvisioningConnectionRepository connections, ITenantRepository tenants, IUserAccountRepository userAccounts, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, ICallContext callContext, IOptions<PlatformTenantSettings> platform, IOptions<TokenIssuerSettings> issuer, TimeProvider clock, IInactivitySuspension rule)
        : base(connections, tenants, userAccounts, auditEvents, securityEvents, callContext, platform, issuer, clock)
    {
        this.rule = rule;
    }

    public async Task<CallResponse<int>> Handle(RunInactivityRuleCommand command, CancellationToken cancellationToken)
        => this.CallerIsPlatform
            ? CallResponseBuilder.CreateResponse<int>(eCallResponseStatus.Success).HasData(await this.rule.RunAsync(cancellationToken))
            : Fail<int>(BusinessErrorCodes.Identity.Tenant.PlatformOnly);
}
