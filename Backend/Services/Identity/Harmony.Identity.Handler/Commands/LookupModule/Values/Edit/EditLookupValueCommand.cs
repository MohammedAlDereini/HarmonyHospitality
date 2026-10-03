namespace Harmony.Identity.Handler.Commands.LookupModule.Values.Edit;

public class EditLookupValueCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public class EditLookupValueCommandValidation : AbstractValidator<EditLookupValueCommand>
    {
        public EditLookupValueCommandValidation()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Lookup.ValueIdRequired);

            this.RuleFor(e => e.Name)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Lookup.ValueNameRequired)
                .MaximumLength(200).WithErrorCode(ModelValidationErrorCodes.Identity.Lookup.ValueNameTooLong);

            this.RuleFor(e => e.Description)
                .MaximumLength(500).WithErrorCode(ModelValidationErrorCodes.Identity.Lookup.ValueDescriptionTooLong)
                .When(e => !string.IsNullOrWhiteSpace(e.Description));
        }
    }
}