namespace Harmony.Identity.Handler.Commands.UserAccountModule.Sessions;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

/// <summary>
/// "Log me out everywhere": the caller's SecurityVersion bumps, so every access token on every device dies at its next
/// request, and the caller's refresh tokens are removed, so none can be refreshed. The caller's own token included:
/// the person signs in again once. Same mechanism an admin triggers with Suspend, here chosen by the person.
/// </summary>
public class LogoutEverywhereCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<LogoutEverywhereCommand, CallResponse>
{
    private readonly ICallContext callContext;
    private readonly ILogger<LogoutEverywhereCommandHandler> logger;

    public LogoutEverywhereCommandHandler(UserManager<User> userManager, ICacheService cacheService, ISessionRevoker sessionRevoker, ICallContext callContext, ILogger<LogoutEverywhereCommandHandler> logger)
        : base(userManager, cacheService, sessionRevoker)
    {
        this.callContext = callContext;
        this.logger = logger;
    }

    public async Task<CallResponse> Handle(LogoutEverywhereCommand command, CancellationToken cancellationToken)
    {
        // The framework copied the token's sub into the call context; anything else is not a person's token.
        if (!Guid.TryParse(this.callContext.UserId(), out var userId))
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        var user = await this.LoadAsync(userId);
        if (user is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        user.EndAllSessions();
        await this.SaveAsync(user);

        // The bump is published and the refresh tokens go: out on every device, the caller included.
        await this.PublishSecurityVersionAsync(user, cancellationToken);
        this.logger.LogInformation("Account {UserId} logged out everywhere; SecurityVersion is now {Version}.", user.Id, user.SecurityVersion);

        return Ok();
    }
}
