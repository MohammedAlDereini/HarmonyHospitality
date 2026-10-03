using Harmony.Core.Exceptions;
using Harmony.Core.Errors;
using Harmony.Core.Models;

namespace Harmony.Identity.Domain.Common;

public static class IdentityText
{
    public static string? Blank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static string RoleCode(string? value)
    {
        var trimmed = Blank(value);

        if (trimmed is null || trimmed.Length < 2 || trimmed.Length > 32
            || !trimmed.All(c => char.IsAsciiLetterOrDigit(c) || c == '-'))
        {
            throw new BusinessException($"'{value}' is not a valid role code.", Error.New(BusinessErrorCodes.Identity.Role.InvalidCode));
        }

        return trimmed.ToUpperInvariant();
    }

    public static string DisplayName(string? value)
    {
        var trimmed = Blank(value);

        if (trimmed is null || trimmed.Length > 128)
        {
            throw new BusinessException("A display name of 1 to 128 characters is required.", Error.New(BusinessErrorCodes.Identity.UserAccount.DisplayNameRequired));
        }

        return trimmed;
    }

    public static string? Reason(string? value)
    {
        var trimmed = Blank(value);
        return trimmed is { Length: > 500 } ? trimmed[..500] : trimmed;
    }

    public static bool SameCode(string? left, string? right)
        => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
}