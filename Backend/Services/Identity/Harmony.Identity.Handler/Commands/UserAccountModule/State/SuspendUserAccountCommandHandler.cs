namespace Harmony.Identity.Handler.Commands.UserAccountModule.State;

using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Microsoft.AspNetCore.Identity;

public class SuspendUserAccountCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<SuspendUserAccountCommand, CallResponse>
{
    public SuspendUserAccountCommandHandler(UserManager<User> userManager)
        : base(userManager)
    {
    }

    public async Task<CallResponse> Handle(SuspendUserAccountCommand command, CancellationToken cancellationToken)
    {
        var user = await this.LoadAsync(command.Id);
        if (user is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        user.Suspend(command.Reason);
        await this.SaveAsync(user);

        return Ok();
    }
}
