using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Common;

namespace Harmony.Identity.Domain.Entities.Aggregates.PropertyGroupModule;

/// <summary>
/// What only a brand carries: its look, its site, the franchise. One row that shares the group's key, inside the wall
/// like the group; born and changed only through the group. The TypeCode copy, the CHECK and the composite key keep
/// it on a Brand group in SQL, whatever the code does (the ERD's BrandDetails).
/// </summary>
public sealed class BrandDetail : IMultiTenantEntity
{
    public const int ThemeReferenceMaxLength = 128;
    public const int WebsiteUrlMaxLength = 512;
    public const int FranchisorNameMaxLength = 128;
    public const decimal RoyaltyPercentMax = 100m;

    private BrandDetail()
    {
    }

    public Guid PropertyGroupId { get; private set; }

    public Guid TenantId { get; private set; }

    /// <summary>A copy of the group's type, held honest by the composite key; always Brand.</summary>
    public string TypeCode { get; private set; } = null!;

    public string? ThemeReference { get; private set; }

    public Guid? LogoFileId { get; private set; }

    public string? WebsiteUrl { get; private set; }

    public string? FranchisorName { get; private set; }

    public DateOnly? AgreementStartsOn { get; private set; }

    public DateOnly? AgreementEndsOn { get; private set; }

    /// <summary>0 to 100, two decimals.</summary>
    public decimal? RoyaltyPercent { get; private set; }

    /// <summary>What a brand detail is asked to hold; an empty value clears the column.</summary>
    public sealed record Request(
        string? ThemeReference,
        Guid? LogoFileId,
        string? WebsiteUrl,
        string? FranchisorName,
        DateOnly? AgreementStartsOn,
        DateOnly? AgreementEndsOn,
        decimal? RoyaltyPercent);

    internal static BrandDetail New(PropertyGroup group, Request request)
    {
        var detail = new BrandDetail { PropertyGroupId = group.Id, TenantId = group.TenantId, TypeCode = group.TypeCode };
        detail.Update(request);
        return detail;
    }

    internal void Update(Request request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.AgreementEndsOn is { } ends && request.AgreementStartsOn is { } starts && ends < starts)
        {
            throw new ArgumentException("A franchise agreement cannot end before it starts.", nameof(request));
        }

        if (request.RoyaltyPercent is < 0 or > RoyaltyPercentMax)
        {
            throw new ArgumentOutOfRangeException(nameof(request), request.RoyaltyPercent, $"Royalty runs 0 to {RoyaltyPercentMax} percent.");
        }

        ThemeReference = Optional(request.ThemeReference, ThemeReferenceMaxLength, nameof(request));
        LogoFileId = request.LogoFileId;
        WebsiteUrl = Optional(request.WebsiteUrl, WebsiteUrlMaxLength, nameof(request));
        FranchisorName = Optional(request.FranchisorName, FranchisorNameMaxLength, nameof(request));
        AgreementStartsOn = request.AgreementStartsOn;
        AgreementEndsOn = request.AgreementEndsOn;
        RoyaltyPercent = request.RoyaltyPercent;
    }

    private static string? Optional(string? value, int maxLength, string argument)
    {
        var text = IdentityText.Blank(value);
        if (text is { Length: var length } && length > maxLength)
        {
            throw new ArgumentException($"At most {maxLength} characters.", argument);
        }

        return text;
    }
}
