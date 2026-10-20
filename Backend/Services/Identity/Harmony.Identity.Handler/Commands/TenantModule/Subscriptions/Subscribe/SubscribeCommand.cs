namespace Harmony.Identity.Handler.Commands.TenantModule.Subscriptions.Subscribe;

using Harmony.Identity.Handler.Commands.ReferenceModule;

/// <summary>
/// A line of the customer's contract: born, or renewed and switched back on if it exists. A hotel line names the hotel
/// and may carry rooms and terminals; a chain-wide line (PropertyId empty) names no hotel and carries no counts.
/// Platform only.
/// </summary>
public class SubscribeCommand : BaseCommandRequest<CallResponse<Guid>>
{
    public Guid TenantId { get; set; }

    /// <summary>The hotel; empty = chain-wide (loyalty, multi-property).</summary>
    public Guid? PropertyId { get; set; }

    public string ModuleCode { get; set; } = null!;

    public int? Rooms { get; set; }

    public int? Terminals { get; set; }

    public DateOnly StartsOn { get; set; }

    /// <summary>End of the term; empty = open-ended. Required when the line does not renew.</summary>
    public DateOnly? EndsOn { get; set; }

    /// <summary>True unless the customer gave notice for this line.</summary>
    public bool RenewsAutomatically { get; set; } = true;

    public class SubscribeCommandValidation : AbstractValidator<SubscribeCommand>
    {
        public SubscribeCommandValidation()
        {
            this.RuleFor(e => e.TenantId)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.IdRequired);

            this.RuleFor(e => e.ModuleCode)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeRequired)
                .Matches(ReferenceRules.ModuleCodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeInvalidFormat);

            this.RuleFor(e => e.Rooms)
                .GreaterThanOrEqualTo(1).WithErrorCode(ModelValidationErrorCodes.Identity.Property.LineRoomsOutOfRange)
                .When(e => e.Rooms.HasValue);

            this.RuleFor(e => e.Terminals)
                .GreaterThanOrEqualTo(1).WithErrorCode(ModelValidationErrorCodes.Identity.Property.LineTerminalsOutOfRange)
                .When(e => e.Terminals.HasValue);

            // A chain-wide line covers the whole customer: nothing to count.
            this.RuleFor(e => e)
                .Must(e => e.PropertyId.HasValue || (e.Rooms is null && e.Terminals is null))
                .WithErrorCode(ModelValidationErrorCodes.Identity.Property.ChainWideLineHasCounts)
                .WithName(nameof(PropertyId));

            // The term: a real start, an end not before it, and an end whenever the line gives notice.
            this.RuleFor(e => e.StartsOn)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Property.TermInvalid);

            this.RuleFor(e => e)
                .Must(e => e.EndsOn is null || e.EndsOn >= e.StartsOn)
                .WithErrorCode(ModelValidationErrorCodes.Identity.Property.TermInvalid)
                .WithName(nameof(EndsOn));

            this.RuleFor(e => e)
                .Must(e => e.RenewsAutomatically || e.EndsOn is not null)
                .WithErrorCode(ModelValidationErrorCodes.Identity.Property.TermInvalid)
                .WithName(nameof(RenewsAutomatically));
        }
    }
}
