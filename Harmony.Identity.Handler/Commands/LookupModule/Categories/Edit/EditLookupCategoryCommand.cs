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
                .NotEmpty().WithMessage("This field is required.");

            this.RuleFor(e => e.Name)
                .NotEmpty().WithMessage("This field is required.")
                .MaximumLength(200).WithMessage("Category name must not exceed 200 characters.");

            this.RuleFor(e => e.Description)
                .MaximumLength(500).WithMessage("Description must not exceed 500 characters.")
                .When(e => !string.IsNullOrWhiteSpace(e.Description));
        }
    }
}