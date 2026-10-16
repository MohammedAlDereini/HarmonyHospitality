namespace Harmony.Identity.Handler.Commands.TenantModule.State;

/// <summary>The customer is back: its people sign in again. A terminated tenant never comes back (00072).</summary>
public class ReinstateTenantCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public class ReinstateTenantCommandValidation : AbstractValidator<ReinstateTenantCommand>
    {
        public ReinstateTenantCommandValidation()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.IdRequired);
        }
    }
}
