namespace Harmony.Identity.Handler.Commands.UserAccountModule.Access;

using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

/// <summary>A temporary elevation: a role at one hotel or group between two moments, for a reason. FromUtc empty = now.</summary>
public class GrantAccessCommand : BaseCommandRequest<CallResponse<Guid>>
{
    public Guid Id { get; set; }

    public Guid RoleId { get; set; }

    public Guid? PropertyId { get; set; }

    public Guid? PropertyGroupId { get; set; }

    public DateTimeOffset? FromUtc { get; set; }

    public DateTimeOffset ToUtc { get; set; }

    public string Reason { get; set; } = null!;

    public class GrantAccessCommandValidation : AbstractValidator<GrantAccessCommand>
    {
        public GrantAccessCommandValidation()
        {
            this.RuleFor(e => e.Id).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.IdRequired);
            this.RuleFor(e => e.RoleId).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.RoleIdsRequired);
            this.RuleFor(e => e)
                .Must(e => (e.PropertyId is { } p && p != Guid.Empty) != (e.PropertyGroupId is { } g && g != Guid.Empty))
                .WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.ScopeRequired)
                .WithName(nameof(PropertyId));
            this.RuleFor(e => e)
                .Must(e => e.ToUtc > (e.FromUtc ?? DateTimeOffset.UtcNow) && e.ToUtc > DateTimeOffset.UtcNow)
                .WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.GrantWindowInvalid)
                .WithName(nameof(ToUtc));
            this.RuleFor(e => e)
                .Must(e => e.ToUtc - (e.FromUtc ?? DateTimeOffset.UtcNow) <= AccessGrant.LongestWindow)
                .WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.GrantTooLong)
                .WithName(nameof(ToUtc));
            this.RuleFor(e => e.Reason)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.GrantReasonRequired)
                .MaximumLength(AccessGrant.ReasonMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.GrantReasonTooLong);
        }
    }
}
