namespace Harmony.Identity.Handler.Commands.UserAccountModule.SecurityTrail;

using Harmony.Identity.Domain.Entities.Aggregates.SecurityAuditModule;
using Harmony.Identity.Domain.Entities.Aggregates.SessionModule;

using Harmony.Core.Exceptions;
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
    private readonly IOperatorSessionRepository sessions;
    private readonly ILogger<RecordSignInCommandHandler> logger;

    public RecordSignInCommandHandler(IUserAccountRepository userAccounts, IUserDeviceRepository devices, ISecurityAuditEventRepository auditEvents, ISecurityEventFactory securityEvents, IAccountMailer mailer, IOperatorSessionRepository sessions, ILogger<RecordSignInCommandHandler> logger)
    {
        this.sessions = sessions;
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

        var row = this.securityEvents.Create(user, command.TrailAction, command.Detail);
        await this.auditEvents.AddAsync(row, cancellationToken);

        // The sign-in's own record (design 3.4), once per Duende session: a sign-in that reached us twice opens one row.
        if (!string.IsNullOrWhiteSpace(command.SessionId) && await this.sessions.OpenByKeyAsync(command.SessionId, cancellationToken) is null)
        {
            var (methods, provider) = HowOf(command.TrailAction, command.Detail);
            await this.sessions.AddAsync(OperatorSession.Start(user.TenantId, user.Id, command.SessionId, methods, provider, row.OccurredAtUtc), cancellationToken);
        }

        var label = row.Device ?? UnknownDevice;
        var hash = UserDevice.HashOf(command.DeviceKey);
        var device = await this.devices.FindByKeyAsync(user.Id, hash, cancellationToken);
        var fresh = device is null ? UserDevice.First(user.TenantId, user.Id, hash, label, row.SourceAddress, command.SessionId, row.OccurredAtUtc) : null;
        if (fresh is not null)
        {
            await this.devices.AddAsync(fresh, cancellationToken);
        }
        else
        {
            device!.SeenAgain(label, row.SourceAddress, command.SessionId, row.OccurredAtUtc);
        }

        try
        {
            await this.devices.SaveChangesAsync(cancellationToken);
        }
        catch (BusinessException exception) when (fresh is not null && exception.Errors.Any(e => e.Code == "Conflict"))
        {
            // The same sign-in reached us twice and the other copy saved this device first (the unique index said no).
            // The device exists now, which is all this copy wanted: let go of our unsaved row, mark the saved one seen
            // again, and keep this copy's trail row. One device, one e-mail, no error for the person.
            this.devices.Forget(fresh);
            device = await this.devices.FindByKeyAsync(user.Id, hash, cancellationToken)
                ?? throw new InvalidOperationException("The device vanished right after its duplicate was refused.");
            device.SeenAgain(label, row.SourceAddress, command.SessionId, row.OccurredAtUtc);
            await this.devices.SaveChangesAsync(cancellationToken);
        }

        if (fresh is not null && device is null)
        {
            this.logger.LogInformation("Account {UserId} signed in from a new device ({Device}); the person is told.", user.Id, label);
            await this.mailer.SendNewDeviceAlertAsync(user, label, row.SourceAddress, row.OccurredAtUtc, cancellationToken);
        }

        return CallResponseBuilder.CreateResponse(eCallResponseStatus.Success);
    }

    /// <summary>How the sign-in was made, as the token's amr says it, from the trail action the page recorded.</summary>
    private static (string[] Methods, string Provider) HowOf(string trailAction, string? detail) => trailAction switch
    {
        SecurityAuditActions.SignInPasskey => (["swk", "mfa"], "local"),
        SecurityAuditActions.SignInPasswordOnly => (["pwd"], "local"),
        SecurityAuditActions.SignInCompany => (["external"], (detail ?? "company").ToLowerInvariant()),
        _ => (["mfa"], "local"),
    };
}
