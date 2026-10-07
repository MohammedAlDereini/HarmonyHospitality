using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Models.UserAccountModule;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.UserAccountModule.Security;

public class GetSecurityEventsQueryHandler : IRequestHandler<GetSecurityEventsQuery, CallResponse<List<SecurityEventModel>>>
{
    public const int Take = 100;

    private readonly IUserAccountRepository userAccounts;
    private readonly ISecurityAuditEventRepository events;
    private readonly ICallContext callContext;

    public GetSecurityEventsQueryHandler(IUserAccountRepository userAccounts, ISecurityAuditEventRepository events, ICallContext callContext)
    {
        this.userAccounts = userAccounts;
        this.events = events;
        this.callContext = callContext;
    }

    public async Task<CallResponse<List<SecurityEventModel>>> Handle(GetSecurityEventsQuery request, CancellationToken cancellationToken)
    {
        var id = request.Id ?? (Guid.TryParse(this.callContext.UserId(), out var caller) ? caller : Guid.Empty);

        // The account must be one of the caller's tenant: the tenant filter on this query is the wall.
        if (id == Guid.Empty || !await this.userAccounts.Query().AnyAsync(a => a.Id == id, cancellationToken))
        {
            return CallResponseBuilder.CreateResponse<List<SecurityEventModel>>(eCallResponseStatus.BusinessValidation)
                .HasErrors(Error.New(BusinessErrorCodes.Identity.UserAccount.NotFound));
        }

        var trail = await this.events.Query()
            .Where(e => e.UserAccountId == id)
            .OrderByDescending(e => e.OccurredAtUtc)
            .Take(Take)
            .Select(e => new SecurityEventModel
            {
                Action = e.Action,
                OccurredAtUtc = e.OccurredAtUtc,
                ActorSubject = e.ActorSubject,
                SourceAddress = e.SourceAddress,
                Device = e.Device,
            })
            .ToListAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse<List<SecurityEventModel>>(eCallResponseStatus.Success).HasData(trail);
    }
}
