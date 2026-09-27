using Harmony.Core.Exceptions;
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
            throw new BusinessException($"'{value}' is not a valid role code.", Error.New(IdentityErrorCodes.InvalidRoleCode));
        }

        return trimmed.ToUpperInvariant();
    }

    public static string PermissionCode(string? value)
    {
        var trimmed = Blank(value);

        if (trimmed is null || trimmed.Contains('*') || trimmed.Contains('%'))
        {
            throw new BusinessException($"'{value}' is not a valid permission code.", Error.New(IdentityErrorCodes.InvalidPermissionCode));
        }

        return trimmed;
    }

    public static bool SameCode(string? left, string? right)
        => string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
}