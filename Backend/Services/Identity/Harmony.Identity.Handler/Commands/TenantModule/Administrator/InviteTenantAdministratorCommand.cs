namespace Harmony.Identity.Handler.Commands.TenantModule.Administrator;

/// <summary>
/// Repeat or repair the birth of a tenant's administrator (platform only): a fresh invitation while the administrator has
/// not chosen a password yet; whatever is missing of the catalogue, the role or the account is made. The e-mail and the
/// name are needed only when no administrator account exists yet; for a plain re-send they stay empty.
/// </summary>
public class InviteTenantAdministratorCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public string? AdministratorEmail { get; set; }

    public string? AdministratorDisplayName { get; set; }

    public class InviteTenantAdministratorCommandValidation : AbstractValidator<InviteTenantAdministratorCommand>
    {
        public InviteTenantAdministratorCommandValidation()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.IdRequired);

            this.When(e => !string.IsNullOrWhiteSpace(e.AdministratorEmail), () =>
            {
                this.RuleFor(e => e.AdministratorEmail!)
                    .MaximumLength(256).WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.AdministratorEmailTooLong)
                    .Matches(@"^[^@\s]+@[^@\s]+\.[^@\s]+$").WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.AdministratorEmailInvalid);
            });

            this.When(e => !string.IsNullOrWhiteSpace(e.AdministratorDisplayName), () =>
            {
                this.RuleFor(e => e.AdministratorDisplayName!)
                    .MaximumLength(128).WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.AdministratorNameTooLong);
            });
        }
    }
}
