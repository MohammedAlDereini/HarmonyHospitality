namespace Harmony.Identity.Handler.Queries.CompanyLoginModule;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;
using Harmony.Identity.Domain.Models.CompanyLoginModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Harmony.Identity.Infrastructure.Tenants;
using Microsoft.Extensions.Options;

/// <summary>The caller's own customer's company logins (Microsoft, Google), with their domains and the consent link.</summary>
public class GetMyCompanyLoginsQuery : BaseQueryRequest<CallResponse<List<CompanyLoginModel>>>
{
}

/// <summary>A customer's company logins, for the platform's console.</summary>
public class GetCompanyLoginsOfTenantQuery : BaseQueryRequest<CallResponse<List<CompanyLoginModel>>>
{
    public Guid TenantId { get; set; }

    public class GetCompanyLoginsOfTenantQueryValidator : AbstractValidator<GetCompanyLoginsOfTenantQuery>
    {
        public GetCompanyLoginsOfTenantQueryValidator()
        {
            this.RuleFor(e => e.TenantId).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.CompanyLogin.TenantRequired);
        }
    }
}

public class GetMyCompanyLoginsQueryHandler : IRequestHandler<GetMyCompanyLoginsQuery, CallResponse<List<CompanyLoginModel>>>
{
    private readonly IFederatedAuthorityRepository authorities;
    private readonly ICompanyLoginDirectory directory;
    private readonly ICallContext callContext;

    public GetMyCompanyLoginsQueryHandler(IFederatedAuthorityRepository authorities, ICompanyLoginDirectory directory, ICallContext callContext)
    {
        this.authorities = authorities;
        this.directory = directory;
        this.callContext = callContext;
    }

    public async Task<CallResponse<List<CompanyLoginModel>>> Handle(GetMyCompanyLoginsQuery request, CancellationToken cancellationToken)
    {
        var logins = await this.authorities.OfTenantAsync(Guid.Parse(this.callContext.TenantId()), cancellationToken);
        return CallResponseBuilder.CreateResponse<List<CompanyLoginModel>>(eCallResponseStatus.Success).HasData(CompanyLoginProjection.Of(logins, this.directory));
    }
}

public class GetCompanyLoginsOfTenantQueryHandler : IRequestHandler<GetCompanyLoginsOfTenantQuery, CallResponse<List<CompanyLoginModel>>>
{
    private readonly IFederatedAuthorityRepository authorities;
    private readonly ICompanyLoginDirectory directory;
    private readonly ICallContext callContext;
    private readonly Guid platformTenant;

    public GetCompanyLoginsOfTenantQueryHandler(IFederatedAuthorityRepository authorities, ICompanyLoginDirectory directory, ICallContext callContext, IOptions<PlatformTenantSettings> platform)
    {
        this.authorities = authorities;
        this.directory = directory;
        this.callContext = callContext;
        this.platformTenant = platform.Value.TenantId;
    }

    public async Task<CallResponse<List<CompanyLoginModel>>> Handle(GetCompanyLoginsOfTenantQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(this.callContext.TenantId(), out var caller) || caller != this.platformTenant)
        {
            return CallResponseBuilder.CreateResponse<List<CompanyLoginModel>>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.Tenant.PlatformOnly));
        }

        var logins = await this.authorities.OfTenantAsync(request.TenantId, cancellationToken);
        return CallResponseBuilder.CreateResponse<List<CompanyLoginModel>>(eCallResponseStatus.Success).HasData(CompanyLoginProjection.Of(logins, this.directory));
    }
}

/// <summary>The company logins only (the local authority is Harmony's own sign-in, not something to show or change).</summary>
internal static class CompanyLoginProjection
{
    public static List<CompanyLoginModel> Of(IEnumerable<FederatedAuthority> logins, ICompanyLoginDirectory directory)
        => logins
            .Where(login => login.IsCompany)
            .Select(login => new CompanyLoginModel
            {
                Id = login.Id,
                Provider = login.Scheme.ToString(),
                Issuer = login.Issuer,
                IsEnabled = login.IsEnabled,
                Domains = login.Domains.Select(d => d.Domain).Order(StringComparer.Ordinal).ToList(),
                ConsentUrl = directory.ConsentUrlOf(login),
                ModifiedDate = login.ModifiedDate ?? login.CreatedDate,
            })
            .ToList();
}
