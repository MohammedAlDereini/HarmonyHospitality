namespace Harmony.Identity.Handler.Commands.UserAccountModule.State;

using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;

public class ReinstateUserAccountCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<ReinstateUserAccountCommand, CallResponse>
{
    public ReinstateUserAccountCommandHandler(UserManager<User> userManager, ICacheService cacheService)
        : base(userManager, cacheService)
    {
    }

    public async Task<CallResponse> Handle(ReinstateUserAccountCommand command, CancellationToken cancellationToken)
    {
        var user = await this.LoadAsync(command.Id);
        if (user is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        // Reinstate kills nothing, so the version stays and nothing is published: the user signs in again.
        user.Reinstate();
        await this.SaveAsync(user);

        return Ok();
    }
}
