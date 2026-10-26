namespace Harmony.Identity.Handler.Commands.UserAccountModule.Access;

/// <summary>A role at one hotel or one group, open-ended or until a moment. Exactly one of the two scopes.</summary>
public class AssignRoleCommand : BaseCommandRequest<CallResponse<Guid>>
{
    public Guid Id { get; set; }

    public Guid RoleId { get; set; }

    public Guid? PropertyId { get; set; }

    public Guid? PropertyGroupId { get; set; }

    public DateTimeOffset? EndsAtUtc { get; set; }

    public class AssignRoleCommandValidation : AbstractValidator<AssignRoleCommand>
    {
        public AssignRoleCommandValidation()
        {
            this.RuleFor(e => e.Id).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.IdRequired);
            this.RuleFor(e => e.RoleId).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.RoleIdsRequired);
            this.RuleFor(e => e)
                .Must(e => (e.PropertyId is { } p && p != Guid.Empty) != (e.PropertyGroupId is { } g && g != Guid.Empty))
                .WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.ScopeRequired)
                .WithName(nameof(PropertyId));
            this.RuleFor(e => e.EndsAtUtc)
                .Must(ends => ends > DateTimeOffset.UtcNow)
                .WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.EndsAtInvalid)
                .When(e => e.EndsAtUtc.HasValue);
        }
    }
}
