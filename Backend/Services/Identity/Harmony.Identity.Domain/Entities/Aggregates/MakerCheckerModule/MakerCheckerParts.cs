using Harmony.Core.BuildingBlocks.Domain.Abstractions;

namespace Harmony.Identity.Domain.Entities.Aggregates.MakerCheckerModule;

/// <summary>The approval being carried out (the ERD's MakerCheckerExecutions): who approved it, when.</summary>
public sealed class MakerCheckerExecution : BaseEntity<Guid>, IMultiTenantEntity
{
    private MakerCheckerExecution()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid RequestId { get; private set; }

    public Guid CheckerUserAccountId { get; private set; }

    public DateTimeOffset ExecutedAtUtc { get; private set; }

    public ICollection<MakerCheckerExecutionItem> Items { get; private set; } = new List<MakerCheckerExecutionItem>();

    internal static MakerCheckerExecution Of(Guid tenantId, Guid requestId, Guid checkerId, IEnumerable<Guid> touched, DateTimeOffset now)
    {
        // Ids are EF's, assigned when the save tracks the rows; the foreign keys follow the navigations.
        var execution = new MakerCheckerExecution
        {
            TenantId = tenantId,
            RequestId = requestId,
            CheckerUserAccountId = checkerId,
            ExecutedAtUtc = now.ToUniversalTime(),
        };
        foreach (var userAccountId in touched.Distinct())
        {
            execution.Items.Add(MakerCheckerExecutionItem.Of(tenantId, userAccountId));
        }

        return execution;
    }
}

/// <summary>An account the execution changed (the ERD's MakerCheckerExecutionItems).</summary>
public sealed class MakerCheckerExecutionItem : BaseEntity<Guid>, IMultiTenantEntity
{
    private MakerCheckerExecutionItem()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid ExecutionId { get; private set; }

    public Guid UserAccountId { get; private set; }

    internal static MakerCheckerExecutionItem Of(Guid tenantId, Guid userAccountId)
        => new() { TenantId = tenantId, UserAccountId = userAccountId };
}

/// <summary>
/// The version guard (the ERD's MakerCheckerExpectedVersions): the account's SecurityVersion when the request was raised.
/// If it moved before the approval, the request is void. UserAccountId is not in the ERD; it says whose version it is.
/// </summary>
public sealed class MakerCheckerExpectedVersion : BaseEntity<Guid>, IMultiTenantEntity
{
    private MakerCheckerExpectedVersion()
    {
    }

    public Guid TenantId { get; private set; }

    public Guid RequestId { get; private set; }

    public Guid UserAccountId { get; private set; }

    public int ExpectedVersion { get; private set; }

    internal static MakerCheckerExpectedVersion Of(Guid tenantId, Guid requestId, Guid userAccountId, int version)
        => new() { TenantId = tenantId, RequestId = requestId, UserAccountId = userAccountId, ExpectedVersion = version };
}
