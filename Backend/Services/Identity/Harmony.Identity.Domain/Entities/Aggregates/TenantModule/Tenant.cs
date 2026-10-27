using System.Text.RegularExpressions;
using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.DomainEvents.TenantModule;

namespace Harmony.Identity.Domain.Entities.Aggregates.TenantModule;

/// <summary>
/// The customer: one row per hotel company that bought Harmony. The contract, the bill and the security wall; a chain
/// of forty hotels is still one row. Everything flows out of it (people, roles, properties), nothing points back into
/// it. Not a multi-tenant row itself: the table is platform data, read and written by the platform tenant (us) only.
/// The platform tenant has a row too, seeded, so the wall below treats it like any other; no door ever suspends it.
/// Its contract is the owned collection of subscription lines (Subscribe, Holds).
/// </summary>
public sealed partial class Tenant : AggregateRoot<Guid>, IAuditableEntity, IConcurrentEntity
{
    public const int CodeMaxLength = 32;
    public const int LegalNameMaxLength = 128;
    public const int ExternalIdentifierMaxLength = 64;
    public const int MinTier = 1;
    public const int MaxTier = 5;

    private Tenant()
    {
    }

    /// <summary>The short name we call the customer by in the console and on the bill, like RITZ-AMMAN. Chosen once; upper-case.</summary>
    public string Code { get; private set; } = null!;

    /// <summary>The contract or billing reference in the system that sends the bill. Optional, ours to read, never shown to the hotel.</summary>
    public string? ExternalIdentifier { get; private set; }

    public string LegalNameEn { get; private set; } = null!;

    public string LegalNameAr { get; private set; } = null!;

    /// <summary>ISO 3166-1 alpha-2, a row of Countries. Required for a customer; the platform's own row may leave it empty.</summary>
    public string? CountryCode { get; private set; }

    /// <summary>ISO 4217, a row of Currencies: the currency of the contract. Required for a customer; the platform's own row may leave it empty.</summary>
    public string? DefaultCurrency { get; private set; }

    public TenantStatus Status { get; private set; }

    /// <summary>Why the tenant is suspended or terminated, for the console. Null while active.</summary>
    public string? StatusReason { get; private set; }

    /// <summary>The commercial level, 1 (smallest) to 5. What each level buys is the entitlements' business.</summary>
    public int Tier { get; private set; }

    /// <summary>The day the customer came on board.</summary>
    public DateOnly OnboardedOn { get; private set; }

    /// <summary>After termination: until when the customer may still export its data. Null before.</summary>
    public DateOnly? ExportWindowEndsOn { get; private set; }

    /// <summary>
    /// The customer's administrator asks for a second person on risky changes (stage D): an administrator-level role, a
    /// two-step exemption, a merge, switching company login off, letting a quarantined person in, and switching this off.
    /// Off by default.
    /// </summary>
    public bool ApprovalsRequired { get; private set; }

    /// <summary>The contract, as lines: a hotel's module with its counts, or a chain-wide module. Changed only through Subscribe and Unsubscribe.</summary>
    public ICollection<TenantSubscription> Subscriptions { get; private set; } = new List<TenantSubscription>();

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    /// <summary>Its people may sign in and its data may change.</summary>
    public bool IsActive => Status == TenantStatus.Active;

    /// <summary>A new customer, active from today.</summary>
    public static Tenant Create(string code, string legalNameEn, string legalNameAr, string countryCode, string defaultCurrency, int tier, string? externalIdentifier, DateOnly onboardedOn)
    {
        return new Tenant
        {
            Code = TenantCode(code),
            LegalNameEn = LegalName(legalNameEn, nameof(legalNameEn)),
            LegalNameAr = LegalName(legalNameAr, nameof(legalNameAr)),
            CountryCode = Letters(countryCode, 2, nameof(countryCode)),
            DefaultCurrency = Letters(defaultCurrency, 3, nameof(defaultCurrency)),
            Tier = TierOf(tier),
            ExternalIdentifier = External(externalIdentifier),
            Status = TenantStatus.Active,
            OnboardedOn = onboardedOn,
        };
    }

    /// <summary>
    /// The platform's own row (us), written once by the seed, which gives it the id every service already knows from its
    /// settings (the base keeps Id read-only, so the seed sets it through the context before the save). No country or
    /// currency yet: the reference tables start empty, and the console completes the row later.
    /// </summary>
    public static Tenant CreatePlatform(string code, string legalNameEn, string legalNameAr, DateOnly onboardedOn)
    {
        return new Tenant
        {
            Code = TenantCode(code),
            LegalNameEn = LegalName(legalNameEn, nameof(legalNameEn)),
            LegalNameAr = LegalName(legalNameAr, nameof(legalNameAr)),
            Tier = MaxTier,
            Status = TenantStatus.Active,
            OnboardedOn = onboardedOn,
        };
    }

    /// <summary>Everything but the code and the status: names, country, currency, tier, the billing reference.</summary>
    public void Update(string legalNameEn, string legalNameAr, string countryCode, string defaultCurrency, int tier, string? externalIdentifier)
    {
        AssertNotTerminated();

        LegalNameEn = LegalName(legalNameEn, nameof(legalNameEn));
        LegalNameAr = LegalName(legalNameAr, nameof(legalNameAr));
        CountryCode = Letters(countryCode, 2, nameof(countryCode));
        DefaultCurrency = Letters(defaultCurrency, 3, nameof(defaultCurrency));
        Tier = TierOf(tier);
        ExternalIdentifier = External(externalIdentifier);
    }

    /// <summary>Approvals on or off. Switching off when approvals are on goes through an approval itself (the handler's rule).</summary>
    public void RequireApprovals(bool required)
    {
        AssertNotTerminated();
        ApprovalsRequired = required;
    }

    /// <summary>Nobody in it signs in until it is reinstated. Suspending twice is a no-op.</summary>
    public void Suspend(string reason)
    {
        AssertNotTerminated();

        if (Status == TenantStatus.Suspended)
        {
            return;
        }

        // The reason first, the status second: a refused reason leaves the tenant exactly as it was.
        var text = IdentityText.Reason(reason) ?? throw new ArgumentException("A reason is required.", nameof(reason));
        Status = TenantStatus.Suspended;
        StatusReason = text;
        Raise(new TenantSuspendedDomainEvent(Id, text));
    }

    /// <summary>Back to Active; its people sign in again and its lines are served again. Reinstating an active tenant is a no-op.</summary>
    public void Reinstate()
    {
        AssertNotTerminated();

        if (Status != TenantStatus.Suspended)
        {
            return;
        }

        Status = TenantStatus.Active;
        StatusReason = null;
        Raise(new TenantReinstatedDomainEvent(Id));
    }

    /// <summary>
    /// The line for a hotel and module (or a chain-wide module): born on the first subscribe, renewed and switched back
    /// on by every later one, so there is never a second row for the same thing. Only while Active.
    /// </summary>
    public TenantSubscription Subscribe(Guid? propertyId, string moduleCode, int? rooms, int? terminals, DateOnly startsOn, DateOnly? endsOn, bool renewsAutomatically = true)
    {
        AssertActive();

        var line = Subscriptions.FirstOrDefault(s => s.Is(propertyId, moduleCode));
        if (line is null)
        {
            line = TenantSubscription.New(propertyId, moduleCode, rooms, terminals, startsOn, endsOn, renewsAutomatically);
            Subscriptions.Add(line);
        }
        else
        {
            line.Renew(rooms, terminals, startsOn, endsOn, renewsAutomatically);
        }

        RaiseSubscriptionsChanged();
        return line;
    }

    /// <summary>
    /// The line switched off with a reason, today, whatever its term says; the row stays for the history and a later
    /// Subscribe switches it back on. Null when the customer never had that line (the handler answers 00094).
    /// Only while Active: a suspended customer's contract waits.
    /// </summary>
    public TenantSubscription? Unsubscribe(Guid? propertyId, string moduleCode, string reason)
    {
        AssertActive();

        var line = Subscriptions.FirstOrDefault(s => s.Is(propertyId, moduleCode));
        if (line is null)
        {
            return null;
        }

        line.Disable(reason);
        RaiseSubscriptionsChanged();
        return line;
    }

    /// <summary>Does the customer hold this module today, at this hotel or chain-wide.</summary>
    public bool Holds(Guid? propertyId, string moduleCode, DateOnly today)
        => Subscriptions.Any(s => s.Is(propertyId, moduleCode) && s.IsHeldOn(today));

    /// <summary>The most rooms any held line licenses at this hotel; null when none carries rooms.</summary>
    public int? LicensedRooms(Guid propertyId, DateOnly today)
        => Subscriptions.Where(s => s.PropertyId == propertyId && s.Rooms is not null && s.IsHeldOn(today)).Max(s => s.Rooms);

    private void AssertActive()
    {
        if (Status != TenantStatus.Active)
        {
            throw new InvalidOperationException("Only an active tenant's contract changes.");
        }
    }

    /// <summary>One event per save, however many lines moved: the handler rewrites the whole key anyway.</summary>
    private void RaiseSubscriptionsChanged()
    {
        if (!DomainEvents.OfType<TenantSubscriptionsChangedDomainEvent>().Any())
        {
            Raise(new TenantSubscriptionsChangedDomainEvent(Id));
        }
    }

    private void AssertNotTerminated()
    {
        if (Status == TenantStatus.Terminated)
        {
            throw new InvalidOperationException("A terminated tenant cannot change.");
        }
    }

    private static string TenantCode(string? value)
    {
        var code = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!CodePattern().IsMatch(code))
        {
            throw new ArgumentException($"'{value}' is not a tenant code: 2 to {CodeMaxLength} characters of letters, digits, . - _", nameof(value));
        }

        return code;
    }

    private static string LegalName(string? value, string argument)
    {
        var name = value?.Trim() ?? string.Empty;
        if (name.Length is 0 or > LegalNameMaxLength)
        {
            throw new ArgumentException($"A legal name of 1 to {LegalNameMaxLength} characters is required.", argument);
        }

        return name;
    }

    private static string Letters(string? value, int length, string argument)
    {
        var code = value?.Trim().ToUpperInvariant() ?? string.Empty;
        if (code.Length != length || !code.All(char.IsAsciiLetterUpper))
        {
            throw new ArgumentException($"'{value}' is not a code of {length} letters.", argument);
        }

        return code;
    }

    private static string? External(string? value)
    {
        var reference = IdentityText.Blank(value);
        if (reference is { Length: > ExternalIdentifierMaxLength })
        {
            throw new ArgumentException($"The external identifier is at most {ExternalIdentifierMaxLength} characters.", nameof(value));
        }

        return reference;
    }

    private static int TierOf(int tier)
        => tier is >= MinTier and <= MaxTier ? tier : throw new ArgumentOutOfRangeException(nameof(tier), tier, $"The tier is {MinTier} to {MaxTier}.");

    [GeneratedRegex("^[A-Z0-9._-]{2,32}$")]
    private static partial Regex CodePattern();
}
