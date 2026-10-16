namespace Harmony.Identity.Handler.Commands.TenantModule.State;

/// <summary>Nobody in the customer signs in until it is reinstated. A reason is required, for the console and the trail.</summary>
public class SuspendTenantCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public string Reason { get; set; } = null!;

    public class SuspendTenantCommandValidation : AbstractValidator<SuspendTenantCommand>
    {
        public SuspendTenantCommandValidation()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.IdRequired);

            this.RuleFor(e => e.Reason)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.ReasonRequired)
                .MaximumLength(500).WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.ReasonTooLong);
        }
    }
}
