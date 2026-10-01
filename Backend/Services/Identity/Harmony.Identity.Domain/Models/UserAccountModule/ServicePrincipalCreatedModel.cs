namespace Harmony.Identity.Domain.Models.UserAccountModule;

/// <summary>The one response that carries the secret. It is not stored and cannot be shown again.</summary>
public class ServicePrincipalCreatedModel
{
    public Guid Id { get; set; }
    public string Code { get; set; } = null!;
    public string Secret { get; set; } = null!;
    public int SecurityVersion { get; set; }
}
