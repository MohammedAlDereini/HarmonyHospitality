namespace Harmony.Identity.Handler.Commands.LookupModule.Categories.Create;

public class CreateLookupCategoryCommand : BaseCommandRequest<CallResponse>
{
    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsGlobal { get; set; }

    public class CreateLookupCategoryCommandValidation : AbstractValidator<CreateLookupCategoryCommand>
    {
        public CreateLookupCategoryCommandValidation()
        {
            this.RuleFor(e => e.Name)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Lookup.CategoryNameRequired)
                .MaximumLength(200).WithErrorCode(ModelValidationErrorCodes.Identity.Lookup.CategoryNameTooLong);

            this.RuleFor(e => e.Description)
                .MaximumLength(500).WithErrorCode(ModelValidationErrorCodes.Identity.Lookup.CategoryDescriptionTooLong)
                .When(e => !string.IsNullOrWhiteSpace(e.Description));
        }
    }
}