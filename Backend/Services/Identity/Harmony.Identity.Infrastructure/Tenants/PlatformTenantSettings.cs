namespace Harmony.Identity.Infrastructure.Tenants;

/// <summary>
/// The platform's own tenant: us. Its id is the one every service already knows (ApplicationSettings:ApplicationKeys:
/// PlatformTenantId); the code and the names below are what its seeded Tenants row says. It holds the whole permission
/// catalogue, platform-only permissions included, and its people run the customers from the console. They never work
/// inside a customer tenant: the token's tenant is the wall, and no door here crosses it.
/// </summary>
public class PlatformTenantSettings
{
    public const string Section = "IdentitySettings:PlatformTenant";

    /// <summary>Filled from ApplicationSettings:ApplicationKeys:PlatformTenantId, never from this section.</summary>
    public Guid TenantId { get; set; }

    public string Code { get; set; } = "HARMONY";

    public string LegalNameEn { get; set; } = "Harmony";

    public string LegalNameAr { get; set; } = "هارموني";

    public static bool IsValid(PlatformTenantSettings settings)
        => settings.TenantId != Guid.Empty
            && !string.IsNullOrWhiteSpace(settings.Code) && settings.Code.Trim().Length <= 32
            && !string.IsNullOrWhiteSpace(settings.LegalNameEn) && settings.LegalNameEn.Trim().Length <= 128
            && !string.IsNullOrWhiteSpace(settings.LegalNameAr) && settings.LegalNameAr.Trim().Length <= 128;
}
