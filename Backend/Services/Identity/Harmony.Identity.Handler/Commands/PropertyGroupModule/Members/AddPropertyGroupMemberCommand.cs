namespace Harmony.Identity.Handler.Commands.PropertyGroupModule.Members;

/// <summary>A hotel joins a group (platform only). FromUtc empty = now. One brand per hotel: a second brand is 00099.</summary>
public class AddPropertyGroupMemberCommand : BaseCommandRequest<CallResponse<Guid>>
{
    public Guid PropertyGroupId { get; set; }

    public Guid PropertyId { get; set; }

    /// <summary>From when; empty = now (UTC).</summary>
    public DateTimeOffset? FromUtc { get; set; }

    public class AddPropertyGroupMemberCommandValidation : AbstractValidator<AddPropertyGroupMemberCommand>
    {
        public AddPropertyGroupMemberCommandValidation()
        {
            this.RuleFor(e => e.PropertyGroupId)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.PropertyGroup.IdRequired);

            this.RuleFor(e => e.PropertyId)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.PropertyGroup.PropertyRequired);
        }
    }
}
