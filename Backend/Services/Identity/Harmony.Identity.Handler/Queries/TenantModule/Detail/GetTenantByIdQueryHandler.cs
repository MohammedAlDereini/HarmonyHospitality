using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Models.TenantModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Handler.Queries.PropertyModule;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Handler.Queries.TenantModule.Detail;

public class GetTenantByIdQueryHandler : IRequestHandler<GetTenantByIdQuery, CallResponse<TenantModel>>
{
    private readonly ITenantRepository tenants;
    private readonly ICallContext callContext;
    private readonly TimeProvider clock;
    private readonly Guid platformTenantId;

    public GetTenantByIdQueryHandler(ITenantRepository tenants, ICallContext callContext, IOptions<PlatformTenantSettings> platform, TimeProvider clock)
    {
        this.tenants = tenants;
        this.callContext = callContext;
        this.clock = clock;
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

        if (model is null)
        {
            return Refuse(BusinessErrorCodes.Identity.Tenant.NotFound);
        }

        // The whole contract: hotel lines and chain-wide ones, each marked held or not for today.
        model.Subscriptions = await this.tenants.Query()
            .Where(t => t.Id == model.Id)
            .SelectMany(t => t.Subscriptions)
            .OrderBy(s => s.PropertyId).ThenBy(s => s.ModuleCode)
            .Select(PropertyProjections.Line)
            .ToListAsync(cancellationToken);
        PropertyProjections.MarkHeld(model.Subscriptions, DateOnly.FromDateTime(this.clock.GetUtcNow().UtcDateTime));

        return CallResponseBuilder.CreateResponse<TenantModel>(eCallResponseStatus.Success).HasData(model);
    }

    private static CallResponse<TenantModel> Refuse(string code)
        => CallResponseBuilder.CreateResponse<TenantModel>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(code));
}
