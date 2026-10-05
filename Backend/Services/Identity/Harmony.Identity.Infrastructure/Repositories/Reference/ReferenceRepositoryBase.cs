using System.Data;
using Harmony.Core.BuildingBlocks.Domain.Abstractions;
using Harmony.Identity.Domain.Entities.Reference;
using Harmony.Identity.Infrastructure.Persistence;

namespace Harmony.Identity.Infrastructure.Repositories.Reference;

/// <summary>
/// The unit-of-work part shared by the reference repositories. Reference entities carry no Guid id, so the framework
/// BaseRepository (which needs IBaseEntity) does not fit; the context itself is the unit of work.
/// Abstract and generic: the repository scanner skips it and registers only the concrete repositories.
/// </summary>
public abstract class ReferenceRepositoryBase<T> : IRepositoryLight<T>
    where T : ReferenceEntity
{
    protected ReferenceRepositoryBase(IdentityDbContext context)
    {
        this.Context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public IUnitOfWork UnitOfWork => this.Context;

    protected IdentityDbContext Context { get; }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await this.Context.SaveChangesAsync(cancellationToken);
    }

    public IResilientTransaction InTransaction(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted)
    {
        return this.Context.InTransaction(isolationLevel);
    }
}
