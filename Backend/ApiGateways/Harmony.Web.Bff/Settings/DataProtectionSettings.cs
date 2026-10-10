namespace Harmony.Web.Bff.Settings;

/// <summary>
/// DataProtectionSettings: the certificate that wraps this host's key ring. The keys themselves live in Redis
/// (surviving restarts and shared by every instance), but only a holder of this certificate can read them.
/// </summary>
public sealed class DataProtectionSettings
{
    public const string Section = "DataProtectionSettings";

    /// <summary>PEM file holding the certificate AND its private key.</summary>
    public string CertificatePath { get; set; } = null!;

    public static bool IsValid(DataProtectionSettings settings)
        => !string.IsNullOrWhiteSpace(settings.CertificatePath) && File.Exists(settings.CertificatePath);
}
