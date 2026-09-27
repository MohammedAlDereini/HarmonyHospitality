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
                .NotEmpty().WithMessage("This field is required.")
                .MaximumLength(200).WithMessage("Category name must not exceed 200 characters.");

            this.RuleFor(e => e.Description)
                .MaximumLength(500).WithMessage("Description must not exceed 500 characters.")
                .When(e => !string.IsNullOrWhiteSpace(e.Description));
        }
    }
}