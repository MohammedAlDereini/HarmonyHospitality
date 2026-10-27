namespace Harmony.Identity.Handler.Queries.ApprovalModule;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;

/// <summary>A change waiting for a second administrator.</summary>
public class ApprovalRequestModel
{
    public Guid Id { get; set; }

    public string Kind { get; set; } = null!;

    public Guid RequestedById { get; set; }

    public string? RequestedBy { get; set; }

    public Guid? TargetId { get; set; }

    public string? Target { get; set; }

    public DateTimeOffset RaisedAtUtc { get; set; }
}

/// <summary>The caller's customer's waiting requests, oldest first.</summary>
public class GetApprovalRequestsQuery : BaseQueryRequest<CallResponse<List<ApprovalRequestModel>>>
{
}

public class GetApprovalRequestsQueryHandler : IRequestHandler<GetApprovalRequestsQuery, CallResponse<List<ApprovalRequestModel>>>
{
    private readonly IMakerCheckerRequestRepository requests;
    private readonly IUserAccountRepository userAccounts;

    public GetApprovalRequestsQueryHandler(IMakerCheckerRequestRepository requests, IUserAccountRepository userAccounts)
    {
        this.requests = requests;
        this.userAccounts = userAccounts;
    }

    public async Task<CallResponse<List<ApprovalRequestModel>>> Handle(GetApprovalRequestsQuery request, CancellationToken cancellationToken)
    {
        var rows = await this.requests.PendingAsync(cancellationToken);
        var names = new Dictionary<Guid, string>();
        foreach (var id in rows.Select(r => r.RequestedByUserAccountId).Concat(rows.Where(r => r.TargetUserAccountId is not null).Select(r => r.TargetUserAccountId!.Value)).Distinct())
        {
            if (await this.userAccounts.FindByIdAcrossTenantsAsync(id, cancellationToken) is User person)
            {
                names[id] = person.DisplayName;
            }
        }

        var models = rows.Select(r => new ApprovalRequestModel
        {
            Id = r.Id,
            Kind = r.Kind,
            RequestedById = r.RequestedByUserAccountId,
            RequestedBy = names.GetValueOrDefault(r.RequestedByUserAccountId),
            TargetId = r.TargetUserAccountId,
            Target = r.TargetUserAccountId is { } target ? names.GetValueOrDefault(target) : null,
            RaisedAtUtc = r.RaisedAtUtc,
        }).ToList();
        return CallResponseBuilder.CreateResponse<List<ApprovalRequestModel>>(eCallResponseStatus.Success).HasData(models);
    }
}
