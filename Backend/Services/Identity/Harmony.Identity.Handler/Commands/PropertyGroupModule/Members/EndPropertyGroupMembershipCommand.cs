namespace Harmony.Identity.Handler.Commands.PropertyGroupModule.Members;

/// <summary>Closes a hotel's membership (platform only). ToUtc empty = now. A membership ends once (00102).</summary>
public class EndPropertyGroupMembershipCommand : BaseCommandRequest<CallResponse>
{
    public Guid MemberId { get; set; }

    /// <summary>Until when; empty = now (UTC). Never before it began (00104).</summary>
    public DateTimeOffset? ToUtc { get; set; }

    public class EndPropertyGroupMembershipCommandValidation : AbstractValidator<EndPropertyGroupMembershipCommand>
    {
        public EndPropertyGroupMembershipCommandValidation()
        {
            this.RuleFor(e => e.MemberId)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.PropertyGroup.MemberIdRequired);
        }
    }
}
