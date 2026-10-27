namespace Harmony.Identity.Domain.Entities.Aggregates.FederatedAuthorityModule;

/// <summary>How an authority proves who a person is. Stored as text, so a row reads plainly.</summary>
public enum AuthorityScheme
{
    /// <summary>Harmony's own sign-in: password, passkeys, two-step. Every customer has one.</summary>
    Local = 1,

    /// <summary>The company's Microsoft account (Entra ID), through Harmony's one Microsoft app.</summary>
    Microsoft = 2,

    /// <summary>The company's Google Workspace account, through Harmony's one Google app.</summary>
    Google = 3,
}
