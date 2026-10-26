namespace Harmony.Identity.Domain.Models.PropertyGroupModule;

/// <summary>The brand's own columns, on the detail read of a Brand group.</summary>
public class BrandDetailModel
{
    public string? ThemeReference { get; set; }

    public Guid? LogoFileId { get; set; }

    public string? WebsiteUrl { get; set; }

    public string? FranchisorName { get; set; }

    public DateOnly? AgreementStartsOn { get; set; }

    public DateOnly? AgreementEndsOn { get; set; }

    public decimal? RoyaltyPercent { get; set; }
}
