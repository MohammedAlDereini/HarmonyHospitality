using Harmony.Identity.Domain.Entities.Reference;

namespace Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;

/// <summary>
/// The four regimes a hotel lives under: rows of RegulatoryEnvironments, one per kind, all of the hotel's country.
/// Fixed once saved, the way Mews fixes a property's legal environment at creation: tax and accounting rules hang off it.
/// Whether each code exists, has the right kind and belongs to the country is the handler's check (it needs the table).
/// </summary>
public sealed record RegulatoryProfile
{
    public RegulatoryProfile(string legal, string tax, string accounting, string accommodation)
    {
        Legal = ReferenceText.Key(legal, RegulatoryEnvironment.CodeMaxLength, nameof(legal));
        Tax = ReferenceText.Key(tax, RegulatoryEnvironment.CodeMaxLength, nameof(tax));
        Accounting = ReferenceText.Key(accounting, RegulatoryEnvironment.CodeMaxLength, nameof(accounting));
        Accommodation = ReferenceText.Key(accommodation, RegulatoryEnvironment.CodeMaxLength, nameof(accommodation));
    }

    public string Legal { get; }

    public string Tax { get; }

    public string Accounting { get; }

    public string Accommodation { get; }

    /// <summary>Kind and code, in the order the handler checks them.</summary>
    public IEnumerable<(RegulatoryKind Kind, string Code)> Entries() =>
    [
        (RegulatoryKind.Legal, Legal),
        (RegulatoryKind.Tax, Tax),
        (RegulatoryKind.Accounting, Accounting),
        (RegulatoryKind.Accommodation, Accommodation),
    ];
}
