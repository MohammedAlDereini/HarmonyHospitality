namespace Harmony.Identity.Handler.Commands.UserAccountModule.State;

using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;

public class TerminateUserAccountCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<TerminateUserAccountCommand, CallResponse>
{
    public TerminateUserAccountCommandHandler(UserManager<User> userManager, ICacheService cacheService)
        : base(userManager, cacheService)
    {
    }

    public async Task<CallResponse> Handle(TerminateUserAccountCommand command, CancellationToken cancellationToken)
    {
        var user = await this.LoadAsync(command.Id);
        if (user is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        user.Terminate(command.Reason);
        await this.SaveAsync(user);

        // Terminate bumped the version: every token this user holds dies at its next request.
        await this.PublishSecurityVersionAsync(user, cancellationToken);

        return Ok();
    }
}
