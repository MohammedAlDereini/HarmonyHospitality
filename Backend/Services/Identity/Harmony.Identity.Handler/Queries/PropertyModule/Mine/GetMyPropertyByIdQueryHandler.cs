using Harmony.Identity.Domain.Models.PropertyModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.PropertyModule.Mine;

/// <summary>One of the caller's hotels with its lines and the computed facts. Another customer's hotel is simply not found (00092): the wall gives no hint.</summary>
public class GetMyPropertyByIdQueryHandler : IRequestHandler<GetMyPropertyByIdQuery, CallResponse<PropertyModel>>
{
    private readonly IPropertyRepository properties;
    private readonly ITenantRepository tenants;
    private readonly TimeProvider clock;

    public GetMyPropertyByIdQueryHandler(IPropertyRepository properties, ITenantRepository tenants, TimeProvider clock)
    {
        this.properties = properties;
        this.tenants = tenants;
        this.clock = clock;
    }

    public async Task<CallResponse<PropertyModel>> Handle(GetMyPropertyByIdQuery request, CancellationToken cancellationToken)
    {
        var model = await this.properties.Query()
            .Where(p => p.Id == request.Id)
            .Select(PropertyProjections.Model)
            .FirstOrDefaultAsync(cancellationToken);
        if (model is null)
        {
            return CallResponseBuilder.CreateResponse<PropertyModel>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Property.NotFound));
        }

        // The lines live on the customer; the hotel passed the wall, so its customer is the caller's.
        var lines = await this.tenants.Query()
            .Where(t => t.Id == model.TenantId)
            .SelectMany(t => t.Subscriptions)
            .Where(s => s.PropertyId == model.Id)
            .OrderBy(s => s.ModuleCode)
            .Select(PropertyProjections.Line)
            .ToListAsync(cancellationToken);

        PropertyProjections.Complete(model, lines, this.clock.GetUtcNow());
        return CallResponseBuilder.CreateResponse<PropertyModel>(eCallResponseStatus.Success).HasData(model);
    }
}
