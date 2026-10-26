using Harmony.Core.Enums;
using Harmony.Core.Identity.Permissions;
using Harmony.Identity.Handler.Queries.UserAccountModule.Access;
using Harmony.MediatR;

namespace Harmony.Identity.Api.Access;

/// <summary>The framework asks here; the answer is MayActAtQueryHandler's. Registered scoped in Program.cs.</summary>
public sealed class PropertyAccessChecker(IMediator mediator) : IPropertyAccessChecker
{
    public async Task<bool> MayActAtAsync(Guid userId, Guid tenantId, Guid propertyId, CancellationToken cancellationToken)
    {
        var answer = await mediator.Send(new MayActAtQuery { UserId = userId, TenantId = tenantId, PropertyId = propertyId }, cancellationToken);
        return answer.Status == eCallResponseStatus.Success && answer.Data;
    }
}
