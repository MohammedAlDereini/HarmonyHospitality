namespace Harmony.Identity.Handler.Commands.LookupModule.Categories.Edit;

public class EditLookupCategoryCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public class EditLookupCategoryCommandValidation : AbstractValidator<EditLookupCategoryCommand>
    {
        public EditLookupCategoryCommandValidation()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Lookup.CategoryIdRequired);

            this.RuleFor(e => e.Name)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Lookup.CategoryNameRequired)
                .MaximumLength(200).WithErrorCode(ModelValidationErrorCodes.Identity.Lookup.CategoryNameTooLong);

            this.RuleFor(e => e.Description)
                .MaximumLength(500).WithErrorCode(ModelValidationErrorCodes.Identity.Lookup.CategoryDescriptionTooLong)
                .When(e => !string.IsNullOrWhiteSpace(e.Description));
        }
    }
}