namespace Harmony.Identity.Handler.Commands.PropertyModule.Contacts;

using Harmony.Identity.Domain.Entities.Aggregates.PropertyModule;

/// <summary>
/// The hotel's contact list, replaced whole: a contact with an id is updated, one without is born, one not listed is
/// removed. One primary at most.
/// </summary>
public class UpdatePropertyContactsCommand : BaseCommandRequest<CallResponse>
{
    public Guid PropertyId { get; set; }

    public List<ContactRequest> Contacts { get; set; } = [];

    public class ContactRequest
    {
        /// <summary>The contact to update; empty = a new contact.</summary>
        public Guid? Id { get; set; }

        public string Name { get; set; } = null!;

        public string? Role { get; set; }

        public string? Email { get; set; }

        public string? Telephone { get; set; }

        public bool IsPrimary { get; set; }
    }

    public class UpdatePropertyContactsCommandValidation : AbstractValidator<UpdatePropertyContactsCommand>
    {
        private const string TelephonePattern = @"^\+?[0-9 ()./-]{5,32}$";

        public UpdatePropertyContactsCommandValidation()
        {
            this.RuleFor(e => e.PropertyId)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Property.IdRequired);

            this.RuleFor(e => e.Contacts)
                .Must(contacts => contacts.Count(c => c.IsPrimary) <= 1)
                .WithErrorCode(ModelValidationErrorCodes.Identity.Property.ContactsOnePrimary);

            this.RuleForEach(e => e.Contacts).ChildRules(contact =>
            {
                contact.RuleFor(c => c.Name)
                    .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Property.ContactNameRequired)
                    .MaximumLength(PropertyContact.NameMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Property.ContactNameTooLong);

                contact.RuleFor(c => c.Role)
                    .MaximumLength(PropertyContact.RoleMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Property.ContactRoleTooLong);

                contact.RuleFor(c => c.Email)
                    .MaximumLength(PropertyContact.EmailMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.Property.ContactEmailInvalidFormat)
                    .EmailAddress().WithErrorCode(ModelValidationErrorCodes.Identity.Property.ContactEmailInvalidFormat)
                    .When(c => !string.IsNullOrWhiteSpace(c.Email));

                contact.RuleFor(c => c.Telephone)
                    .Matches(TelephonePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Property.ContactTelephoneInvalidFormat)
                    .When(c => !string.IsNullOrWhiteSpace(c.Telephone));
            });
        }
    }
}
