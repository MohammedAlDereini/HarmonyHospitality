using System.Security.Cryptography.X509Certificates;

namespace Harmony.Identity.Infrastructure.Signing;

/// <summary>
/// Loads the certificates that lock the Data Protection key ring, and refuses to start on anything that would lock us
/// out later: a missing file, a certificate without its private key, or a current certificate that has already expired.
/// </summary>
public static class KeyRingCertificate
{
    /// <summary>The certificate new key-ring entries are encrypted with. Must be valid today.</summary>
    public static X509Certificate2 LoadCurrent(string? path)
        => Load(path, $"{DataProtectionSettings.Section}:{nameof(DataProtectionSettings.CertificatePath)}", mustBeValidNow: true);

    /// <summary>An earlier certificate that only decrypts. It may be expired: it still has to open what it once locked.</summary>
    public static X509Certificate2 LoadPrevious(string? path, int index)
        => Load(path, $"{DataProtectionSettings.Section}:{nameof(DataProtectionSettings.PreviousCertificatePaths)}[{index}]", mustBeValidNow: false);

    private static X509Certificate2 Load(string? path, string setting, bool mustBeValidNow)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new InvalidOperationException($"{setting} is required: a PEM file with the certificate and its private key.");
        }

        if (!File.Exists(path))
        {
            throw new InvalidOperationException($"{setting} does not exist: '{path}'.");
        }

        X509Certificate2 certificate;
        try
        {
            certificate = X509Certificate2.CreateFromPemFile(path);
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException($"{setting} is not a readable PEM certificate with a private key.", exception);
        }

        if (!certificate.HasPrivateKey)
        {
            throw new InvalidOperationException($"{setting} holds a certificate without its private key; the key ring could never be decrypted.");
        }

        if (mustBeValidNow && certificate.NotAfter < DateTime.Now)
        {
            throw new InvalidOperationException($"{setting} expired on {certificate.NotAfter:yyyy-MM-dd}. Rotate: new certificate here, this one in PreviousCertificatePaths.");
        }

        return certificate;
    }
}
