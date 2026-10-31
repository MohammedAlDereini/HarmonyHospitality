namespace Harmony.Identity.Handler.Commands.UserAccountModule.SecurityTrail;

using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;

/// <summary>One trail row for an account, saved now: the pages send it for events that change nothing else.</summary>
public class RecordSecurityEventCommandHandler : IRequestHandler<RecordSecurityEventCommand, CallResponse>
{
    private readonly IUserAccountRepository userAccounts;
    private readonly ISecurityAuditEventRepository auditEvents;
    private readonly ISecurityEventFactory securityEvents;

    public RecordSecurityEventCommandHandler(IUserAccountRepository userAccounts, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents)
    {
        this.userAccounts = userAccounts;
        this.auditEvents = auditEvents;
        this.securityEvents = securityEvents;
    }

    public async Task<CallResponse> Handle(RecordSecurityEventCommand command, CancellationToken cancellationToken)
    {
        var user = await this.userAccounts.FindByIdAcrossTenantsAsync(command.UserId, cancellationToken)
            ?? throw new InvalidOperationException($"Account {command.UserId} cannot be found for its security event.");

        await this.auditEvents.AddAsync(this.securityEvents.Create(user, command.TrailAction, command.Detail), cancellationToken);
        await this.auditEvents.SaveChangesAsync(cancellationToken);

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }
}
