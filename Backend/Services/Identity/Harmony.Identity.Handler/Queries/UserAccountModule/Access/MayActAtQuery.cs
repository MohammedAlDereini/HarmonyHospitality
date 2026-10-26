namespace Harmony.Identity.Handler.Queries.UserAccountModule.Access;

/// <summary>May this person act at this hotel, right now? Asked by the framework's PropertyAccessHandler through the checker.</summary>
public class MayActAtQuery : BaseQueryRequest<CallResponse<bool>>
{
    public Guid UserId { get; set; }

    public Guid TenantId { get; set; }

    public Guid PropertyId { get; set; }
}
