using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Models.TenantModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Handler.Queries.TenantModule.List;

/// <summary>
/// The customers, for the platform console (C2): the row and, for the page shown, what the list says beside it: the
/// hotels, the rooms declared against the rooms licensed today, the modules with a line, and the administrator with
/// the state of the invitation. A caller outside the platform tenant sees nothing (00073).
/// </summary>
public class GetPagedTenantsQueryHandler : IRequestHandler<GetPagedTenantsQuery, CallResponse<PagedResult<TenantModel>>>
{
    private readonly ITenantRepository tenants;
    private readonly IPropertyRepository properties;
    private readonly IUserAccountRepository userAccounts;
    private readonly ILocalStaffEnrolmentRepository enrolments;
    private readonly IOperatorSessionRepository sessions;
    private readonly ICallContext callContext;
    private readonly TimeProvider clock;
    private readonly Guid platformTenantId;

    public GetPagedTenantsQueryHandler(
        ITenantRepository tenants,
        IPropertyRepository properties,
        IUserAccountRepository userAccounts,
        ILocalStaffEnrolmentRepository enrolments,
        IOperatorSessionRepository sessions,
        ICallContext callContext,
        TimeProvider clock,
        IOptions<PlatformTenantSettings> platform)
    {
        this.tenants = tenants;
        this.properties = properties;
        this.userAccounts = userAccounts;
        this.enrolments = enrolments;
        this.sessions = sessions;
        this.callContext = callContext;
        this.clock = clock;
        this.platformTenantId = platform.Value.TenantId;
    }

    public async Task<CallResponse<PagedResult<TenantModel>>> Handle(GetPagedTenantsQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(this.callContext.TenantId(), out var caller) || caller != this.platformTenantId)
        {
            return CallResponseBuilder.CreateResponse<PagedResult<TenantModel>>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Tenant.PlatformOnly));
        }

        var keyword = request.SearchText?.Trim();
        var statuses = request.Statuses ?? [];
        var country = request.CountryCode?.Trim();
        var module = request.ModuleCode?.Trim();
        var query = this.tenants.Query();

        query = query.WhereIf(request.Status.HasValue, e => e.Status == request.Status!.Value);
        query = query.WhereIf(statuses.Length > 0, e => statuses.Contains(e.Status));
        query = query.WhereIf(request.Tier.HasValue, e => e.Tier == request.Tier!.Value);
        query = query.WhereIf(country.IsNotNullOrEmpty(), e => e.CountryCode == country);
        query = query.WhereIf(module.IsNotNullOrEmpty(), e => e.Subscriptions.Any(s => s.IsEnabled && s.ModuleCode == module));
        query = query.WhereIf(
            keyword.IsNotNullOrEmpty() && keyword!.Length >= 2,
            e => e.Code.Contains(keyword!) || e.LegalNameEn.Contains(keyword!) || e.LegalNameAr.Contains(keyword!) || (e.ExternalIdentifier != null && e.ExternalIdentifier.Contains(keyword!)));

        // The framework sorts DateTime, not DateOnly: a sort by the onboarding day is a sort by the creation instant.
        foreach (var sort in request.SortOrder.Where(s => string.Equals(s.MemberName, nameof(TenantModel.OnboardedOn), StringComparison.OrdinalIgnoreCase)))
        {
            sort.MemberName = nameof(TenantModel.CreatedDate);
        }

        var pagedResult = await query.Select(TenantProjections.Model(this.platformTenantId)).GetPagedResultAsync(request, true, cancellationToken);
        await TenantCompany.DescribeAsync(pagedResult.Results, false, this.clock.GetUtcNow(), this.tenants, this.properties, this.userAccounts, this.enrolments, this.sessions, cancellationToken);

        return CallResponseBuilder.CreateResponse<PagedResult<TenantModel>>(eCallResponseStatus.Success).HasData(pagedResult);
    }
}
