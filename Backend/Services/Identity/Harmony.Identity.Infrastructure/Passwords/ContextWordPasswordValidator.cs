using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Microsoft.AspNetCore.Identity;

namespace Harmony.Identity.Infrastructure.Passwords;

/// <summary>
/// Refuses a password that contains the service name, or the person's own name or e-mail name (NIST SP 800-63B-4
/// "context-specific words"; Microsoft Entra Password Protection does the same). Both sides are compared the way
/// Microsoft does it: lower case, with the common swaps 0→o, 1→l, $→s, @→a undone, so "H@rm0ny" is still "harmony".
/// Names count from 4 characters, like Microsoft's, so a short name does not block ordinary words.
/// </summary>
public sealed class ContextWordPasswordValidator : IPasswordValidator<User>
{
    public const int MinimumWordLength = 4;

    private static readonly char[] EmailSeparators = ['.', '_', '-', '+'];

    public Task<IdentityResult> ValidateAsync(UserManager<User> manager, User user, string? password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return Task.FromResult(IdentityResult.Success);
        }

        var normalized = Normalize(password);
        var found = ContextWords(user).Any(word => normalized.Contains(word, StringComparison.Ordinal));

        return Task.FromResult(found
            ? IdentityResult.Failed(new IdentityError
            {
                Code = PasswordRules.ContextWordErrorCode,
                Description = "The password contains the service name, or the person's own name or e-mail name.",
            })
            : IdentityResult.Success);
    }

    // The service name, the e-mail's local part and its pieces, and each word of the display name.
    private static IEnumerable<string> ContextWords(User user)
    {
        var local = (user.Email ?? user.UserName ?? string.Empty).Split('@')[0];
        var names = new[] { local }
            .Concat(local.Split(EmailSeparators, StringSplitOptions.RemoveEmptyEntries))
            .Concat((user.DisplayName ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Where(name => name.Length >= MinimumWordLength);

        return PasswordRules.ServiceWords.Concat(names).Select(Normalize).Distinct();
    }

    // Lower case, then Microsoft's substitutions undone.
    private static string Normalize(string value)
    {
        var chars = value.ToLowerInvariant().ToCharArray();
        for (var i = 0; i < chars.Length; i++)
        {
            chars[i] = chars[i] switch
            {
                '0' => 'o',
                '1' => 'l',
                '$' => 's',
                '@' => 'a',
                _ => chars[i],
            };
        }

        return new string(chars);
    }
}
