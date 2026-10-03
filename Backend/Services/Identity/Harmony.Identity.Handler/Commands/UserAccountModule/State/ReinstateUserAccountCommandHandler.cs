namespace Harmony.Identity.Handler.Commands.UserAccountModule.State;

using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Microsoft.AspNetCore.Identity;

public class ReinstateUserAccountCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<ReinstateUserAccountCommand, CallResponse>
{
    public ReinstateUserAccountCommandHandler(UserManager<User> userManager)
        : base(userManager)
    {
    }

    public async Task<CallResponse> Handle(ReinstateUserAccountCommand command, CancellationToken cancellationToken)
    {
        var user = await this.LoadAsync(command.Id);
        if (user is null)
        {
            return Fail(IdentityErrorCodes.UserAccountNotFound);
        }

        user.Reinstate();
        await this.SaveAsync(user);

        return Ok();
    }
}
