using System.Linq.Expressions;
using Harmony.Identity.Domain.Entities.Aggregates.TenantModule;
using Harmony.Identity.Domain.Models.TenantModule;

namespace Harmony.Identity.Handler.Queries.TenantModule;

internal static class TenantProjections
{
    /// <summary>The row as the console sees it; the platform's own row is marked by its id.</summary>
    public static Expression<Func<Tenant, TenantModel>> Model(Guid platformTenantId) => e => new TenantModel
    {
        Id = e.Id,
        Code = e.Code,
        ExternalIdentifier = e.ExternalIdentifier,
        LegalNameEn = e.LegalNameEn,
        LegalNameAr = e.LegalNameAr,
        CountryCode = e.CountryCode,
        DefaultCurrency = e.DefaultCurrency,
        Status = e.Status,
        StatusReason = e.StatusReason,
        Tier = e.Tier,
        OnboardedOn = e.OnboardedOn,
        ExportWindowEndsOn = e.ExportWindowEndsOn,
        IsPlatform = e.Id == platformTenantId,
        ApprovalsRequired = e.ApprovalsRequired,
        InactivityDays = e.InactivityDays,
        CreatedDate = e.CreatedDate,
        CreatedBy = e.CreatedBy,
        ModifiedDate = e.ModifiedDate,
        ModifiedBy = e.ModifiedBy,
    };
}
