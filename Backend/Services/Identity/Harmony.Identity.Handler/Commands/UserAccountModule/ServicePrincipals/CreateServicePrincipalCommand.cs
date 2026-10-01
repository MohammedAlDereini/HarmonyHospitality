namespace Harmony.Identity.Handler.Commands.UserAccountModule.ServicePrincipals;

using Harmony.Identity.Domain.Models.UserAccountModule;

public class CreateServicePrincipalCommand : BaseCommandRequest<CallResponse<ServicePrincipalCreatedModel>>
{
    public string Code { get; set; } = null!;

    public string DisplayName { get; set; } = null!;

    public class CreateServicePrincipalCommandValidation : AbstractValidator<CreateServicePrincipalCommand>
    {
        public CreateServicePrincipalCommandValidation()
        {
            this.RuleFor(e => e.Code)
                .NotEmpty().WithMessage("This field is required.")
                .Length(3, 64).WithMessage("Code must be 3 to 64 characters.");

            this.RuleFor(e => e.DisplayName)
                .NotEmpty().WithMessage("This field is required.")
                .MaximumLength(128).WithMessage("Display name must not exceed 128 characters.");
        }
    }
}
