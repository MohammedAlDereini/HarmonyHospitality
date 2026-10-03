namespace Harmony.Identity.Handler.Commands.LookupModule.Values.Delete;

public class DeleteLookupValueCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public class DeleteLookupValueCommandValidation : AbstractValidator<DeleteLookupValueCommand>
    {
        public DeleteLookupValueCommandValidation()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Lookup.ValueIdRequired);
        }
    }
}