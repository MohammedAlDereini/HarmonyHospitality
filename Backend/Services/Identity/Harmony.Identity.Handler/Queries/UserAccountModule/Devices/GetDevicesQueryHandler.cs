using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Models.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Harmony.Identity.Domain.Services;

namespace Harmony.Identity.Handler.Queries.UserAccountModule.Devices;

public class GetDevicesQueryHandler : IRequestHandler<GetDevicesQuery, CallResponse<List<UserDeviceModel>>>
{
    private readonly IUserAccountRepository userAccounts;
    private readonly IUserDeviceRepository devices;
    private readonly ISessionService sessions;
    private readonly ICallContext callContext;

    public GetDevicesQueryHandler(IUserAccountRepository userAccounts, IUserDeviceRepository devices, ISessionService sessions, ICallContext callContext)
    {
        this.userAccounts = userAccounts;
        this.devices = devices;
        this.sessions = sessions;
        this.callContext = callContext;
    }

    public async Task<CallResponse<List<UserDeviceModel>>> Handle(GetDevicesQuery request, CancellationToken cancellationToken)
    {
        var id = request.Id ?? (Guid.TryParse(this.callContext.UserId(), out var caller) ? caller : Guid.Empty);
        if (id == Guid.Empty)
        {
            return NotFound();
        }

        // One trip to the database: the account and its devices, newest first. The tenant filter on the account is the wall:
        // an account of another tenant is not found, so neither are its devices.
        var deviceRows = this.devices.Query();
        var found = await this.userAccounts.Query()
            .Where(a => a.Id == id)
            .Select(a => new { Devices = deviceRows.Where(d => d.UserAccountId == a.Id).OrderByDescending(d => d.LastSeenUtc).ToList() })
            .SingleOrDefaultAsync(cancellationToken);

        if (found is null)
        {
            return NotFound();
        }

        var open = await this.sessions.OpenSessionIdsAsync(id, cancellationToken);

        var list = found.Devices.Select(d => new UserDeviceModel
        {
            Id = d.Id,
            Label = d.Label,
            FirstSeenUtc = d.FirstSeenUtc,
            LastSeenUtc = d.LastSeenUtc,
            LastAddress = d.LastAddress,
            SignedIn = d.SessionId is not null && open.Contains(d.SessionId),
        }).ToList();

        return CallResponseBuilder.CreateResponse<List<UserDeviceModel>>(eCallResponseStatus.Success).HasData(list);
    }

    private static CallResponse<List<UserDeviceModel>> NotFound()
        => CallResponseBuilder.CreateResponse<List<UserDeviceModel>>(eCallResponseStatus.BusinessValidation)
            .HasErrors(Error.New(BusinessErrorCodes.Identity.UserAccount.NotFound));
}
