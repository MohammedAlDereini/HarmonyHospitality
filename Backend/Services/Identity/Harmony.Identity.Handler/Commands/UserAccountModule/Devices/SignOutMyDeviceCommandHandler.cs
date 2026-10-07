namespace Harmony.Identity.Handler.Commands.UserAccountModule.Devices;

using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.AspNetCore.Identity;

/// <summary>The caller signs out of one of their own devices; the same rules as the admin's <see cref="SignOutDeviceCommandHandler"/>.</summary>
public class SignOutMyDeviceCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<SignOutMyDeviceCommand, CallResponse>
{
    private readonly IUserDeviceRepository devices;
    private readonly ICallContext callContext;

    public SignOutMyDeviceCommandHandler(UserManager<User> userManager, ICacheService cacheService, ISessionService sessionService, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, IUserDeviceRepository devices, ICallContext callContext)
        : base(userManager, cacheService, sessionService, auditEvents, securityEvents)
    {
        this.devices = devices;
        this.callContext = callContext;
    }

    public async Task<CallResponse> Handle(SignOutMyDeviceCommand command, CancellationToken cancellationToken)
    {
        var user = Guid.TryParse(this.callContext.UserId(), out var caller) ? await this.LoadAsync(caller) : null;
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
