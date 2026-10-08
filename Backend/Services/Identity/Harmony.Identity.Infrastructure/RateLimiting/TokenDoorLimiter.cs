using System.Globalization;
using System.Threading.RateLimiting;
using Duende.IdentityServer;
using Duende.IdentityServer.Models;
using Harmony.Core.Errors;
using Harmony.Identity.Infrastructure.IdentityServer;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Harmony.Identity.Infrastructure.RateLimiting;

/// <summary>
/// Item 5, the token door, until BFF step 5 removes the old password grant. Duende answers /connect/token itself, outside
/// routing, so no [EnableRateLimiting] reaches it: this middleware counts the password grant and its second step (mfa_otp)
/// per caller. While both doors exist they share ONE sign-in budget: a sign-in page send is counted here too, so a caller
/// cannot get twice the tries by using both. The BFF's code and refresh calls never count. A token refusal is 429 with
/// Retry-After and an OAuth-shaped error whose description is our code (00075), the way the web app reads token errors.
/// </summary>
public sealed class TokenDoorLimiter
{
    private const string TokenDoor = "token";

    // Duende matches its endpoints by the exact path, ignoring case; this door does the same, so no spelling gets past it.
    private static readonly PathString TokenPath = "/" + IdentityServerConstants.ProtocolRoutePaths.Token;

    private readonly RequestDelegate next;
    private readonly ILogger<TokenDoorLimiter> logger;
    private readonly PartitionedRateLimiter<string> signInBudget;
    private readonly PartitionedRateLimiter<string> warnings = RateLimitPolicies.OncePerMinute();

    public TokenDoorLimiter(RequestDelegate next, IOptions<RateLimitSettings> settings, ILogger<TokenDoorLimiter> logger)
    {
        this.next = next;
        this.logger = logger;
        var limits = settings.Value;
        this.signInBudget = PartitionedRateLimiter.Create<string, string>(caller => RateLimitPolicies.Bucket(caller, limits.SignInBurst, limits.SignInPerMinute));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var door = await DoorOfAsync(context);
        if (door is null)
        {
            await this.next(context);
            return;
        }

        var caller = RateLimitPolicies.CallerOf(context.Connection.RemoteIpAddress);
        using (var lease = this.signInBudget.AttemptAcquire(caller))
        {
            if (!lease.IsAcquired)
            {
                var seconds = RateLimitPolicies.SecondsToWait(lease);
                this.WarnOnce(door, caller, seconds);
                if (door == TokenDoor)
                {
                    await WriteTokenRefusalAsync(context, seconds);
                }
                else
                {
                    await RateLimitPolicies.WriteRefusalAsync(context, seconds, context.RequestAborted);
                }

                return;
            }
        }

        await this.next(context);
    }

    /// <summary>"token" for a password or mfa_otp grant, "sign-in" for a sign-in page send, null for anything else.</summary>
    private static async Task<string?> DoorOfAsync(HttpContext context)
    {
        if (RateLimitPolicies.IsSend(context.Request.Method)
            && context.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName == RateLimitPolicies.SignIn)
        {
            return RateLimitPolicies.SignIn;
        }

        return await IsPasswordGrantAsync(context) ? TokenDoor : null;
    }

    private static async Task<bool> IsPasswordGrantAsync(HttpContext context)
    {
        // Exactly what Duende accepts before it reads anything (POST, this path, a urlencoded form): never read a body Duende
        // would refuse unread, such as a large multipart upload.
        if (!HttpMethods.IsPost(context.Request.Method)
            || !context.Request.Path.Equals(TokenPath, StringComparison.OrdinalIgnoreCase)
            || !MediaTypeHeaderValue.TryParse(context.Request.ContentType, out var type)
            || !type.MediaType.Equals("application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        IFormCollection form;
        try
        {
            // Read once here. ASP.NET keeps the result, so Duende gets the same form, or the same error, which it answers itself.
            form = await context.Request.ReadFormAsync(context.RequestAborted);
        }
        catch (InvalidDataException)
        {
            return false;
        }

        // Any grant_type value counts (a repeated parameter cannot slip past), in any case (counting more is the safe side).
        return form["grant_type"].Any(grant =>
            string.Equals(grant, GrantType.ResourceOwnerPassword, StringComparison.OrdinalIgnoreCase)
            || string.Equals(grant, MfaOtpGrantValidator.GrantTypeName, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task WriteTokenRefusalAsync(HttpContext context, int seconds)
    {
        context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        context.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);
        context.Response.Headers.CacheControl = "no-store";
        await context.Response.WriteAsJsonAsync(
            new { error = "too_many_attempts", error_description = BusinessErrorCodes.Identity.UserAccount.TooManyAttempts },
            context.RequestAborted);
    }

    private void WarnOnce(string door, string caller, int seconds)
    {
        using var warning = this.warnings.AttemptAcquire($"{door}|{caller}");
        if (warning.IsAcquired)
        {
            this.logger.LogWarning("Too many tries at the {Door} door from {Caller}; refused for {Seconds} s. More refusals for this caller and door within a minute are not logged.", door, caller, seconds);
        }
    }
}
