namespace Harmony.Identity.Domain.Entities.Reference;

/// <summary>
/// Checks for seeded values. Reference data is installed by code, not typed by users,
/// so a bad value is a programming error and throws ArgumentException.
/// </summary>
internal static class ReferenceText
{
    public static string Letters(string? value, int length, bool upper, string paramName)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length != length || !trimmed.All(char.IsAsciiLetter))
        {
            throw new ArgumentException($"'{value}' must be exactly {length} ASCII letters.", paramName);
        }

        return upper ? trimmed.ToUpperInvariant() : trimmed.ToLowerInvariant();
    }

    public static string Key(string? value, int maxLength, string paramName)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length < 2 || trimmed.Length > maxLength
            || !trimmed.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_' or '/'))
        {
            throw new ArgumentException($"'{value}' is not a valid code (2-{maxLength} characters: letters, digits, . - _ /).", paramName);
        }

        return trimmed;
    }

    public static string Name(string? value, int maxLength, string paramName)
    {
        var trimmed = value?.Trim() ?? string.Empty;

        if (trimmed.Length == 0 || trimmed.Length > maxLength)
        {
            throw new ArgumentException($"A name of 1-{maxLength} characters is required.", paramName);
        }

        return trimmed;
    }
}