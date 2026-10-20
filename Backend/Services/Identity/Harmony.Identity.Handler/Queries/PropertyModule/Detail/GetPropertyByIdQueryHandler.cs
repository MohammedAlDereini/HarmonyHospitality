using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Models.PropertyModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Handler.Queries.PropertyModule.Detail;

/// <summary>A hotel with its lines and the computed facts, for the platform console. A caller outside the platform sees nothing (00073).</summary>
public class GetPropertyByIdQueryHandler : IRequestHandler<GetPropertyByIdQuery, CallResponse<PropertyModel>>
{
    private readonly IPropertyRepository properties;
    private readonly ITenantRepository tenants;
    private readonly ICallContext callContext;
    private readonly TimeProvider clock;
    private readonly Guid platformTenantId;

    public GetPropertyByIdQueryHandler(IPropertyRepository properties, ITenantRepository tenants, ICallContext callContext, IOptions<PlatformTenantSettings> platform, TimeProvider clock)
    {
        this.properties = properties;
        this.tenants = tenants;
        this.callContext = callContext;
        this.clock = clock;
        this.platformTenantId = platform.Value.TenantId;
    }

    public async Task<CallResponse<PropertyModel>> Handle(GetPropertyByIdQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(this.callContext.TenantId(), out var caller) || caller != this.platformTenantId)
        {
            return Refuse(BusinessErrorCodes.Identity.Tenant.PlatformOnly);
        }

        var model = await this.properties.QueryAcrossTenants()
            .Where(p => p.Id == request.Id)
            .Select(PropertyProjections.Model)
            .FirstOrDefaultAsync(cancellationToken);
        if (model is null)
        {
            return Refuse(BusinessErrorCodes.Identity.Property.NotFound);
        }

        // The lines live on the customer; only this hotel's are shown here.
        var lines = await this.tenants.Query()
            .Where(t => t.Id == model.TenantId)
            .SelectMany(t => t.Subscriptions)
            .Where(s => s.PropertyId == model.Id)
            .OrderBy(s => s.ModuleCode)
            .Select(PropertyProjections.Line)
            .ToListAsync(cancellationToken);

        model.Contacts = await this.properties.QueryAcrossTenants()
            .Where(p => p.Id == model.Id)
            .SelectMany(p => p.Contacts)
            .OrderByDescending(c => c.IsPrimary).ThenBy(c => c.Name)
            .Select(PropertyProjections.Contact)
            .ToListAsync(cancellationToken);

        PropertyProjections.Complete(model, lines, this.clock.GetUtcNow());
        return CallResponseBuilder.CreateResponse<PropertyModel>(eCallResponseStatus.Success).HasData(model);
    }

    private static CallResponse<PropertyModel> Refuse(string code)
        => CallResponseBuilder.CreateResponse<PropertyModel>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(code));
}
