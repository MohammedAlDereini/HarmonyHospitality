using Harmony.Identity.Domain.Models.ReferenceModule;

namespace Harmony.Identity.Handler.Queries.ReferenceModule.PropertyGroupTypes.List
{
    public class GetPagedPropertyGroupTypesQuery : BasePaginationQueryRequest<CallResponse<PagedResult<PropertyGroupTypeModel>>>
    {
        public static readonly string[] SortableMembers =
        [
            nameof(PropertyGroupTypeModel.Code),
            nameof(PropertyGroupTypeModel.NameEn),
            nameof(PropertyGroupTypeModel.NameAr),
            nameof(PropertyGroupTypeModel.IsExclusive),
            nameof(PropertyGroupTypeModel.IsSystem),
            nameof(PropertyGroupTypeModel.CreatedDate),
            nameof(PropertyGroupTypeModel.ModifiedDate),
        ];

        public GetPagedPropertyGroupTypesQuery()
        {
            this.SortOrder = new List<SortOrderOption>
            {
                new SortOrderOption { MemberName = nameof(PropertyGroupTypeModel.Code), SortOrder = eSortOrder.ASC }
            };
        }

        /// <summary>Matches the code or either name; at least 2 characters.</summary>
        public string? SearchText { get; set; }

        public class GetPagedPropertyGroupTypesQueryValidator : AbstractValidator<GetPagedPropertyGroupTypesQuery>
        {
            public GetPagedPropertyGroupTypesQueryValidator()
            {
                this.RuleFor(e => e.PageNumber)
                    .GreaterThan(0)
                    .WithErrorCode(ModelValidationErrorCodes.General.PageNumberOutOfRange);

                this.RuleFor(e => e.PageSize)
                    .InclusiveBetween(1, 100)
                    .WithErrorCode(ModelValidationErrorCodes.General.PageSizeOutOfRange);

                this.RuleForEach(e => e.SortOrder)
                    .Must(s => s is not null && SortableMembers.Contains(s.MemberName, StringComparer.OrdinalIgnoreCase))
                    .WithErrorCode(ModelValidationErrorCodes.General.SortMemberInvalid);
            }
        }
    }
}
