using Harmony.Identity.Domain.Models.UserAccountModule;

namespace Harmony.Identity.Handler.Queries.UserAccountModule.Devices;

/// <summary>The browsers an account signed in from. Built by the controller, never bound from a request: no Id means the caller.</summary>
public class GetDevicesQuery : BaseQueryRequest<CallResponse<List<UserDeviceModel>>>
{
    public Guid? Id { get; init; }
}
