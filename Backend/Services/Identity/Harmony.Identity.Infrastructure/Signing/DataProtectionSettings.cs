namespace Harmony.Identity.Infrastructure.Signing;

/// <summary>
/// DataProtectionSettings — the certificate that locks the Data Protection key ring stored in the database.
/// The key ring protects the stored refresh tokens and the authenticator secrets; this certificate protects the key ring,
/// and it lives outside the repository and outside the database (a mounted secret on servers, a local folder on a PC),
/// so a copy of the database alone decrypts nothing. Same arrangement as <see cref="TokenIssuerSettings.SigningKeyPath"/>.
/// </summary>
public class DataProtectionSettings
{
    public const string Section = "DataProtectionSettings";

    /// <summary>A PEM file holding the certificate and its private key. Required; the service does not start without it.</summary>
    public string CertificatePath { get; set; } = string.Empty;

    /// <summary>
    /// Earlier certificates that still decrypt keys protected before a rotation. A rotation puts the new certificate in
    /// <see cref="CertificatePath"/> and moves the old one here until the key ring has rolled over (90 days by default).
    /// </summary>
    public string[] PreviousCertificatePaths { get; set; } = [];
}
