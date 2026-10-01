namespace Harmony.Identity.Handler.Commands.UserAccountModule.ServicePrincipals;

using Harmony.Identity.Domain.Models.UserAccountModule;

public class RotateServiceCredentialCommand : BaseCommandRequest<CallResponse<ServicePrincipalCreatedModel>>
{
    public Guid Id { get; set; }

    public class RotateServiceCredentialCommandValidation : AbstractValidator<RotateServiceCredentialCommand>
    {
        public RotateServiceCredentialCommandValidation()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithMessage("This field is required.");
        }
    }
}
