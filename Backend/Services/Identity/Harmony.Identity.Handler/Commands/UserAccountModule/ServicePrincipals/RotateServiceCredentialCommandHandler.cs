namespace Harmony.Identity.Handler.Commands.UserAccountModule.ServicePrincipals;

using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Models.UserAccountModule;
using Microsoft.AspNetCore.Identity;

public class RotateServiceCredentialCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<RotateServiceCredentialCommand, CallResponse<ServicePrincipalCreatedModel>>
{
    public RotateServiceCredentialCommandHandler(UserManager<HarmonyUser> userManager)
        : base(userManager)
    {
    }

    public async Task<CallResponse<ServicePrincipalCreatedModel>> Handle(RotateServiceCredentialCommand command, CancellationToken cancellationToken)
    {
        var user = await this.LoadAsync(command.Id);
        if (user is null)
        {
            return Fail<ServicePrincipalCreatedModel>(IdentityErrorCodes.UserAccountNotFound);
        }

        var secret = user.RotateServiceCredential(DateTime.UtcNow);
        await this.SaveAsync(user);

        var model = new ServicePrincipalCreatedModel
        {
            Id = user.Id,
            Code = user.UserName!,
            Secret = secret,
            SecurityVersion = user.SecurityVersion,
        };

        return CallResponseBuilder.CreateResponse<ServicePrincipalCreatedModel>(eCallResponseStatus.Success).HasData(model);
    }
}
