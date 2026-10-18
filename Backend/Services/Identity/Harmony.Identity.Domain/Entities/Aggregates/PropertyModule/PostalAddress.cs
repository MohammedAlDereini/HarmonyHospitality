using Harmony.Identity.Domain.Common;

namespace Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;

/// <summary>
/// The street part of where the hotel stands: a required line and an optional postal code. Required at birth because
/// an invoice without an address is not an invoice (Mews requires it on the property; Opera keeps an Address section).
/// </summary>
public sealed record PostalAddress
{
    public const int LineMaxLength = 256;
    public const int PostalCodeMaxLength = 16;

    public PostalAddress(string line, string? postalCode)
    {
        Line = IdentityText.Blank(line) ?? throw new ArgumentException("An address line is required.", nameof(line));
        if (Line.Length > LineMaxLength)
        {
            throw new ArgumentException($"An address line is at most {LineMaxLength} characters.", nameof(line));
        }

        PostalCode = IdentityText.Blank(postalCode);
        if (PostalCode is { Length: > PostalCodeMaxLength })
        {
            throw new ArgumentException($"A postal code is at most {PostalCodeMaxLength} characters.", nameof(postalCode));
        }
    }

    public string Line { get; }

    public string? PostalCode { get; }
}
