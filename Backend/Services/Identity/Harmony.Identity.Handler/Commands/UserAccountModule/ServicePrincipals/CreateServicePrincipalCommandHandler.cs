namespace Harmony.Identity.Handler.Commands.UserAccountModule.ServicePrincipals;

using Harmony.Identity.Domain.Common;
using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;
using Harmony.Identity.Domain.Models.UserAccountModule;
using Microsoft.AspNetCore.Identity;

public class CreateServicePrincipalCommandHandler : UserAccountCommandHandlerBase, IRequestHandler<CreateServicePrincipalCommand, CallResponse<ServicePrincipalCreatedModel>>
{
    public CreateServicePrincipalCommandHandler(UserManager<HarmonyUser> userManager)
        : base(userManager)
    {
    }

    public async Task<CallResponse<ServicePrincipalCreatedModel>> Handle(CreateServicePrincipalCommand command, CancellationToken cancellationToken)
    {
        var user = HarmonyUser.CreateServicePrincipal(command.Code, command.DisplayName, DateTime.UtcNow, out var secret);

        // Identity checks the name is free within the tenant and saves; a duplicate is the one refusal that is user input.
        var result = await this.UserManager.CreateAsync(user);
        if (result.Errors.Any(e => e.Code == nameof(IdentityErrorDescriber.DuplicateUserName)))
        {
            return Fail<ServicePrincipalCreatedModel>(IdentityErrorCodes.ServicePrincipalCodeInUse);
        }

        EnsureSucceeded(result);

        // The only time the secret exists in plain text. The row holds its digest.
        var model = new ServicePrincipalCreatedModel
        {
            Id = user.Id,
            Code = user.UserName!,
            Secret = secret,
            SecurityVersion = user.SecurityVersion,
        };

        return CallResponseBuilder.CreateResponse<ServicePrincipalCreatedModel>(eCallResponseStatus.Created).HasData(model);
    }
}
