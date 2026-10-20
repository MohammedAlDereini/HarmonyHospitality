using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Models.TenantModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Handler.Queries.PropertyModule;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Handler.Queries.TenantModule.Mine;

/// <summary>My customer and every line it holds, hotel and chain-wide, each marked held or not for today. Tenants carry no wall, so the caller's id from the token is the filter.</summary>
public class GetMyTenantQueryHandler : IRequestHandler<GetMyTenantQuery, CallResponse<TenantModel>>
{
    private readonly ITenantRepository tenants;
    private readonly ICallContext callContext;
    private readonly TimeProvider clock;
    private readonly Guid platformTenantId;

    public GetMyTenantQueryHandler(ITenantRepository tenants, ICallContext callContext, IOptions<PlatformTenantSettings> platform, TimeProvider clock)
    {
        this.tenants = tenants;
        this.callContext = callContext;
        this.clock = clock;
        this.platformTenantId = platform.Value.TenantId;
    }

    public async Task<CallResponse<TenantModel>> Handle(GetMyTenantQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(this.callContext.TenantId(), out var caller))
        {
            return Refuse(BusinessErrorCodes.Identity.Tenant.NotFound);
        }

        var model = await this.tenants.Query()
            .Where(e => e.Id == caller)
            .Select(TenantProjections.Model(this.platformTenantId))
            .FirstOrDefaultAsync(cancellationToken);
        if (model is null)
        {
            return Refuse(BusinessErrorCodes.Identity.Tenant.NotFound);
        }

        model.Subscriptions = await this.tenants.Query()
            .Where(t => t.Id == caller)
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
