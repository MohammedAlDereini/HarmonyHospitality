using Harmony.Core.Abstractions;
using Harmony.Identity.Domain.Models.UserAccountModule;
using Harmony.Identity.Domain.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Harmony.Identity.Handler.Queries.UserAccountModule.Passkeys;

public class GetPasskeysQueryHandler : IRequestHandler<GetPasskeysQuery, CallResponse<List<UserPasskeyModel>>>
{
    private readonly IUserAccountRepository userAccounts;
    private readonly IUserPasskeyRepository passkeys;
    private readonly ICallContext callContext;
    private readonly IOptions<IdentityPasskeyOptions> passkeyOptions;

    public GetPasskeysQueryHandler(IUserAccountRepository userAccounts, IUserPasskeyRepository passkeys, ICallContext callContext, IOptions<IdentityPasskeyOptions> passkeyOptions)
    {
        this.userAccounts = userAccounts;
        this.passkeys = passkeys;
        this.callContext = callContext;
        this.passkeyOptions = passkeyOptions;
    }

    public async Task<CallResponse<List<UserPasskeyModel>>> Handle(GetPasskeysQuery request, CancellationToken cancellationToken)
    {
        var id = request.Id ?? (Guid.TryParse(this.callContext.UserId(), out var caller) ? caller : Guid.Empty);
        if (id == Guid.Empty)
        {
            return NotFound();
        }

        // One trip: the account and its passkeys, newest first. The tenant filter on the account is the wall.
        var passkeyRows = this.passkeys.Query();
        var found = await this.userAccounts.Query()
            .Where(a => a.Id == id)
            .Select(a => new { Passkeys = passkeyRows.Where(p => p.UserAccountId == a.Id).OrderByDescending(p => p.CreatedUtc).ToList() })
            .SingleOrDefaultAsync(cancellationToken);

        if (found is null)
        {
            return NotFound();
        }

        return CallResponseBuilder.CreateResponse<List<UserPasskeyModel>>(eCallResponseStatus.Success)
            .HasData(found.Passkeys.Select(row => PasskeyModel.From(row, this.passkeyOptions.Value.ServerDomain!)).ToList());
    }

    private static CallResponse<List<UserPasskeyModel>> NotFound()
        => CallResponseBuilder.CreateResponse<List<UserPasskeyModel>>(eCallResponseStatus.BusinessValidation)
            .HasErrors(Error.New(BusinessErrorCodes.Identity.UserAccount.NotFound));
}
