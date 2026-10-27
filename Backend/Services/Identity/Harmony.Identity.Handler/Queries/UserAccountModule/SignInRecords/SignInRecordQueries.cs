namespace Harmony.Identity.Handler.Queries.UserAccountModule.SignInRecords;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Microsoft.AspNetCore.Identity;

/// <summary>One invitation still waiting (or expired) for the caller's customer.</summary>
public class InvitationModel
{
    public Guid Id { get; set; }

    public Guid UserAccountId { get; set; }

    public string DisplayName { get; set; } = null!;

    public string? Email { get; set; }

    public string? SentBy { get; set; }

    public DateTime SentAtUtc { get; set; }

    public DateTimeOffset ExpiresAtUtc { get; set; }

    public bool IsExpired { get; set; }
}

/// <summary>One sign-in of an account, from start to end.</summary>
public class SessionRecordModel
{
    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? EndedAtUtc { get; set; }

    public string? EndReason { get; set; }

    public string Methods { get; set; } = null!;

    public string Provider { get; set; } = null!;

    public bool MultiFactorSatisfied { get; set; }
}

/// <summary>The caller's customer's waiting invitations, newest first.</summary>
public class GetInvitationsQuery : BaseQueryRequest<CallResponse<List<InvitationModel>>>
{
}

/// <summary>An account's latest sign-ins (the newest 50), inside the caller's customer.</summary>
public class GetSessionRecordsQuery : BaseQueryRequest<CallResponse<List<SessionRecordModel>>>
{
    public const int Take = 50;

    public Guid Id { get; set; }
}

public class GetInvitationsQueryHandler : IRequestHandler<GetInvitationsQuery, CallResponse<List<InvitationModel>>>
{
    private readonly ILocalStaffEnrolmentRepository enrolments;
    private readonly IUserAccountRepository userAccounts;
    private readonly ICallContext callContext;
    private readonly TimeProvider clock;

    public GetInvitationsQueryHandler(ILocalStaffEnrolmentRepository enrolments, IUserAccountRepository userAccounts, ICallContext callContext, TimeProvider clock)
    {
        this.enrolments = enrolments;
        this.userAccounts = userAccounts;
        this.callContext = callContext;
        this.clock = clock;
    }

    public async Task<CallResponse<List<InvitationModel>>> Handle(GetInvitationsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = Guid.Parse(this.callContext.TenantId());
        var rows = await this.enrolments.PendingOfTenantAsync(tenantId, cancellationToken);
        var ids = rows.Select(r => r.UserAccountId).Concat(rows.Select(r => r.SponsorUserAccountId)).Distinct().ToList();
        var people = new Dictionary<Guid, User>();
        foreach (var id in ids)
        {
            if (await this.userAccounts.FindByIdAcrossTenantsAsync(id, cancellationToken) is { } person)
            {
                people[id] = person;
            }
        }

        var now = this.clock.GetUtcNow();
        var models = rows
            .Where(r => people.ContainsKey(r.UserAccountId) && people[r.UserAccountId].PasswordHash is null)
            .Select(r => new InvitationModel
            {
                Id = r.Id,
                UserAccountId = r.UserAccountId,
                DisplayName = people[r.UserAccountId].DisplayName,
                Email = people[r.UserAccountId].Email,
                SentBy = people.TryGetValue(r.SponsorUserAccountId, out var sponsor) ? sponsor.DisplayName : null,
                SentAtUtc = r.CreatedDate,
                ExpiresAtUtc = r.ExpiresAtUtc,
                IsExpired = !r.IsOpen(now),
            })
            .ToList();
        return CallResponseBuilder.CreateResponse<List<InvitationModel>>(eCallResponseStatus.Success).HasData(models);
    }
}

public class GetSessionRecordsQueryHandler : IRequestHandler<GetSessionRecordsQuery, CallResponse<List<SessionRecordModel>>>
{
    private readonly UserManager<User> userManager;
    private readonly IOperatorSessionRepository sessions;

    public GetSessionRecordsQueryHandler(UserManager<User> userManager, IOperatorSessionRepository sessions)
    {
        this.userManager = userManager;
        this.sessions = sessions;
    }

    public async Task<CallResponse<List<SessionRecordModel>>> Handle(GetSessionRecordsQuery request, CancellationToken cancellationToken)
    {
        // UserManager walls the read: another customer's account is not found, so neither are its sessions.
        var user = await this.userManager.FindByIdAsync(request.Id.ToString());
        if (user is null)
        {
            return CallResponseBuilder.CreateResponse<List<SessionRecordModel>>(eCallResponseStatus.BusinessValidation).HasErrors(Error.New(BusinessErrorCodes.Identity.UserAccount.NotFound));
        }

        var rows = await this.sessions.LatestOfAccountAsync(user.TenantId, user.Id, GetSessionRecordsQuery.Take, cancellationToken);
        var models = rows.Select(s => new SessionRecordModel
        {
            StartedAtUtc = s.StartedAtUtc,
            EndedAtUtc = s.EndedAtUtc,
            EndReason = s.EndReason,
            Methods = s.Methods,
            Provider = s.Provider,
            MultiFactorSatisfied = s.MultiFactorSatisfied,
        }).ToList();
        return CallResponseBuilder.CreateResponse<List<SessionRecordModel>>(eCallResponseStatus.Success).HasData(models);
    }
}
