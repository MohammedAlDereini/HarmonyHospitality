using Harmony.Core.BuildingBlocks.Domain.Abstractions;

namespace Harmony.Identity.Domain.Entities.Aggregates.MakerCheckerModule;

/// <summary>Where an approval stands. Rejected is not in the ERD's "pending approved void": a checker can say no.</summary>
public enum RequestState
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
    Void = 4,
}

/// <summary>
/// A risky change that waits for a second person (design 4.2, the ERD's MakerCheckerRequests). It owns its execution,
/// the accounts the execution touched, the version guard and the quarantine releases: request, approval and effect are
/// one lifecycle. The checker is never the maker; the maker held a live step-up when raising it; and if a guarded
/// account's security version moved, the request is void, because it was reviewed against something that has changed.
/// </summary>
public sealed class MakerCheckerRequest : AggregateRoot<Guid>, IAuditableEntity, IMultiTenantEntity, IConcurrentEntity
{
    public const int KindMaxLength = 64;

    private MakerCheckerRequest()
    {
    }

    private MakerCheckerRequest(Guid id)
        : base(id)
    {
    }

    public Guid TenantId { get; private set; }

    /// <summary>The maker.</summary>
    public Guid RequestedByUserAccountId { get; private set; }

    /// <summary>The maker's proof that it was them, minutes before raising it.</summary>
    public Guid MakerStepUpAuthorizationId { get; private set; }

    /// <summary>What is asked: "account.administrator-roles", "company-login.switch-off".</summary>
    public string Kind { get; private set; } = null!;

    /// <summary>The account it is about, when there is one (not in the ERD; what the list shows).</summary>
    public Guid? TargetUserAccountId { get; private set; }

    /// <summary>The change itself, as the maker sent it (not in the ERD; what the approval runs). Never a secret.</summary>
    public string Payload { get; private set; } = null!;

    public RequestState State { get; private set; }

    public DateTimeOffset RaisedAtUtc { get; private set; }

    public DateTimeOffset? DecidedAtUtc { get; private set; }

    public Guid? DecidedByUserAccountId { get; private set; }

    public ICollection<MakerCheckerExecution> Executions { get; private set; } = new List<MakerCheckerExecution>();

    public ICollection<MakerCheckerExpectedVersion> ExpectedVersions { get; private set; } = new List<MakerCheckerExpectedVersion>();

    public DateTime CreatedDate { get; private set; }

    public string CreatedBy { get; private set; } = null!;

    public DateTime? ModifiedDate { get; private set; }

    public string ModifiedBy { get; private set; } = null!;

    public static MakerCheckerRequest Raise(Guid tenantId, Guid makerId, Guid stepUpId, string kind, Guid? targetUserAccountId, string payload, IEnumerable<(Guid UserAccountId, int Version)> guard, DateTimeOffset now)
    {
        if (tenantId == Guid.Empty || makerId == Guid.Empty || stepUpId == Guid.Empty)
        {
            throw new ArgumentException("A tenant, a maker and the maker's step-up are required.");
        }

        if (string.IsNullOrWhiteSpace(kind) || kind.Length > KindMaxLength || string.IsNullOrWhiteSpace(payload))
        {
            throw new ArgumentException("A kind (at most 64 characters) and the change are required.");
        }

        var request = new MakerCheckerRequest(Guid.CreateVersion7())
        {
            TenantId = tenantId,
            RequestedByUserAccountId = makerId,
            MakerStepUpAuthorizationId = stepUpId,
            Kind = kind,
            TargetUserAccountId = targetUserAccountId,
            Payload = payload,
            State = RequestState.Pending,
            RaisedAtUtc = now.ToUniversalTime(),
        };
        foreach (var (userAccountId, version) in guard.DistinctBy(g => g.UserAccountId))
        {
            request.ExpectedVersions.Add(MakerCheckerExpectedVersion.Of(tenantId, request.Id, userAccountId, version));
        }

        return request;
    }

    /// <summary>The checker ran it: the execution and every account it touched are recorded with it.</summary>
    public MakerCheckerExecution Approve(Guid checkerId, IEnumerable<Guid> touched, DateTimeOffset now)
    {
        Decide(checkerId, now, RequestState.Approved);
        var execution = MakerCheckerExecution.Of(TenantId, Id, checkerId, touched, now);
        Executions.Add(execution);
        return execution;
    }

    public void Reject(Guid checkerId, DateTimeOffset now) => Decide(checkerId, now, RequestState.Rejected);

    /// <summary>A guarded account moved since the request was raised: nothing runs, ever.</summary>
    public void Void(DateTimeOffset now)
    {
        if (State == RequestState.Pending)
        {
            State = RequestState.Void;
            DecidedAtUtc = now.ToUniversalTime();
        }
    }

    private void Decide(Guid checkerId, DateTimeOffset now, RequestState outcome)
    {
        if (State != RequestState.Pending)
        {
            throw new InvalidOperationException("Only a pending request is decided.");
        }

        if (checkerId == RequestedByUserAccountId)
        {
            throw new InvalidOperationException("The checker is never the maker.");
        }

        State = outcome;
        DecidedAtUtc = now.ToUniversalTime();
        DecidedByUserAccountId = checkerId;
    }
}
