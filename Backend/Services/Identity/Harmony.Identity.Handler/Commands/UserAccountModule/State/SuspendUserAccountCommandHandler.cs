namespace Harmony.Identity.Handler.Commands.UserAccountModule.State;

using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;

public class SuspendUserAccountCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<SuspendUserAccountCommand, CallResponse>
{
    public SuspendUserAccountCommandHandler(UserManager<User> userManager, ICacheService cacheService)
        : base(userManager, cacheService)
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

        // Suspend bumped the version: every token this user holds dies at its next request.
        await this.PublishSecurityVersionAsync(user, cancellationToken);

        return Ok();
    }
}
