namespace Harmony.Identity.Handler.Commands.LookupModule.Values.Create;

public class CreateLookupValueCommand : BaseCommandRequest<CallResponse>
{
    public Guid LookupCategoryId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsGlobal { get; set; }

    public class CreateLookupValueCommandValidation : AbstractValidator<CreateLookupValueCommand>
    {
        public CreateLookupValueCommandValidation()
        {
            this.RuleFor(e => e.LookupCategoryId)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Lookup.ValueCategoryIdRequired);

            this.RuleFor(e => e.Name)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Lookup.ValueNameRequired)
                .MaximumLength(200).WithErrorCode(ModelValidationErrorCodes.Identity.Lookup.ValueNameTooLong);

            this.RuleFor(e => e.Description)
                .MaximumLength(500).WithErrorCode(ModelValidationErrorCodes.Identity.Lookup.ValueDescriptionTooLong)
                .When(e => !string.IsNullOrWhiteSpace(e.Description));
        }
    }
}