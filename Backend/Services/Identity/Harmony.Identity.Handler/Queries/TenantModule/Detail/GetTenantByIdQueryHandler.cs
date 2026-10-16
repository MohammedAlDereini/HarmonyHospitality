using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Models.TenantModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Handler.Queries.TenantModule.Detail;

public class GetTenantByIdQueryHandler : IRequestHandler<GetTenantByIdQuery, CallResponse<TenantModel>>
{
    private readonly ITenantRepository tenants;
    private readonly ICallContext callContext;
    private readonly Guid platformTenantId;

    public GetTenantByIdQueryHandler(ITenantRepository tenants, ICallContext callContext, IOptions<PlatformTenantSettings> platform)
    {
        this.tenants = tenants;
        this.callContext = callContext;
        this.platformTenantId = platform.Value.TenantId;
    }

    public async Task<CallResponse<TenantModel>> Handle(GetTenantByIdQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(this.callContext.TenantId(), out var caller) || caller != this.platformTenantId)
        {
            return Refuse(BusinessErrorCodes.Identity.Tenant.PlatformOnly);
        }

        var model = await this.tenants.Query()
            .Where(e => e.Id == request.Id)
            .Select(TenantProjections.Model(this.platformTenantId))
            .FirstOrDefaultAsync(cancellationToken);

        return model is null
            ? Refuse(BusinessErrorCodes.Identity.Tenant.NotFound)
            : CallResponseBuilder.CreateResponse<TenantModel>(eCallResponseStatus.Success).HasData(model);
    }

    private static CallResponse<TenantModel> Refuse(string code)
        => CallResponseBuilder.CreateResponse<TenantModel>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(code));
}
