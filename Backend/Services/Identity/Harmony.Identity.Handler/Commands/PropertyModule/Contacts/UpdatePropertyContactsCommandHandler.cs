namespace Harmony.Identity.Handler.Commands.PropertyModule.Contacts;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Handler.Commands.TenantModule;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

/// <summary>
/// The hotel's contact list, by its own people: the wall finds the hotel with its contacts (another customer's is
/// 00092, no hint), the aggregate reconciles the list (an id that is not this hotel's is 00095, nothing written),
/// the trail, one save. A removed contact is an orphan of a required parent: EF deletes the row at the save.
/// </summary>
public class UpdatePropertyContactsCommandHandler : TenantCommandHandlerBase, IRequestHandler<UpdatePropertyContactsCommand, CallResponse>
{
    private readonly IPropertyRepository properties;

    public UpdatePropertyContactsCommandHandler(
        ITenantRepository tenants,
        IPropertyRepository properties,
        IUserAccountRepository userAccounts,
        ISecurityAuditEventRepository auditEvents,
        ISecurityEventFactory securityEvents,
        ICallContext callContext,
        IOptions<PlatformTenantSettings> platform)
        : base(tenants, userAccounts, auditEvents, securityEvents, callContext, platform)
    {
        this.properties = properties;
    }

    public async Task<CallResponse> Handle(UpdatePropertyContactsCommand command, CancellationToken cancellationToken)
    {
        var property = await this.properties.FindWithContactsAsync(command.PropertyId, cancellationToken);
        if (property is null)
        {
            return Fail(BusinessErrorCodes.Identity.Property.NotFound);
        }

        var wanted = command.Contacts
            .Select(c => new Property.ContactRequest(c.Id, c.Name, c.Role, c.Email, c.Telephone, c.IsPrimary))
            .ToList();
        if (property.SetContacts(wanted) is not null)
        {
            return Fail(BusinessErrorCodes.Identity.Property.ContactNotFound);
        }

        await this.TrailAsync(SecurityAuditActions.PropertyContactsChanged, $"{property.Code}:{property.Contacts.Count}", cancellationToken);
        await this.Tenants.SaveChangesAsync(cancellationToken);
        return Ok();
    }
}
