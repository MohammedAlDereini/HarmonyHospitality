namespace Harmony.Identity.Handler.Queries.RevocationModule;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Repositories;

/// <summary>One line of the feed: from this number on, the account's tokens below NewSecurityVersion are dead.</summary>
public class RevocationEntryModel
{
    public long Sequence { get; set; }

    public Guid UserAccountId { get; set; }

    public string Kind { get; set; } = null!;

    public int NewSecurityVersion { get; set; }

    public Guid OperationId { get; set; }

    public DateTimeOffset OccurredAtUtc { get; set; }
}

/// <summary>
/// A page of a customer's feed. A reader whose position is below MinimumRetainedSequence - 1 missed pruned lines:
/// MustLoadSnapshot says so, and it loads the snapshot before reading on. It never treats a gap as "nothing happened".
/// </summary>
public class RevocationFeedModel
{
    public Guid TenantId { get; set; }

    public long HighWatermark { get; set; }

    public long MinimumRetainedSequence { get; set; }

    public long? PrunedThrough { get; set; }

    public bool MustLoadSnapshot { get; set; }

    public List<RevocationEntryModel> Entries { get; set; } = [];
}

/// <summary>A page of the customer's newest snapshot: every account's SecurityVersion at TakenAtSequence.</summary>
public class RevocationSnapshotModel
{
    public Guid? SnapshotId { get; set; }

    public long TakenAtSequence { get; set; }

    public DateTimeOffset? TakenAtUtc { get; set; }

    public List<RevocationSnapshotItemModel> Items { get; set; } = [];
}

public class RevocationSnapshotItemModel
{
    public long PageKey { get; set; }

    public Guid UserAccountId { get; set; }

    public int SecurityVersion { get; set; }
}

/// <summary>The feed of one customer after a position (0 = from the start), oldest first. Platform only.</summary>
public class GetRevocationFeedQuery : BaseQueryRequest<CallResponse<RevocationFeedModel>>
{
    public const int MaxTake = 1000;

    public Guid TenantId { get; set; }

    public long After { get; set; }

    public int Take { get; set; } = 500;

    public class GetRevocationFeedQueryValidation : AbstractValidator<GetRevocationFeedQuery>
    {
        public GetRevocationFeedQueryValidation()
        {
            this.RuleFor(e => e.TenantId).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.IdRequired);
        }
    }
}

/// <summary>A page of the customer's newest snapshot, after a page key (0 = from the start). Platform only.</summary>
public class GetRevocationSnapshotQuery : BaseQueryRequest<CallResponse<RevocationSnapshotModel>>
{
    public const int MaxTake = 5000;

    public Guid TenantId { get; set; }

    public long AfterPageKey { get; set; }

    public int Take { get; set; } = 1000;

    public class GetRevocationSnapshotQueryValidation : AbstractValidator<GetRevocationSnapshotQuery>
    {
        public GetRevocationSnapshotQueryValidation()
        {
            this.RuleFor(e => e.TenantId).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.IdRequired);
        }
    }
}

public class GetRevocationFeedQueryHandler : IRequestHandler<GetRevocationFeedQuery, CallResponse<RevocationFeedModel>>
{
    private readonly IRevocationFeedRepository feed;

    public GetRevocationFeedQueryHandler(IRevocationFeedRepository feed)
    {
        this.feed = feed;
    }

    public async Task<CallResponse<RevocationFeedModel>> Handle(GetRevocationFeedQuery request, CancellationToken cancellationToken)
    {
        var model = new RevocationFeedModel { TenantId = request.TenantId, MinimumRetainedSequence = 1 };
        var sequence = await this.feed.SequenceOfAsync(request.TenantId, cancellationToken);
        if (sequence is not null)
        {
            var after = Math.Max(0, request.After);
            var rows = await this.feed.FeedAfterAsync(request.TenantId, after, Math.Clamp(request.Take, 1, GetRevocationFeedQuery.MaxTake), cancellationToken);
            model.HighWatermark = sequence.HighWatermark;
            model.MinimumRetainedSequence = sequence.MinimumRetainedSequence;
            model.PrunedThrough = (await this.feed.LatestTombstoneAsync(request.TenantId, cancellationToken))?.ThroughSequence;
            model.MustLoadSnapshot = after + 1 < sequence.MinimumRetainedSequence;
            model.Entries = rows.Select(e => new RevocationEntryModel
            {
                Sequence = e.Sequence,
                UserAccountId = e.UserAccountId,
                Kind = e.Kind.ToString(),
                NewSecurityVersion = e.NewSecurityVersion,
                OperationId = e.CorrelationId,
                OccurredAtUtc = e.OccurredAtUtc,
            }).ToList();
        }

        return CallResponseBuilder.CreateResponse<RevocationFeedModel>(eCallResponseStatus.Success).HasData(model);
    }
}

public class GetRevocationSnapshotQueryHandler : IRequestHandler<GetRevocationSnapshotQuery, CallResponse<RevocationSnapshotModel>>
{
    private readonly IRevocationFeedRepository feed;

    public GetRevocationSnapshotQueryHandler(IRevocationFeedRepository feed)
    {
        this.feed = feed;
    }

    public async Task<CallResponse<RevocationSnapshotModel>> Handle(GetRevocationSnapshotQuery request, CancellationToken cancellationToken)
    {
        var model = new RevocationSnapshotModel();
        if (await this.feed.LatestSnapshotAsync(request.TenantId, cancellationToken) is { } snapshot)
        {
            var items = await this.feed.SnapshotPageAsync(snapshot.Id, Math.Max(0, request.AfterPageKey), Math.Clamp(request.Take, 1, GetRevocationSnapshotQuery.MaxTake), cancellationToken);
            model.SnapshotId = snapshot.Id;
            model.TakenAtSequence = snapshot.TakenAtSequence;
            model.TakenAtUtc = snapshot.TakenAtUtc;
            model.Items = items.Select(i => new RevocationSnapshotItemModel { PageKey = i.PageKey, UserAccountId = i.UserAccountId, SecurityVersion = i.SecurityVersion }).ToList();
        }

        return CallResponseBuilder.CreateResponse<RevocationSnapshotModel>(eCallResponseStatus.Success).HasData(model);
    }
}
