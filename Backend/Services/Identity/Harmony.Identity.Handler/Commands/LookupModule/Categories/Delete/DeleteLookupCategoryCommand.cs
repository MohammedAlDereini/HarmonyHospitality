namespace Harmony.Identity.Handler.Commands.LookupModule.Categories.Delete;

public class DeleteLookupCategoryCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public class DeleteLookupCategoryCommandValidation : AbstractValidator<DeleteLookupCategoryCommand>
    {
        public DeleteLookupCategoryCommandValidation()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithMessage("This field is required.");
        }
    }
}