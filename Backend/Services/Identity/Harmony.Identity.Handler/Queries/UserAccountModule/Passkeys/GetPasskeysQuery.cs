using Harmony.Identity.Domain.Models.UserAccountModule;

namespace Harmony.Identity.Handler.Queries.UserAccountModule.Passkeys;

/// <summary>The passkeys an account holds. Built by the controller, never bound from a request: no Id means the caller.</summary>
public class GetPasskeysQuery : BaseQueryRequest<CallResponse<List<UserPasskeyModel>>>
{
    public Guid? Id { get; init; }
}
