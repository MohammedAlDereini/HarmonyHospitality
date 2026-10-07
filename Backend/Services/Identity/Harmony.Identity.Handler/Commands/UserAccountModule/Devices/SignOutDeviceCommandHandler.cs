namespace Harmony.Identity.Handler.Commands.UserAccountModule.Devices;

using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;

/// <summary>
/// An admin signs a person out of one device, like Mews's Remove in "Devices and activity": the session on that browser ends
/// (its refresh tokens too) and the device is forgotten, so signing in there again counts as a new device. The trail
/// records the admin.
/// </summary>
public class SignOutDeviceCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<SignOutDeviceCommand, CallResponse>
{
    private readonly IUserDeviceRepository devices;

    public SignOutDeviceCommandHandler(UserManager<User> userManager, ICacheService cacheService, ISessionService sessionService, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, IUserDeviceRepository devices)
        : base(userManager, cacheService, sessionService, auditEvents, securityEvents)
    {
        this.devices = devices;
    }

    public async Task<CallResponse> Handle(SignOutDeviceCommand command, CancellationToken cancellationToken)
    {
        // Tenant-filtered: an admin reaches only the accounts of their own tenant.
        var user = await this.LoadAsync(command.Id);
        if (user is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.NotFound);
        }

        var device = await this.devices.FindAsync(user.Id, command.DeviceId, cancellationToken);
        if (device is null)
        {
            return Fail(BusinessErrorCodes.Identity.UserAccount.DeviceNotFound);
        }

        if (device.SessionId is not null)
        {
            await this.SessionService.EndOneAsync(user.Id, device.SessionId, cancellationToken);
        }

        await this.devices.DeleteAsync(device, cancellationToken);
        await this.TrailAsync(user, SecurityAuditActions.SignedOutDevice, cancellationToken);
        await this.devices.SaveChangesAsync(cancellationToken);

        return Ok();
    }
}
