using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Threading.RateLimiting;
using Harmony.Core.Errors;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Infrastructure.RateLimiting;

/// <summary>
/// Item 5: how many tries one caller gets at the anonymous doors (the numbers are RateLimitSettings). Only sends count:
/// opening a page, HEAD and the browser's CORS check never do. A refusal is 429 with Retry-After and our usual error
/// envelope (00075). The counts live in this server's memory: with two or more servers each counts on its own (later.md).
/// </summary>
public static class RateLimitPolicies
{
    public const string SignIn = "sign-in";
    public const string ForgotPassword = "forgot-password";
    public const string ResetPassword = "reset-password";

    /// <summary>The passkey doors behind a session (a challenge, an answer, a proof of identity): counted per PERSON, not per address.</summary>
    public const string Passkeys = "passkeys";

    /// <summary>The sign-in family's refusal page (Pages/Account/TooManyTries), where a refused sign-in send is redirected.</summary>
    public const string RefusalPage = "/account/toomanytries";

    public static IServiceCollection AddSignInRateLimits(this IServiceCollection services)
    {
        // The middleware's services and built-in metrics. Microsoft's default refusal is 503 (server down); ours is 429.
        services.AddRateLimiter(options => options.RejectionStatusCode = StatusCodes.Status429TooManyRequests);

        // The rules, built once with our settings and a logger injected (options pattern), never looked up per request.
        services.AddOptions<RateLimiterOptions>()
            .Configure<IOptions<RateLimitSettings>, ILoggerFactory>((options, settings, loggers) =>
            {
                var limits = settings.Value;
                var logger = loggers.CreateLogger(typeof(RateLimitPolicies));

                // One warning per caller and door a minute: an attacker's thousand refusals must not flood the log. The
                // built-in metrics (aspnetcore.rate_limiting) still count every refusal.
                var warnings = OncePerMinute();
                options.OnRejected = (context, cancellationToken) => RefuseAsync(context, logger, warnings, cancellationToken);

                // A burst a hotel's shift change fits in, then a steady pace.
                options.AddPolicy(SignIn, context => SendsOnly(context, caller => Bucket(caller, limits.SignInBurst, limits.SignInPerMinute)));

                options.AddPolicy(ForgotPassword, context => SendsOnly(context, caller =>
                    Window(caller, limits.ForgotPasswordPerWindow, limits.WindowMinutes)));

                // Also the invitation door: a burst an onboarding day fits in.
                options.AddPolicy(ResetPassword, context => SendsOnly(context, caller => Bucket(caller, limits.ResetPasswordBurst, limits.ResetPasswordPerMinute)));

                // Item 6 (OWASP: rate-limit registration): a signed-in person asks for a passkey challenge a few times, a script a
                // thousand times. Counted by the token's subject; a call without one falls back to the address.
                options.AddPolicy(Passkeys, context => IsSend(context.Request.Method)
                    ? Bucket(PersonOf(context), limits.PasskeyBurst, limits.PasskeyPerMinute)
                    : RateLimitPartition.GetNoLimiter("read"));
            });

        return services;
    }

    /// <summary>
    /// Who is counted: one IPv4 address, or one IPv6 /64. A home or office line gets a whole /64, so counting single IPv6
    /// addresses would let one caller take a new address at every try.
    /// </summary>
    public static string CallerOf(IPAddress? address)
    {
        if (address is null)
        {
            return "unknown";
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
        {
            return address.ToString();
        }

        var bytes = address.GetAddressBytes();
        Array.Clear(bytes, 8, 8);
        return new IPAddress(bytes) + "/64";
    }

    /// <summary>The signed-in person behind a call (the token's subject), else the caller's address.</summary>
    internal static string PersonOf(HttpContext context)
        => context.User.FindFirst("sub")?.Value is { Length: > 0 } subject ? "sub:" + subject : CallerOf(context.Connection.RemoteIpAddress);

    /// <summary>A send counts; opening a page (GET), HEAD and the browser's CORS check (OPTIONS) never do.</summary>
    internal static bool IsSend(string method)
        => !(HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method));

    /// <summary>
    /// The refusal at our own doors. A sign-in page send is redirected (303, Post/Redirect/Get) to the refusal page, which a
    /// person reads in the sign-in family's look, and a browser refresh never sends the refused form again. The API gets 429,
    /// how long to wait, and our usual error envelope (00075), because the web app reads it.
    /// </summary>
    internal static async Task WriteRefusalAsync(HttpContext http, int seconds, CancellationToken cancellationToken)
    {
        http.Response.Headers.RetryAfter = seconds.ToString(CultureInfo.InvariantCulture);

        if (http.GetEndpoint()?.Metadata.GetMetadata<PageActionDescriptor>() is not null)
        {
            http.Response.StatusCode = StatusCodes.Status303SeeOther;
            http.Response.Headers.Location = (http.Request.PathBase + new PathString(RefusalPage)).ToString();
            return;
        }

        http.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await http.Response.WriteAsJsonAsync(
            new { errors = new[] { new { code = BusinessErrorCodes.Identity.UserAccount.TooManyAttempts } } },
            cancellationToken);
    }

    private static RateLimitPartition<string> SendsOnly(HttpContext context, Func<string, RateLimitPartition<string>> limit)
        => IsSend(context.Request.Method)
            ? limit(CallerOf(context.Connection.RemoteIpAddress))
            : RateLimitPartition.GetNoLimiter("read");

    /// <summary>One permit per key a minute: a refusal is logged once, not a thousand times.</summary>
    internal static PartitionedRateLimiter<string> OncePerMinute()
        => PartitionedRateLimiter.Create<string, string>(key => RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 1,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));

    /// <summary>The wait the limiter asks for, in whole seconds and at least 1; a minute when it does not say.</summary>
    internal static int SecondsToWait(RateLimitLease lease)
    {
        var wait = lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter) ? retryAfter : TimeSpan.FromMinutes(1);
        return Math.Max(1, (int)Math.Ceiling(wait.TotalSeconds));
    }

    /// <summary>A burst at once, then a steady number back every minute.</summary>
    internal static RateLimitPartition<string> Bucket(string caller, int burst, int perMinute)
        => RateLimitPartition.GetTokenBucketLimiter(caller, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = burst,
            TokensPerPeriod = perMinute,
            ReplenishmentPeriod = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });

    /// <summary>
    /// A sliding window in one-minute steps: no double burst at a window's edge, as a fixed window allows. One-minute steps
    /// keep every wait at a minute or less, so "wait a minute" is always true; the window is at most an hour (settings).
    /// </summary>
    private static RateLimitPartition<string> Window(string caller, int permits, int minutes)
        => RateLimitPartition.GetSlidingWindowLimiter(caller, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = permits,
            Window = TimeSpan.FromMinutes(minutes),
            SegmentsPerWindow = minutes,
            QueueLimit = 0,
        });

    private static async ValueTask RefuseAsync(OnRejectedContext context, ILogger logger, PartitionedRateLimiter<string> warnings, CancellationToken cancellationToken)
    {
        var http = context.HttpContext;
        var seconds = SecondsToWait(context.Lease);
        var caller = CallerOf(http.Connection.RemoteIpAddress);

        // Keyed by the door's policy, not the path: a path can be spelled many ways, a policy name cannot.
        var door = http.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName ?? "unknown";
        using (var warning = warnings.AttemptAcquire($"{door}|{caller}"))
        {
            if (warning.IsAcquired)
            {
                logger.LogWarning("Too many tries at the {Door} door from {Caller}; refused for {Seconds} s. More refusals for this caller and door within a minute are not logged.", door, caller, seconds);
            }
        }

        await WriteRefusalAsync(http, seconds, cancellationToken);
    }
}
