namespace Harmony.Web.Bff.Settings;

/// <summary>
/// ApiDocs, optional: the one API-docs page of the platform (/docs) and the services it lists. No section, or no
/// services, means no page at all. UserName and PasswordPath are the credentials every service asks for before it
/// hands out its document (framework ApiDocs): both set, or both empty while the services still answer anonymously.
/// </summary>
public sealed class ApiDocsSettings
{
    public const string Section = "ApiDocs";

    public string? UserName { get; set; }

    public string? PasswordPath { get; set; }

    public ApiDocsService[] Services { get; set; } = [];

    public bool HasCredentials => !string.IsNullOrWhiteSpace(this.UserName);

    public static bool IsValid(ApiDocsSettings settings)
        => settings.Services.All(ApiDocsService.IsValid)
            && settings.Services.Select(service => service.Name.ToLowerInvariant()).Distinct().Count() == settings.Services.Length
            && (string.IsNullOrWhiteSpace(settings.UserName) == string.IsNullOrWhiteSpace(settings.PasswordPath))
            && (string.IsNullOrWhiteSpace(settings.PasswordPath) || File.Exists(settings.PasswordPath));
}

/// <summary>One service on the page: its name (a path segment, shown in the dropdown) and where its document lives.</summary>
public sealed class ApiDocsService
{
    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    /// <summary>A name is a short path segment: letters, digits and dashes, 2 to 32 long, starting with a letter.</summary>
    public static bool IsValid(ApiDocsService service)
        => System.Text.RegularExpressions.Regex.IsMatch(service.Name, "^[A-Za-z][A-Za-z0-9-]{1,31}$")
            && Uri.TryCreate(service.Url, UriKind.Absolute, out var url)
            && url.Scheme == Uri.UriSchemeHttps;
}
