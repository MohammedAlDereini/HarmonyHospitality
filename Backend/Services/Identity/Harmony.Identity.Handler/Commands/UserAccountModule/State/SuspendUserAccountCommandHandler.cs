namespace Harmony.Identity.Handler.Commands.UserAccountModule.State;

using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Microsoft.AspNetCore.Identity;

public class SuspendUserAccountCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<SuspendUserAccountCommand, CallResponse>
{
    public SuspendUserAccountCommandHandler(UserManager<HarmonyUser> userManager)
        : base(userManager)
    {
    }

    public async Task<CallResponse> Handle(SuspendUserAccountCommand command, CancellationToken cancellationToken)
    {
        var user = await this.LoadAsync(command.Id);
        if (user is null)
        {
            return Fail(IdentityErrorCodes.UserAccountNotFound);
        }

        user.Suspend(command.Reason);
        await this.SaveAsync(user);

        return Ok();
    }
}
