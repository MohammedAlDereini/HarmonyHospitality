namespace Harmony.Identity.Handler.Commands.UserAccountModule.Create;

using Harmony.Identity.Domain.Entities.Aggregates.UserAccountModule;

/// <summary>An admin creates an account. The initial password works once: the person must replace it at first login.</summary>
public class CreateUserAccountCommand : BaseCommandRequest<CallResponse<Guid>>
{
    public string DisplayName { get; set; } = null!;

    /// <summary>Also the user name. Unique inside the tenant.</summary>
    public string Email { get; set; } = null!;

    /// <summary>Ids from POST api/Role/get-all. At least one: an account with no role can do nothing.</summary>
    public List<Guid> RoleIds { get; set; } = [];

    /// <summary>
    /// Optional. Empty means an invitation: the person gets a mail with a link and chooses the first password, nobody else
    /// ever knows it. When given, the account is born with MustChangePassword and this password works exactly once.
    /// </summary>
    public string? InitialPassword { get; set; }

    public class CreateUserAccountCommandValidation : AbstractValidator<CreateUserAccountCommand>
    {
        public CreateUserAccountCommandValidation()
        {
            this.RuleFor(e => e.DisplayName)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.DisplayNameRequired)
                .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.DisplayNameTooLong);

            this.RuleFor(e => e.Email)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.EmailRequired)
                .MaximumLength(256).WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.EmailTooLong)
                .Matches(@"^[^@\s]+@[^@\s]+\.[^@\s]+$").WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.EmailInvalid);

            this.RuleFor(e => e.RoleIds)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.AtLeastOneRoleRequired);

            this.RuleForEach(e => e.RoleIds)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.RoleIdEmpty);

            this.RuleFor(e => e.InitialPassword)
                .MinimumLength(PasswordRules.MinimumLength).WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.InitialPasswordTooShort)
                .When(e => !string.IsNullOrEmpty(e.InitialPassword));
        }
    }
}
