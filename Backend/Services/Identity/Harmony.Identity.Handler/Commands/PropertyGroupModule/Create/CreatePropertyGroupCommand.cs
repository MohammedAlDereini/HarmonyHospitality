namespace Harmony.Identity.Handler.Commands.PropertyGroupModule.Create;

using Harmony.Identity.Domain.Entities.Aggregates.PropertyGroupModule;
using Harmony.Identity.Handler.Commands.ReferenceModule;

/// <summary>
/// A hotel group is born for a customer (platform only): a brand, a region, an owner, any named set of hotels.
/// Its code and its type are chosen once. Which hotels are in it comes through the membership doors.
/// </summary>
public class CreatePropertyGroupCommand : BaseCommandRequest<CallResponse<Guid>>
{
    public Guid TenantId { get; set; }

    /// <summary>A row of PropertyGroupTypes (Brand, Region, Owner...). Fixed once saved.</summary>
    public string TypeCode { get; set; } = null!;

    /// <summary>2 to 32 characters: letters, digits, . - _ like RITZ-CARLTON. Upper-cased, unique in the customer.</summary>
    public string Code { get; set; } = null!;

    public string NameEn { get; set; } = null!;

    public string NameAr { get; set; } = null!;

    public string? Description { get; set; }

    /// <summary>Higher wins a standards tie; 0 to 1000.</summary>
    public int Precedence { get; set; }

    public int DisplayOrder { get; set; }

    public string? ExternalIdentifier { get; set; }

    /// <summary>The brand's own columns; only when the type is Brand (00098 otherwise).</summary>
    public BrandDetailRequest? Brand { get; set; }

    public class CreatePropertyGroupCommandValidation : AbstractValidator<CreatePropertyGroupCommand>
    {
        public CreatePropertyGroupCommandValidation()
        {
            this.RuleFor(e => e.TenantId)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.Tenant.IdRequired);

            this.RuleFor(e => e.TypeCode)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.PropertyGroup.TypeRequired)
                .Matches(ReferenceRules.CodePattern).WithErrorCode(ModelValidationErrorCodes.Identity.Reference.CodeInvalidFormat);

            this.RuleFor(e => e.Code)
                .NotEmpty().WithErrorCode(ModelValidationErrorCodes.Identity.PropertyGroup.CodeRequired)
                .Matches("^[A-Za-z0-9._-]{2,32}$").WithErrorCode(ModelValidationErrorCodes.Identity.PropertyGroup.CodeInvalidFormat);

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
