using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

namespace Harmony.Identity.Domain.Services;

/// <summary>
/// Builds one row of the security trail from the current request: who did it (the signed-in caller, else the account
/// itself, else "system"), the IP address, the browser, the time. It never saves: the handler adds the row through its
/// repository, in the same save as the change it describes.
/// </summary>
public interface ISecurityEventFactory
{
    /// <param name="detail">One short fact the action needs (which passkey, which proof); null for most actions.</param>
    SecurityAuditEvent Create(User user, string action, string? detail = null);

    /// <summary>The same, with a named actor that is not a person (the company directory).</summary>
    SecurityAuditEvent CreateBy(User user, string action, string actor, string? detail = null);
}
