namespace Harmony.Identity.Domain.Models.CompanyLoginModule;

/// <summary>How one person signs in: the administrator's choice, the company login that covers them, and their company links.</summary>
public class AccountSignInModel
{
    /// <summary>The administrator kept this person on a Harmony password.</summary>
    public bool PasswordSignIn { get; set; }

    /// <summary>The switched-on company login that covers the person's e-mail (Microsoft or Google), or null.</summary>
    public string? CoveredBy { get; set; }

    /// <summary>What the person uses now: Company or Password.</summary>
    public string SignsInWith { get; set; } = null!;

    public List<CompanyLinkModel> Links { get; set; } = [];
}

/// <summary>A company identity bound to the account, and since when (or from when to when).</summary>
public class CompanyLinkModel
{
    public Guid Id { get; set; }

    /// <summary>Microsoft or Google.</summary>
    public string Provider { get; set; } = null!;

    /// <summary>"first-sign-in", or the administrator's account id.</summary>
    public string LinkedBy { get; set; } = null!;

    public bool IsOpen { get; set; }

    /// <summary>When the latest period began.</summary>
    public DateTimeOffset Since { get; set; }

    /// <summary>When the latest period ended; null while it is open.</summary>
    public DateTimeOffset? Until { get; set; }
}
