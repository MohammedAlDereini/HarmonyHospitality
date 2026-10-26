namespace Harmony.Identity.Handler.Commands.PropertyGroupModule.Create;

using Harmony.Core.Abstractions;
using Harmony.Core.Exceptions;
using Harmony.Identity.Domain.Entities.Aggregates.PropertyGroupModule;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Every gate refuses before anything is written. Then one save: the group (inside the customer's tenant, its
/// TenantId set explicitly) and the trail. Two console users racing on the same code: the index wins, and the base
/// context's "Conflict" becomes the readable refusal.
/// </summary>
public class CreatePropertyGroupCommandHandler : PropertyGroupCommandHandlerBase, IRequestHandler<CreatePropertyGroupCommand, CallResponse<Guid>>
{
    private readonly ILogger<CreatePropertyGroupCommandHandler> logger;

    public CreatePropertyGroupCommandHandler(
        ITenantRepository tenants,
        IPropertyGroupRepository groups,
        IPropertyGroupTypeRepository groupTypes,
        IUserAccountRepository userAccounts,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        ICallContext callContext,
        IOptions<PlatformTenantSettings> platform,
        ILogger<CreatePropertyGroupCommandHandler> logger)
        : base(tenants, groups, groupTypes, userAccounts, auditEvents, securityEvents, callContext, platform)
    {
        this.logger = logger;
    }

    public async Task<CallResponse<Guid>> Handle(CreatePropertyGroupCommand command, CancellationToken cancellationToken)
    {
        // 1. Platform only, never aimed at the platform's own tenant.
        if (!this.CallerIsPlatform || this.IsPlatformTenant(command.TenantId))
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Tenant.PlatformOnly);
        }

        // 2. The customer exists and is active.
        var tenant = await this.Tenants.FindAsync(command.TenantId, cancellationToken);
        if (tenant is null)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Tenant.NotFound);
        }

        if (tenant.Status != TenantStatus.Active)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Tenant.NotActive);
        }

        // 3. The type is a row of PropertyGroupTypes.
        var type = await this.GroupTypes.GetByCodeAsync(command.TypeCode.Trim(), cancellationToken);
        if (type is null)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.Reference.NotFound);
        }

        // 4. One code per customer: the first answer; the unique index is the last.
        var code = command.Code.Trim().ToUpperInvariant();
        if (await this.Groups.CodeExistsInTenantAsync(tenant.Id, code, cancellationToken))
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.PropertyGroup.CodeInUse);
        }

        // 5. Brand detail only on a Brand group: refused here, before any write.
        var isBrand = string.Equals(type.Code, PropertyGroupTypeRef.BrandCode, StringComparison.OrdinalIgnoreCase);
        if (command.Brand is not null && !isBrand)
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.PropertyGroup.BrandDetailNotAllowed);
        }

        // 6. The group first, so the context assigns its id; then its brand detail (it copies the id); then the trail. One save.
        var group = PropertyGroup.Create(
            tenant.Id,
            type.Code,
            code,
            command.NameEn,
            command.NameAr,
            command.Description,
            new Precedence(command.Precedence),
            command.DisplayOrder,
            command.ExternalIdentifier);
        await this.Groups.AddAsync(group, cancellationToken);
        group.SetBrandDetail(command.Brand?.ToDomain());
        await this.TrailAsync(SecurityAuditActions.PropertyGroupCreated, group.Code, cancellationToken);

        try
        {
            await this.Groups.SaveChangesAsync(cancellationToken);
        }
        catch (BusinessException exception) when (exception.Errors.Any(error => error.Code == ConflictCode))
        {
            return Fail<Guid>(BusinessErrorCodes.Identity.PropertyGroup.CodeInUse);
        }

        this.logger.LogInformation("Property group {Code} ({Type}) created for tenant {Tenant}.", group.Code, group.TypeCode, tenant.Code);
        return CallResponseBuilder.CreateResponse<Guid>(eCallResponseStatus.Created).HasData(group.Id);
    }
}
