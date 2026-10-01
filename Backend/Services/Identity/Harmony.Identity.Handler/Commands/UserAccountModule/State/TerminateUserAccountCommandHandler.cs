namespace Harmony.Identity.Handler.Commands.UserAccountModule.State;

using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Microsoft.AspNetCore.Identity;

public class TerminateUserAccountCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<TerminateUserAccountCommand, CallResponse>
{
    public TerminateUserAccountCommandHandler(UserManager<HarmonyUser> userManager)
        : base(userManager)
    {
    }

    public async Task<CallResponse> Handle(TerminateUserAccountCommand command, CancellationToken cancellationToken)
    {
        var user = await this.LoadAsync(command.Id);
        if (user is null)
        {
            return Fail(IdentityErrorCodes.UserAccountNotFound);
        }

        user.Terminate(command.Reason);
        await this.SaveAsync(user);

        return Ok();
    }
}
