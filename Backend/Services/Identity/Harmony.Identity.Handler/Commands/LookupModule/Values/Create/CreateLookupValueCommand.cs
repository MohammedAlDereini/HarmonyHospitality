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
                .NotEmpty().WithMessage("This field is required.");

            this.RuleFor(e => e.Name)
                .NotEmpty().WithMessage("This field is required.")
                .MaximumLength(200).WithMessage("Value name must not exceed 200 characters.");

            this.RuleFor(e => e.Description)
                .MaximumLength(500).WithMessage("Description must not exceed 500 characters.")
                .When(e => !string.IsNullOrWhiteSpace(e.Description));
        }
    }
}