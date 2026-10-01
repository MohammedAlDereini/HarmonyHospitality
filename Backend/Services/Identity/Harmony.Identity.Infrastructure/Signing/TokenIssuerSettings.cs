namespace Harmony.Identity.Infrastructure.Signing;

/// <summary>
/// TokenIssuerSettings — who this Identity is, and the RSA keys it signs with.
/// The private key never reaches configuration in git: every environment points <see cref="SigningKeyPath"/>
/// at a key file kept outside the repository (a mounted secret on servers, a local folder on a PC).
/// </summary>
public class TokenIssuerSettings
{
    public const string Section = "TokenIssuerSettings";

    /// <summary>The exact "iss" every token carries, and what every service checks. A logical https name.</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>The current private key, PEM encoded (RSA, at least 2048 bits).</summary>
    public string? SigningKeyPem { get; set; }

    /// <summary>A file holding the current private key, PEM encoded. Used when <see cref="SigningKeyPem"/> is empty.</summary>
    public string? SigningKeyPath { get; set; }

    /// <summary>
    /// Keys that still verify tokens in flight but sign nothing. A rotation publishes the new key, moves
    /// signing to it, and keeps the old one here until every token it signed has expired.
    /// </summary>
    public string[] PreviousSigningKeyPems { get; set; } = [];
}