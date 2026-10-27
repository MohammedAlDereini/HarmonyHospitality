using Harmony.Core.Abstractions;
using Harmony.Core.Identity.Permissions;
using Harmony.Identity.Domain.Repositories;

namespace Harmony.Identity.Handler.Queries.UserAccountModule.Access;

/// <summary>MayActAt for the caller, then the roles that reach the hotel. MayActAt published the copy; this answers the asking service.</summary>
public class GetMyAccessAtQueryHandler : IRequestHandler<GetMyAccessAtQuery, CallResponse<HotelAccess>>
{
    private readonly IMediator mediator;
    private readonly ICallContext callContext;
    private readonly IUserAccountRepository userAccounts;
    private readonly TimeProvider clock;

    public GetMyAccessAtQueryHandler(IMediator mediator, ICallContext callContext, IUserAccountRepository userAccounts, TimeProvider clock)
    {
        this.mediator = mediator;
        this.callContext = callContext;
        this.userAccounts = userAccounts;
        this.clock = clock;
    }

    public async Task<CallResponse<HotelAccess>> Handle(GetMyAccessAtQuery request, CancellationToken cancellationToken)
    {
        var access = new HotelAccess();
        if (Guid.TryParse(this.callContext.UserId(), out var userId) && Guid.TryParse(this.callContext.TenantId(), out var tenantId))
        {
            var answer = await this.mediator.Send(new MayActAtQuery { UserId = userId, TenantId = tenantId, PropertyId = request.PropertyId }, cancellationToken);
            if (answer.Status == eCallResponseStatus.Success && answer.Data)
            {
                access.Allowed = true;
                access.RoleIds = (await this.userAccounts.RoleIdsAtHotelAsync(userId, request.PropertyId, this.clock.GetUtcNow(), cancellationToken)).ToList();
            }
        }

        return CallResponseBuilder.CreateResponse<HotelAccess>(eCallResponseStatus.Success).HasData(access);
    }
}
