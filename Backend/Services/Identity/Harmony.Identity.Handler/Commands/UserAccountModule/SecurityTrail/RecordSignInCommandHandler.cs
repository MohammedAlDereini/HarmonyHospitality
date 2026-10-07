namespace Harmony.Identity.Handler.Commands.UserAccountModule.SecurityTrail;

using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;
using Microsoft.Extensions.Logging;

/// <summary>
/// A sign-in on the pages (4b, 4c): the sign-in goes in the trail, the browser is remembered with its new session (or seen
/// again), and a browser the account does not know earns the person an e-mail, every time, like Mews's "Login from new
/// device" (a forgotten device counts as new again). One save for the row and the device.
/// </summary>
public class RecordSignInCommandHandler : IRequestHandler<RecordSignInCommand, CallResponse>
{
    /// <summary>The device key the pages make: 32 random bytes, base64url, no padding.</summary>
    public const int DeviceKeyLength = 43;

    private const string UnknownDevice = "an unknown browser on an unknown system";

    private readonly IUserAccountRepository userAccounts;
    private readonly IUserDeviceRepository devices;
    private readonly ISecurityAuditEventRepository auditEvents;
    private readonly ISecurityEventFactory securityEvents;
    private readonly IAccountMailer mailer;
    private readonly ILogger<RecordSignInCommandHandler> logger;

    public RecordSignInCommandHandler(IUserAccountRepository userAccounts, IUserDeviceRepository devices, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, IAccountMailer mailer, ILogger<RecordSignInCommandHandler> logger)
    {
        this.userAccounts = userAccounts;
        this.devices = devices;
        this.auditEvents = auditEvents;
        this.securityEvents = securityEvents;
        this.mailer = mailer;
        this.logger = logger;
    }

    public async Task<CallResponse> Handle(RecordSignInCommand command, CancellationToken cancellationToken)
    {
        // Our own pages send this right after a sign-in, so a bad key or an unknown account is a bug, not input.
        if (command.DeviceKey is not { Length: DeviceKeyLength })
        {
            throw new ArgumentException("A 43-character device key is required.", nameof(command));
        }

        var user = await this.userAccounts.FindByIdAcrossTenantsAsync(command.UserId, cancellationToken)
            ?? throw new InvalidOperationException($"Account {command.UserId} signed in but cannot be found.");

        var row = this.securityEvents.Create(user, command.TrailAction);
        await this.auditEvents.AddAsync(row, cancellationToken);

        var label = row.Device ?? UnknownDevice;
        var hash = UserDevice.HashOf(command.DeviceKey);
        var device = await this.devices.FindByKeyAsync(user.Id, hash, cancellationToken);
        if (device is null)
        {
            await this.devices.AddAsync(UserDevice.First(user.TenantId, user.Id, hash, label, row.SourceAddress, command.SessionId, row.OccurredAtUtc), cancellationToken);
        }
        else
        {
            device.SeenAgain(label, row.SourceAddress, command.SessionId, row.OccurredAtUtc);
        }

        await this.devices.SaveChangesAsync(cancellationToken);

        if (device is null)
        {
            this.logger.LogInformation("Account {UserId} signed in from a new device ({Device}); the person is told.", user.Id, label);
            await this.mailer.SendNewDeviceAlertAsync(user, label, row.SourceAddress, row.OccurredAtUtc, cancellationToken);
        }

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }
}
