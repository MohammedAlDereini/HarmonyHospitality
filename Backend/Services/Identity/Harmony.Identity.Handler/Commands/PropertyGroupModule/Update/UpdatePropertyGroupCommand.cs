namespace Harmony.Identity.Handler.Commands.PropertyGroupModule.Update;

using Harmony.Identity.Domain.Entities.Aggregates.PropertyGroupModule;

/// <summary>
/// What the platform may change about a group, replaced whole: the names, the words, the precedence, the order, the
/// reference, and whether it is active. Never the code or the type.
/// </summary>
public class UpdatePropertyGroupCommand : BaseCommandRequest<CallResponse>
{
    public Guid Id { get; set; }

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>Higher wins a standards tie; 0 to 1000.</summary>
    public int Precedence { get; set; }

    public int DisplayOrder { get; set; }

    public string? ExternalIdentifier { get; set; }

    /// <summary>An inactive group keeps its history; no hotel joins it.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>The brand's own columns, replaced whole; null clears them. Only when the type is Brand (00098 otherwise).</summary>
    public BrandDetailRequest? Brand { get; set; }

    public class UpdatePropertyGroupCommandValidation : AbstractValidator<UpdatePropertyGroupCommand>
    {
        public UpdatePropertyGroupCommandValidation()
        {
            this.RuleFor(e => e.Id)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.PropertyGroup.IdRequired);

            this.RuleFor(e => e.NameEn)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.PropertyGroup.NameEnRequired)
                .MaximumLength(PropertyGroup.NameMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.PropertyGroup.NameEnTooLong);

            this.RuleFor(e => e.NameAr)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.PropertyGroup.NameArRequired)
                .MaximumLength(PropertyGroup.NameMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.PropertyGroup.NameArTooLong);

            this.RuleFor(e => e.Description)
                .MaximumLength(PropertyGroup.DescriptionMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.PropertyGroup.DescriptionTooLong);

            this.RuleFor(e => e.Precedence)
                .InclusiveBetween(Domain.Entities.Aggregates.PropertyGroupModule.Precedence.Min, Domain.Entities.Aggregates.PropertyGroupModule.Precedence.Max).WithErrorCode(ModelValidationErrorCodes.Identity.PropertyGroup.PrecedenceOutOfRange);

            this.RuleFor(e => e.DisplayOrder)
                .GreaterThanOrEqualTo(0).WithErrorCode(ModelValidationErrorCodes.Identity.PropertyGroup.DisplayOrderOutOfRange);

            this.RuleFor(e => e.ExternalIdentifier)
                .MaximumLength(PropertyGroup.ExternalIdentifierMaxLength).WithErrorCode(ModelValidationErrorCodes.Identity.PropertyGroup.ExternalIdentifierTooLong);

            this.RuleFor(e => e.Brand!)
                .SetValidator(new BrandDetailRequest.BrandDetailRequestValidation())
                .When(e => e.Brand is not null);
        }
    }
}
