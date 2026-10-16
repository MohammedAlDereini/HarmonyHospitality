using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Models.TenantModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Handler.Queries.TenantModule.List;

/// <summary>The customers, for the platform console. A caller outside the platform tenant sees nothing (00073).</summary>
public class GetPagedTenantsQueryHandler : IRequestHandler<GetPagedTenantsQuery, CallResponse<PagedResult<TenantModel>>>
{
    private readonly ITenantRepository tenants;
    private readonly ICallContext callContext;
    private readonly Guid platformTenantId;

    public GetPagedTenantsQueryHandler(ITenantRepository tenants, ICallContext callContext, IOptions<PlatformTenantSettings> platform)
    {
        this.tenants = tenants;
        this.callContext = callContext;
        this.platformTenantId = platform.Value.TenantId;
    }

    public async Task<CallResponse<PagedResult<TenantModel>>> Handle(GetPagedTenantsQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(this.callContext.TenantId(), out var caller) || caller != this.platformTenantId)
        {
            return CallResponseBuilder.CreateResponse<PagedResult<TenantModel>>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Tenant.PlatformOnly));
        }

        var keyword = request.SearchText?.Trim();
        var query = this.tenants.Query();

        query = query.WhereIf(request.Status.HasValue, e => e.Status == request.Status!.Value);
        query = query.WhereIf(
            keyword.IsNotNullOrEmpty() && keyword!.Length >= 2,
            e => e.Code.Contains(keyword!) || e.LegalNameEn.Contains(keyword!) || e.LegalNameAr.Contains(keyword!) || (e.ExternalIdentifier != null && e.ExternalIdentifier.Contains(keyword!)));

        var pagedResult = await query.Select(TenantProjections.Model(this.platformTenantId)).GetPagedResultAsync(request, true, cancellationToken);

        return CallResponseBuilder.CreateResponse<PagedResult<TenantModel>>(eCallResponseStatus.Success).HasData(pagedResult);
    }
}
