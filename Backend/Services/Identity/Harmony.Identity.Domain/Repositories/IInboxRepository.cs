namespace Harmony.Identity.Domain.Repositories;

/// <summary>
/// The inbox (stage F, Microsoft's Idempotent Consumer pattern): a receiver of another service's events records each
/// message it handled IN ITS OWN SAVE, keyed by receiver + message id. A repeat finds the mark and is skipped; two copies
/// at once cannot both commit (the key is unique). Marks live 14 days, longer than the outbox's 7-day keep.
/// </summary>
public interface IInboxRepository
{
    /// <summary>True when this receiver already handled this message.</summary>
    Task<bool> HandledAsync(string receiver, string messageId, CancellationToken cancellationToken);

    /// <summary>Stages the mark; the receiver's next save stores it together with its work, or neither.</summary>
    Task RecordAsync(string receiver, string messageId, Guid tenantId, CancellationToken cancellationToken);

    /// <summary>Removes marks older than the cutoff, at most one batch; returns how many.</summary>
    Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, int batchSize, CancellationToken cancellationToken);
}
