namespace Harmony.Identity.Domain.Models.DirectoryModule;

/// <summary>
/// The customer's directory connection, the way Mews' SCIM page shows it: the address to give the directory, the client
/// id, on or off, when the secret was made, and the groups the directory pushed with the role each one gives.
/// </summary>
public sealed class DirectoryModel
{
    public Guid Id { get; set; }

    public string ClientId { get; set; } = null!;

    public bool IsEnabled { get; set; }

    public DateTimeOffset SecretIssuedAtUtc { get; set; }

    /// <summary>The SCIM base address the directory calls (Entra's "Tenant URL").</summary>
    public string ScimUrl { get; set; } = null!;

    /// <summary>The token door for OAuth 2.0 client credentials (Entra's "Token endpoint").</summary>
    public string TokenUrl { get; set; } = null!;

    /// <summary>How many people the directory owns now.</summary>
    public int People { get; set; }

    public List<DirectoryGroupModel> Groups { get; set; } = [];
}

public sealed class DirectoryGroupModel
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = null!;

    public string? ExternalId { get; set; }

    /// <summary>The role the members hold; null: the group gives nothing.</summary>
    public Guid? RoleId { get; set; }

    public Guid? PropertyId { get; set; }

    public Guid? PropertyGroupId { get; set; }

    public int Members { get; set; }
}

/// <summary>
/// Shown once, when the connection opens or the secret is replaced, and never again: Harmony keeps only its hash. The
/// directory uses the client id and the secret at the token door, or the whole SecretToken as a long-lived bearer
/// token (Okta's header login, Entra's "Secret Token").
/// </summary>
public sealed class DirectorySecretModel
{
    public string ClientId { get; set; } = null!;

    public string Secret { get; set; } = null!;

    public string SecretToken { get; set; } = null!;

    public string ScimUrl { get; set; } = null!;

    public string TokenUrl { get; set; } = null!;
}
