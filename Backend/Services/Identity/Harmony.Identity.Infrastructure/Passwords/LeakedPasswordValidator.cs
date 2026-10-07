using System.Security.Cryptography;
using System.Text;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Infrastructure.Passwords;

/// <summary>
/// Refuses a password that appears in a known data breach (NIST SP 800-63B-4 blocklist), with Have I Been Pwned's free
/// range API: only the first 5 characters of the password's SHA-1 leave the server, the answer is padded to hide its
/// size, and the comparison happens here. If the service cannot answer in time, the password is accepted and a warning
/// is logged: an outside service being down must not stop every password change (decided 2026-10-07).
/// </summary>
public sealed class LeakedPasswordValidator : IPasswordValidator<User>
{
    public const string ClientName = "pwned-passwords";

    private readonly IHttpClientFactory clients;
    private readonly LeakedPasswordSettings settings;
    private readonly ILogger<LeakedPasswordValidator> logger;

    public LeakedPasswordValidator(IHttpClientFactory clients, IOptions<LeakedPasswordSettings> settings, ILogger<LeakedPasswordValidator> logger)
    {
        this.clients = clients;
        this.settings = settings.Value;
        this.logger = logger;
    }

    public async Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user, string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return IdentityResult.Success;
        }

        // SHA-1 because that is what the range API indexes; it is a lookup key here, not a stored password hash.
        var hash = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(password)));

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(this.settings.TimeoutSeconds));
            using var request = new HttpRequestMessage(HttpMethod.Get, this.settings.RangeUrl + hash[..5]);
            request.Headers.Add("Add-Padding", "true");

            using var response = await this.clients.CreateClient(ClientName).SendAsync(request, timeout.Token);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync(timeout.Token);

            return IsListed(body, hash[5..])
                ? IdentityResult.Failed(new IdentityError
                {
                    Code = PasswordRules.LeakedErrorCode,
                    Description = "The password appears in a known data breach.",
                })
                : IdentityResult.Success;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            this.logger.LogWarning("Leaked-password check unavailable ({Reason}): the password was accepted without it.", exception.GetType().Name);
            return IdentityResult.Success;
        }
    }

    // One "SUFFIX:COUNT" per line; the padding lines carry a count of 0.
    private static bool IsListed(string body, string suffix)
    {
        foreach (var line in body.Split('\n'))
        {
            var colon = line.IndexOf(':');
            if (colon == suffix.Length
                && line.AsSpan(0, colon).Equals(suffix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(line.AsSpan(colon + 1).Trim(), out var count)
                && count > 0)
            {
                return true;
            }
        }

        return false;
    }
}
