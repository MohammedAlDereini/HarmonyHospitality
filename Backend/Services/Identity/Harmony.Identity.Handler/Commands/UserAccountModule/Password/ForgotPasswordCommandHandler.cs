namespace Harmony.Identity.Handler.Commands.UserAccountModule.Password;

using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

/// <summary>
/// "Forgot password": if exactly one active account carries this e-mail, a one-time reset link goes to it. Everything else
/// (unknown address, two tenants with the same address, suspended account, mail trouble) is logged and answered with the
/// same "ok", so the endpoint cannot be used to learn which addresses exist. One mail per account per minute; the token
/// itself expires after 24 hours and dies with the first password change (it is bound to the security stamp).
/// </summary>
public class ForgotPasswordCommandHandler : IRequestHandler<ForgotPasswordCommand, CallResponse>
{
    public static readonly TimeSpan ResendWindow = TimeSpan.FromSeconds(60);
    private const string ThrottleKeyPrefix = "identity:reset-sent:";

    private readonly IUserAccountRepository userAccounts;
    private readonly UserManager<User> userManager;
    private readonly IAccountMailer mailer;
    private readonly ICacheService cacheService;
    private readonly ILogger<ForgotPasswordCommandHandler> logger;

    public ForgotPasswordCommandHandler(IUserAccountRepository userAccounts, UserManager<User> userManager, IAccountMailer mailer, ICacheService cacheService, ILogger<ForgotPasswordCommandHandler> logger)
    {
        this.userAccounts = userAccounts;
        this.userManager = userManager;
        this.mailer = mailer;
        this.cacheService = cacheService;
        this.logger = logger;
    }

    public async Task<CallResponse> Handle(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        // Anonymous call: no tenant is known, so the lookup runs across tenants and must hit exactly one account.
        var normalized = this.userManager.NormalizeName(command.Email.Trim());
        var candidates = await this.userAccounts.FindByUserNameAcrossTenantsAsync(normalized, cancellationToken);

        if (candidates.Count != 1)
        {
            this.logger.LogInformation("Password reset not sent: {Count} accounts match the address.", candidates.Count);
            return Ok();
        }

        var user = candidates[0];
        if (!user.CanSignIn)
        {
            this.logger.LogInformation("Password reset not sent: account {UserId} is {State}.", user.Id, user.State);
            return Ok();
        }

        var throttleKey = ThrottleKeyPrefix + user.Id.ToString("D");
        if (await this.cacheService.ExistsAsync(throttleKey, cancellationToken))
        {
            this.logger.LogInformation("Password reset not sent: account {UserId} already got one within the last minute.", user.Id);
            return Ok();
        }

        var token = await this.userManager.GeneratePasswordResetTokenAsync(user);

        try
        {
            await this.mailer.SendPasswordResetAsync(user, token, cancellationToken);
            await this.cacheService.AddAsync(throttleKey, "1", ResendWindow, cancellationToken);
        }
        catch (Exception exception)
        {
            // The person is told nothing different; the operator sees it here.
            this.logger.LogError(exception, "Password reset mail could not be sent for account {UserId}.", user.Id);
        }

        return Ok();
    }

    private static CallResponse Ok()
        => CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
}
