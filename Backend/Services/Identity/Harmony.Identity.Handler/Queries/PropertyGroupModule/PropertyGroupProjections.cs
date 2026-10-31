using System.Linq.Expressions;
using Harmony.Identity.Domain.Entities.Aggregates.GroupMembershipModule;
using Harmony.Identity.Domain.Entities.Aggregates.PropertyGroupModule;
using Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;
using Harmony.Identity.Domain.Models.PropertyGroupModule;

namespace Harmony.Identity.Handler.Queries.PropertyGroupModule;

internal static class PropertyGroupProjections
{
    /// <summary>The group row as the reads show it, with the brand's columns when it carries them; the precedence reads as its one column.</summary>
    public static readonly Expression<Func<PropertyGroup, PropertyGroupModel>> Model = e => new PropertyGroupModel
    {
        Id = e.Id,
        TenantId = e.TenantId,
        TypeCode = e.TypeCode,
        Code = e.Code,
        ExternalIdentifier = e.ExternalIdentifier,
        NameEn = e.NameEn,
        NameAr = e.NameAr,
        Description = e.Description,
        Precedence = e.Precedence.Value,
        DisplayOrder = e.DisplayOrder,
        IsActive = e.IsActive,
        CreatedDate = e.CreatedDate,
        CreatedBy = e.CreatedBy,
        ModifiedDate = e.ModifiedDate,
        ModifiedBy = e.ModifiedBy,
        Brand = e.Brand == null
            ? null
            : new BrandDetailModel
            {
                ThemeReference = e.Brand.ThemeReference,
                LogoFileId = e.Brand.LogoFileId,
                WebsiteUrl = e.Brand.WebsiteUrl,
                FranchisorName = e.Brand.FranchisorName,
                AgreementStartsOn = e.Brand.AgreementStartsOn,
                AgreementEndsOn = e.Brand.AgreementEndsOn,
                RoyaltyPercent = e.Brand.RoyaltyPercent,
            },
    };

    /// <summary>A group's memberships with the hotel named, current ones first, then by hotel code, newest first.</summary>
    public static IQueryable<PropertyGroupMemberModel> MembersOf(IQueryable<PropertyGroupMember> members, IQueryable<Property> properties, Guid groupId)
        => members
            .Where(m => m.PropertyGroupId == groupId)
            .Join(properties, m => m.PropertyId, p => p.Id, (m, p) => new PropertyGroupMemberModel
            {
                Id = m.Id,
                PropertyId = p.Id,
                PropertyCode = p.Code,
                PropertyNameEn = p.NameEn,
                PropertyNameAr = p.NameAr,
                FromUtc = m.Period.FromUtc,
                ToUtc = m.Period.ToUtc,
                IsCurrent = m.Period.ToUtc == null,
            })
            .OrderBy(x => x.ToUtc != null)
            .ThenBy(x => x.PropertyCode)
            .ThenByDescending(x => x.FromUtc);

    /// <summary>The count of open memberships per group on a page of rows: one grouped query, no N+1.</summary>
    public static async Task FillCurrentMembersAsync(IList<PropertyGroupModel> rows, IQueryable<PropertyGroupMember> members, CancellationToken cancellationToken)
    {
        var ids = rows.Select(r => r.Id).ToList();
        if (ids.Count == 0)
        {
            return;
        }

        var counts = await members
            .Where(m => ids.Contains(m.PropertyGroupId) && m.Period.ToUtc == null)
            .GroupBy(m => m.PropertyGroupId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, cancellationToken);

        foreach (var row in rows)
        {
            row.CurrentMembers = counts.GetValueOrDefault(row.Id);
        }
    }

    /// <summary>The list filters every read shares: a type, a search of the code, the names or the reference, an active flag.</summary>
    public static IQueryable<PropertyGroup> Filtered(IQueryable<PropertyGroup> query, string? typeCode, string? searchText, bool? isActive)
    {
        var type = typeCode?.Trim();
        var keyword = searchText?.Trim();
        query = query.WhereIf(type.IsNotNullOrEmpty(), e => e.TypeCode == type!);
        query = query.WhereIf(isActive.HasValue, e => e.IsActive == isActive!.Value);
        return query.WhereIf(
            keyword.IsNotNullOrEmpty() && keyword!.Length >= 2,
            e => e.Code.Contains(keyword!) || e.NameEn.Contains(keyword!) || e.NameAr.Contains(keyword!) || (e.ExternalIdentifier != null && e.ExternalIdentifier.Contains(keyword!)));
    }
}
