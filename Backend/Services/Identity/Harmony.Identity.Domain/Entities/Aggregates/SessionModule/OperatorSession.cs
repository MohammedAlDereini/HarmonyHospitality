using Harmony.Core.BuildingBlocks.Domain.Abstractions;

namespace Harmony.Identity.Domain.Entities.Aggregates.SessionModule;

/// <summary>
/// One sign-in, from start to end (design 3.4, the ERD's OperatorSessions): who, how, whether two-step was done, and when
/// it ended. Duende keeps the live session and forgets it when it ends; this row is the record that stays. A session ends
/// once: ending an ended one changes nothing (sign-out arrives twice more often than you would think).
/// </summary>
public sealed class OperatorSession : AggregateRoot<Guid>, IAuditableEntity, IMultiTenantEntity, IConcurrentEntity
{
    public const int SessionKeyMaxLength = 100;
    public const int MethodsMaxLength = 64;
    public const int ProviderMaxLength = 16;
    public const int EndReasonMaxLength = 32;

    /// <summary>Why a session ended, as the row says it.</summary>
    public const string SignedOut = "signed-out";
    public const string DeviceSignedOut = "device-signed-out";
    public const string AllEnded = "all-sessions-ended";
    public const string Expired = "expired";

    private OperatorSession()
    {
    }

    private OperatorSession(Guid id)
        : base(id)
    {
    }

    public Guid TenantId { get; private set; }

    public Guid UserAccountId { get; private set; }

    /// <summary>Duende's session id (sid): what a sign-out, a device sign-out or the closing job names.</summary>
    public string SessionKey { get; private set; } = null!;

    /// <summary>How the person signed in, as the token's amr: "pwd", "mfa", "swk,mfa", "external".</summary>
    public string Methods { get; private set; } = null!;

    /// <summary>local, microsoft or google.</summary>
    public string Provider { get; private set; } = null!;

    /// <summary>Two-step was done at sign-in: a code, a passkey, or a company sign-in whose company said so ("mfa").</summary>
    public bool MultiFactorSatisfied { get; private set; }

    public DateTimeOffset StartedAtUtc { get; private set; }

    /// <summary>Null while the session is open.</summary>
    public DateTimeOffset? EndedAtUtc { get; private set; }

    public string? EndReason { get; private set; }

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    public bool IsOpen => EndedAtUtc is null;

    public static OperatorSession Start(Guid tenantId, Guid userAccountId, string sessionKey, IReadOnlyCollection<string> methods, string provider, DateTimeOffset at)
    {
        if (tenantId == Guid.Empty || userAccountId == Guid.Empty || string.IsNullOrWhiteSpace(sessionKey))
        {
            throw new ArgumentException("A tenant, an account and the session id are required.");
        }

        var amr = string.Join(',', methods.Where(m => !string.IsNullOrWhiteSpace(m)));
        return new OperatorSession(Guid.CreateVersion7())
        {
            TenantId = tenantId,
            UserAccountId = userAccountId,
            SessionKey = Clip(sessionKey.Trim(), SessionKeyMaxLength),
            Methods = Clip(amr.Length == 0 ? "pwd" : amr, MethodsMaxLength),
            Provider = Clip(string.IsNullOrWhiteSpace(provider) ? "local" : provider.Trim().ToLowerInvariant(), ProviderMaxLength),
            MultiFactorSatisfied = methods.Contains("mfa"),
            StartedAtUtc = at.ToUniversalTime(),
        };
    }

    /// <summary>The session ends now, for this reason. Already ended: nothing changes.</summary>
    public void End(DateTimeOffset at, string reason)
    {
        if (EndedAtUtc is not null)
        {
            return;
        }

        var end = at.ToUniversalTime();
        EndedAtUtc = end < StartedAtUtc ? StartedAtUtc : end;
        EndReason = Clip(string.IsNullOrWhiteSpace(reason) ? SignedOut : reason.Trim(), EndReasonMaxLength);
    }

    private static string Clip(string text, int max) => text.Length <= max ? text : text[..max];
}
