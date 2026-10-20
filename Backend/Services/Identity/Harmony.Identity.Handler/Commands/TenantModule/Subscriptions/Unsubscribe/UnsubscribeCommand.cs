namespace Harmony.Identity.Handler.Commands.TenantModule.Subscriptions.Unsubscribe;

using Harmony.Identity.Handler.Commands.ReferenceModule;

/// <summary>A line switched off with a reason, today; the row stays and a later Subscribe switches it back on. Platform only.</summary>
public class UnsubscribeCommand : BaseCommandRequest<CallResponse>
{
    public Guid TenantId { get; set; }

    /// <summary>The hotel; empty = the chain-wide line.</summary>
    public Guid? PropertyId { get; set; }

    public string ModuleCode { get; set; } = null!;

    public string Reason { get; set; } = null!;

    public class UnsubscribeCommandValidation : AbstractValidator<UnsubscribeCommand>
    {
        public UnsubscribeCommandValidation()
        {
            this.RuleFor(e => e.TenantId)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.IdRequired);

            this.RuleFor(e => e.ModuleCode)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeRequired)
                .Matches(ReferenceRules.ModuleCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeInvalidFormat);

            this.RuleFor(e => e.Reason)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.ReasonRequired)
                .MaximumLength(500).WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.ReasonTooLong);
        }
    }
}
