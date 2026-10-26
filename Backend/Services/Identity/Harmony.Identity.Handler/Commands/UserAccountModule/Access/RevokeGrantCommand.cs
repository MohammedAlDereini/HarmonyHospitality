namespace Harmony.Identity.Handler.Commands.UserAccountModule.Access;

/// <summary>Ends a grant now. The row stays, with its revocation.</summary>
public class RevokeGrantCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public Guid GrantId { get; set; }

    public class RevokeGrantCommandValidation : AbstractValidator<RevokeGrantCommand>
    {
        public RevokeGrantCommandValidation()
        {
            this.RuleFor(e => e.Id).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.IdRequired);
            this.RuleFor(e => e.GrantId).NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.UserAccount.GrantIdRequired);
        }
    }
}
