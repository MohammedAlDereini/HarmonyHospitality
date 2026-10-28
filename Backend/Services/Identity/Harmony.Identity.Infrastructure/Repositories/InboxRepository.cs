using System.Security.Cryptography;
using System.Text;
using Harmony.Core.BuildingBlocks.Infrastructure;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Harmony.Identity.Infrastructure.Repositories;

/// <summary>
/// The framework's request table (ClientRequest), mapped into Identity's own context as InboxMessages, so a mark joins the
/// receiver's save. Its one key column holds a hash of receiver + message id: two receivers of one message each keep their
/// own mark (Microsoft: key on consumer and message, or the first receiver would hide the message from the others).
/// </summary>
public sealed class InboxRepository : IInboxRepository
{
    private readonly IdentityDbContext context;

    public InboxRepository(IdentityDbContext context)
    {
        this.context = context;
    }

    /// <summary>The mark's key: the first 16 bytes of SHA-256 over "receiver\nmessageId", stable across every redelivery.</summary>
    public static Guid KeyOf(string receiver, string messageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(receiver);
        ArgumentException.ThrowIfNullOrWhiteSpace(messageId);
        return new Guid(SHA256.HashData(Encoding.UTF8.GetBytes($"{receiver}\n{messageId}")).AsSpan(0, 16));
    }

    // A message id is global, so the tenant wall is lifted for the mark.
    public Task<bool> HandledAsync(string receiver, string messageId, CancellationToken cancellationToken)
    {
        var key = KeyOf(receiver, messageId);
        return this.context.InboxMessages.IgnoreQueryFilters([QueryFilterNames.Tenant]).AnyAsync(m => m.Id == key, cancellationToken);
    }

    public async Task RecordAsync(string receiver, string messageId, Guid tenantId, CancellationToken cancellationToken)
    {
        var mark = new ClientRequest(KeyOf(receiver, messageId), receiver, DateTime.UtcNow);
        mark.SetTenantId(tenantId);
        await this.context.InboxMessages.AddAsync(mark, cancellationToken);
    }

    public async Task<int> DeleteOlderThanAsync(DateTimeOffset cutoffUtc, int batchSize, CancellationToken cancellationToken)
    {
        var cutoff = cutoffUtc.UtcDateTime;
        return await this.context.InboxMessages
            .IgnoreQueryFilters([QueryFilterNames.Tenant])
            .Where(m => m.Time < cutoff)
            .Take(batchSize)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
